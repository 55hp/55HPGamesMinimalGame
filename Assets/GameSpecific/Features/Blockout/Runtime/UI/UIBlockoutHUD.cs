using System;
using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Context;
using hp55games.Mobile.Core.Gameplay.Events;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.UI;
using hp55games.Blockout.InputSystem;

namespace hp55games.Blockout.UI
{
    // Blockout's own gameplay HUD (Documentation/README.md §2) - replaces the template's generic UIGameplayHUD
    // for 02_Gameplay. BlockoutGameplayState.EnterAsync reaches it with ReplaceAsync alone and
    // attaches nothing to it (no AddComponent). No Lives label - Blockout has no lives concept.
    //
    // The 3 bottom-action-bar buttons publish the exact same IEventBus events
    // BlockoutInputHandler already publishes for swipe/tap gestures - not a parallel input path,
    // see Documentation/README.md §2 (Input).
    //
    // Every reference below is [SerializeField], wired by Bezi in BlockoutHUD.prefab (CLAUDE.md
    // Ownership) - see the checklist in the commit/report for exactly what to assign. A missing
    // reference logs an error and that one feature (score / pause / that button) simply does
    // nothing; nothing here is reconstructed from code.
    public sealed class UIBlockoutHUD : MonoBehaviour
    {
        [Header("Score")]
        [SerializeField] private UILocalizedText _scoreLabel;

        [Header("Pause")]
        [SerializeField] private Button _pauseButton;

        [Header("Bottom action bar")]
        [SerializeField] private Button _rotateLeftButton;
        [SerializeField] private Button _hardDropButton;
        [SerializeField] private Button _rotateRightButton;

        private IGameContextService _context;
        private ISceneFlowService _sceneFlow;
        private IEventBus _eventBus;

        private IDisposable _scoreSub;

        private void Awake()
        {
            if (!ServiceRegistry.TryResolve(out _context))
                Debug.LogError("[UIBlockoutHUD] IGameContextService is not registered.", this);

            if (!ServiceRegistry.TryResolve(out _sceneFlow))
                Debug.LogError("[UIBlockoutHUD] ISceneFlowService is not registered.", this);

            if (!ServiceRegistry.TryResolve(out _eventBus))
                Debug.LogError("[UIBlockoutHUD] IEventBus is not registered.", this);

            if (_eventBus != null)
                _scoreSub = _eventBus.Subscribe<ScoreChangedEvent>(UpdateScoreLabel);

            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseClicked);
            else Debug.LogError("[UIBlockoutHUD] _pauseButton is not assigned in the Inspector.", this);

            if (_rotateLeftButton != null) _rotateLeftButton.onClick.AddListener(OnRotateLeftClicked);
            else Debug.LogError("[UIBlockoutHUD] _rotateLeftButton is not assigned in the Inspector.", this);

            if (_hardDropButton != null) _hardDropButton.onClick.AddListener(OnHardDropClicked);
            else Debug.LogError("[UIBlockoutHUD] _hardDropButton is not assigned in the Inspector.", this);

            if (_rotateRightButton != null) _rotateRightButton.onClick.AddListener(OnRotateRightClicked);
            else Debug.LogError("[UIBlockoutHUD] _rotateRightButton is not assigned in the Inspector.", this);

            RefreshScoreLabel();
        }

        private void OnDestroy()
        {
            _scoreSub?.Dispose();

            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (_rotateLeftButton != null) _rotateLeftButton.onClick.RemoveListener(OnRotateLeftClicked);
            if (_hardDropButton != null) _hardDropButton.onClick.RemoveListener(OnHardDropClicked);
            if (_rotateRightButton != null) _rotateRightButton.onClick.RemoveListener(OnRotateRightClicked);
        }

        private void UpdateScoreLabel(ScoreChangedEvent _) => RefreshScoreLabel();

        private void RefreshScoreLabel()
        {
            if (_scoreLabel == null || _context == null) return;
            _scoreLabel.SetSuffix(" :" + _context.Score.ToString());
            _scoreLabel.Refresh();
        }

        private void OnPauseClicked()
        {
            if (_sceneFlow == null) return;
            AsyncUtils.FireAndForget(_sceneFlow.GoToPauseAsync(), context: nameof(UIBlockoutHUD));
        }

        // Fixed 90-degree steps, not direction-sensitive like the swipe gesture (which derives its
        // sign from swipe delta) - Steps90 = 1 for both by default, same physical step size either
        // way. Flip the sign here if Franci wants the opposite rotation direction after
        // playtesting (Documentation/README.md §10: open design item).
        private void OnRotateLeftClicked() =>
            _eventBus?.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });

        private void OnRotateRightClicked() =>
            _eventBus?.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisB, Steps90 = 1 });

        private void OnHardDropClicked() =>
            _eventBus?.Publish(new HardDropRequestedEvent());
    }
}
