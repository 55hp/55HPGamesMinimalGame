using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using hp55games.Mobile.Core;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Architecture.States;
using hp55games.Mobile.Core.Context;
using hp55games.Mobile.Core.Gameplay.Events;
using hp55games.Mobile.Core.UI;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay.Events;

namespace hp55games.Blockout.Gameplay
{
    // Takes the FSM's "gameplay" slot for Blockout, in place of the template's generic
    // GameplayState (which only handles music/HUD/context-reset, no game-specific logic).
    // Registered via BlockoutGameplayStateFactory (see BlockoutGameplayStateInstaller) so
    // SceneFlowService (Core.Runtime) never depends on this type directly.
    public sealed class BlockoutGameplayState : IGameplayState
    {
        private readonly bool _isResuming;

        private IMusicService _music;
        private IGameContextService _context;
        private IEventBus _eventBus;
        private IGameStateMachine _fsm;
        private BlockoutSpawner _spawner;
        private IDisposable _layersClearedSubscription;

        public BlockoutGameplayState(bool isResuming = false)
        {
            _isResuming = isResuming;
        }

        public async Task EnterAsync(CancellationToken ct)
        {
            Debug.Log($"[BlockoutGameplayState] Enter (isResuming: {_isResuming})");

            ServiceRegistry.TryResolve(out _context);
            ServiceRegistry.TryResolve(out _eventBus);
            ServiceRegistry.TryResolve(out _fsm);

            _layersClearedSubscription = _eventBus?.Subscribe<LayersClearedEvent>(OnLayersCleared);

            // Re-acquired every Enter (including resume): the spawner is a scene object that
            // survives a pause, but this state instance does not (SceneFlowService constructs a
            // fresh one via CreateGameplayState(isResuming: true) rather than reusing the old one,
            // mirroring the template's own GameplayState/ResumeFromPauseAsync behavior).
            _spawner = UnityEngine.Object.FindObjectOfType<BlockoutSpawner>();
            if (_spawner != null) _spawner.WellFull += OnWellFull;

            if (!_isResuming)
            {
                if (ServiceRegistry.TryResolve<IMusicService>(out _music))
                {
                    await _music.CrossfadeToAsync(Addr.Content.Audio.Bgm.GameTheme, 0.5f);
                }

                _context?.ResetRun();

                var navigation = ServiceRegistry.Resolve<IUINavigationService>();
                await navigation.ReplaceAsync(hp55games.Addr.Content.UI.Screens.GameplayHUD);

                StartSpawning();
            }
        }

        public Task ExitAsync(CancellationToken ct)
        {
            Debug.Log("[BlockoutGameplayState] Exit");

            _layersClearedSubscription?.Dispose();
            _layersClearedSubscription = null;

            if (_spawner != null)
            {
                _spawner.WellFull -= OnWellFull;
            }

            return Task.CompletedTask;
        }

        // Does what BlockoutSpawner.Start() used to do before Phase 4: resolve the well/fall-curve
        // config and kick off the first spawn. Only called on first entry, never on resume - the
        // spawner and its grid/current piece are untouched by a pause.
        private void StartSpawning()
        {
            if (_spawner == null)
            {
                Debug.LogError("[BlockoutGameplayState] No BlockoutSpawner found in the scene - nothing to spawn.");
                return;
            }

            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalogService))
            {
                Debug.LogError("[BlockoutGameplayState] IConfigCatalogService is not registered - add a ConfigCatalogInstaller (with a populated ConfigCatalog) to the scene.");
                return;
            }

            var fallCurve = catalogService.Get<BlockoutFallCurveConfig>();
            var wellConfig = catalogService.Get<BlockoutWellConfig>();
            var timeDifficultyConfig = catalogService.Get<BlockoutTimeDifficultyConfig>();
            if (fallCurve == null || wellConfig == null || timeDifficultyConfig == null)
            {
                Debug.LogError("[BlockoutGameplayState] Missing BlockoutFallCurveConfig, BlockoutWellConfig, or BlockoutTimeDifficultyConfig in the catalog.");
                return;
            }

            var well = new BlockoutWell(wellConfig);
            _spawner.Initialize(well.Grid, fallCurve, timeDifficultyConfig, BlockoutShapeSet.BuildDefault(), well.Width, well.Height, well.Depth);
        }

        // Reuses the template's existing score infrastructure (IGameContextService.Score,
        // ScoreChangedEvent, UIGameplayHUD) rather than a Blockout-specific score store.
        private void OnLayersCleared(LayersClearedEvent evt)
        {
            if (_context == null) return;

            _context.Score += evt.PointsAwarded;
            _eventBus?.Publish(new ScoreChangedEvent());
        }

        // Spawn-column stack reached the well's height: publish BlockoutGameOverEvent (FinalScore
        // read from context.Score, not a separately-tracked total) and hand off to the template's
        // ResultState - no Blockout-specific result state needed.
        private void OnWellFull()
        {
            if (_spawner != null) _spawner.WellFull -= OnWellFull;

            int finalScore = _context?.Score ?? 0;
            _eventBus?.Publish(new BlockoutGameOverEvent { FinalScore = finalScore });

            if (_fsm != null)
            {
                AsyncUtils.FireAndForget(_fsm.ChangeStateAsync(new ResultState()), context: nameof(BlockoutGameplayState));
            }
        }
    }
}
