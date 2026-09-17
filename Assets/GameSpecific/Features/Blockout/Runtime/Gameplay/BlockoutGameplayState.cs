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
using hp55games.Blockout.Achievements;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay.Events;
using hp55games.Blockout.UI;

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
        private IBlockoutAchievementService _achievements;
        private BlockoutSpawner _spawner;
        private IDisposable _layersClearedSubscription;

        // Technical Doc Phase 6 coins formula's bonus term (+5 x N per multi-layer clear event
        // during the run) - accumulated here rather than recomputed from anything persistent,
        // since it only matters for the single run currently in progress. Reset to 0 on every
        // fresh run (never on resume - see EnterAsync), paid out alongside the base term
        // (floor(finalScore / 100)) at game over (see OnWellFull).
        private int _bonusCoinsThisRun;

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
            ServiceRegistry.TryResolve(out _achievements);

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
                _bonusCoinsThisRun = 0;
                _achievements?.RecordLoginForToday(); // Shop GDD login-streak family - once per fresh session, not on resume

                // Attaches the runtime FOV fit (see 02_camera_investigation.md) to whatever camera
                // is tagged MainCamera in this scene - only needed once per fresh entry, since the
                // component and its GameObject both survive a pause/resume.
                var mainCamera = UnityEngine.Camera.main;
                if (mainCamera != null && mainCamera.GetComponent<BlockoutWellCamera>() == null)
                {
                    mainCamera.gameObject.AddComponent<BlockoutWellCamera>();
                }

                var navigation = ServiceRegistry.Resolve<IUINavigationService>();
                await navigation.ReplaceAsync(hp55games.Addr.Content.UI.Screens.GameplayHUD);

                // Wires the bottom action bar's rotate-left/hard-drop/rotate-right buttons to the
                // same events BlockoutInputHandler already publishes for swipe/tap - see
                // BlockoutHUDInputButtons. Must run after ReplaceAsync above: UIGameplayHUD's
                // GameObject (and its button children) don't exist until that page is loaded.
                var hud = UnityEngine.Object.FindObjectOfType<hp55games.Mobile.Game.UI.UIGameplayHUD>();
                if (hud != null && hud.GetComponent<BlockoutHUDInputButtons>() == null)
                {
                    hud.gameObject.AddComponent<BlockoutHUDInputButtons>();
                }

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

            if (!ServiceRegistry.TryResolve<IBlockoutSkinService>(out var skinService))
            {
                Debug.LogError("[BlockoutGameplayState] IBlockoutSkinService is not registered - add BlockoutGameplayStateInstaller to the scene.");
                return;
            }

            var activeSkin = skinService.ActiveSkin;
            if (activeSkin == null)
            {
                Debug.LogError("[BlockoutGameplayState] No BlockoutSkin in the catalog - pieces need at least one skin (e.g. BlockoutSkin_Default) to render.");
                return;
            }

            // Null for a non-element skin (Default, Profondita, Juicy Clear): AtomicNumber == 0
            // for those, and MaterialCategory would otherwise silently read as Metallic (enum
            // default 0) - see WellCellRenderer.ShowCell, which treats null the same as Opaque.
            PieceMaterialCategory? materialCategory = activeSkin.AtomicNumber > 0 ? activeSkin.MaterialCategory : null;

            var well = new BlockoutWell(wellConfig);
            _spawner.Initialize(well.Grid, fallCurve, timeDifficultyConfig, BlockoutShapeSet.BuildDefault(), well.Width, well.Height, well.Depth, activeSkin.PieceColors, activeSkin.ClearBehaviour, materialCategory);
        }

        // Reuses the template's existing score infrastructure (IGameContextService.Score,
        // ScoreChangedEvent, UIGameplayHUD) rather than a Blockout-specific score store. Also
        // accumulates the coins formula's bonus term: +5 x N coins per "layer-clear multiplo"
        // (LayerCount >= 2) - a single-layer clear earns no bonus, only the base term computed
        // from final score at game over (see OnWellFull).
        private void OnLayersCleared(LayersClearedEvent evt)
        {
            if (evt.LayerCount >= 2) _bonusCoinsThisRun += 5 * evt.LayerCount;
            _achievements?.RecordMultiClear(evt.LayerCount); // Shop GDD "multi_clear_3plus"

            if (_context == null) return;

            _context.Score += evt.PointsAwarded;
            _eventBus?.Publish(new ScoreChangedEvent());
        }

        // Spawn-column stack reached the well's height: publish BlockoutGameOverEvent (FinalScore
        // read from context.Score, not a separately-tracked total), pay out this run's coins
        // (Technical Doc Phase 6: floor(finalScore / 100) base + the per-clear bonus already
        // accumulated in _bonusCoinsThisRun), and hand off to the template's ResultState - no
        // Blockout-specific result state needed.
        private void OnWellFull()
        {
            if (_spawner != null) _spawner.WellFull -= OnWellFull;

            int finalScore = _context?.Score ?? 0;
            AwardCoins(finalScore);
            _achievements?.RecordRunCompleted(); // Shop GDD "first_run_completed"/"runs_completed_*"
            _achievements?.RecordRunScore(finalScore); // Shop GDD "single_run_score_*"

            _eventBus?.Publish(new BlockoutGameOverEvent { FinalScore = finalScore });

            if (_fsm != null)
            {
                AsyncUtils.FireAndForget(_fsm.ChangeStateAsync(new ResultState()), context: nameof(BlockoutGameplayState));
            }
        }

        private void AwardCoins(int finalScore)
        {
            if (!ServiceRegistry.TryResolve<ISaveService>(out var saveService)) return;

            int coinsEarned = finalScore / 100 + _bonusCoinsThisRun; // int division already floors for a non-negative score
            saveService.Data.coins += coinsEarned;
            saveService.Data.progress.lifetimeCoins += coinsEarned;
            saveService.Save();
        }
    }
}
