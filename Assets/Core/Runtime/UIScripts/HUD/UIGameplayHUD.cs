using System;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Context;
using hp55games.Mobile.Core.Gameplay.Events;
using hp55games.Mobile.Core.Juice;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.UI;
using hp55games.Mobile.Core;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Game.UI
{
    public sealed class UIGameplayHUD : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private UILocalizedText _scoreLabel;
        [SerializeField] private UILocalizedText _livesLabel;
        [SerializeField] private Button _pauseButton;

        private IGameContextService  _context;
        private ISceneFlowService    _sceneFlow;
        private IEventBus            _bus;
        private IFeedbackService     _feedbackService;

        private IDisposable _scoreSub;
        private IDisposable _livesSub;

        private void Awake()
        {
            _sceneFlow = ServiceRegistry.Resolve<ISceneFlowService>();
            _context   = ServiceRegistry.Resolve<IGameContextService>();
            _bus       = ServiceRegistry.Resolve<IEventBus>();

            if (_bus != null)
            {
                _scoreSub = _bus.Subscribe<ScoreChangedEvent>(UpdateScoreLabel);
                _livesSub = _bus.Subscribe<HpChangedEvent>(UpdateLivesLabel);
            }

            if (_pauseButton != null)
                _pauseButton.onClick.AddListener(OnPauseClicked);
        }

        private void Start()
        {
            // IFeedbackService resolved in Start() to avoid ordering issues:
            // FeedbackService.Awake() may not have run yet if both live in the same scene.
            ServiceRegistry.TryResolve<IFeedbackService>(out _feedbackService);

            Init();
        }

        private void OnDestroy()
        {
            if (_pauseButton != null)
                _pauseButton.onClick.RemoveListener(OnPauseClicked);

            _scoreSub?.Dispose();
            _livesSub?.Dispose();
        }

        private void Init()
        {
            if (_scoreLabel != null)
            {
                _scoreLabel.SetSuffix(" :" + _context.Score.ToString());
                _scoreLabel.Refresh();
            }

            if (_livesLabel != null)
            {
                _livesLabel.SetSuffix(" :" + _context.Lives.ToString());
                _livesLabel.Refresh();
            }
        }

        private void UpdateScoreLabel(ScoreChangedEvent _)
        {
            if (_scoreLabel != null)
            {
                _scoreLabel.SetSuffix(" :" + _context.Score.ToString());
                _scoreLabel.Refresh();
            }

            _feedbackService?.Play("score_changed");
        }

        private void UpdateLivesLabel(HpChangedEvent _)
        {
            if (_livesLabel == null)
                return;

            if (_context.Lives < 0)
            {
                _livesLabel.gameObject.SetActive(false);
            }
            else
            {
                _livesLabel.gameObject.SetActive(true);
                _livesLabel.SetSuffix(" :" + _context.Lives.ToString());
                _livesLabel.Refresh();
            }

            _feedbackService?.Play("hp_changed");
        }

        private void OnPauseClicked()
        {
            if (_sceneFlow == null)
                return;

            AsyncUtils.FireAndForget(_sceneFlow.GoToPauseAsync(), context: nameof(UIGameplayHUD));
        }
    }
}
