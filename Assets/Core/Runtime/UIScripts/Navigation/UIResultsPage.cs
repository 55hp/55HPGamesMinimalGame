using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Context;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.Core.UI;
using hp55games.Mobile.Core;
using hp55games.Mobile.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Game.UI
{
    /// <summary>
    /// Generic Results page:
    /// - Shows final score from GameContext.
    /// - Retry -> go back to Gameplay via SceneFlowService.
    /// - Main Menu -> go back to Menu via SceneFlowService.
    /// </summary>
    public sealed class UIResultsPage : UIPageBase
    {
        [Header("UI")]
        [SerializeField] private UILocalizedText _scoreLabel;
        [SerializeField] private UILocalizedText _bestScoreLabel;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _menuButton;

        private ISaveService _save;
        private IGameContextService _context;
        private ISceneFlowService _sceneFlow;

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _save);
            ServiceRegistry.TryResolve(out _context);
            _sceneFlow = ServiceRegistry.Resolve<ISceneFlowService>();

            int score = _context?.Score ?? 0;

            if (_scoreLabel != null)
            {
                _scoreLabel.SetSuffix(" :" + score.ToString());
                _scoreLabel.Refresh();
            }

            Bind(_retryButton, OnRetryClicked);
            Bind(_menuButton, OnMenuClicked);
        }
        
        private void Start()
        {
            int finalScore = _context != null ? _context.Score : 0;
            int bestScore  = _save != null ? _save.Data.progress.bestScore : 0;

            if (_scoreLabel != null)
            {
                _scoreLabel.SetSuffix(" :" + finalScore);
                _scoreLabel.Refresh();
            }

            if (_bestScoreLabel != null)
            {
                _bestScoreLabel.SetSuffix(" :" + bestScore);
                _bestScoreLabel.Refresh();
            }
        }

        private void OnRetryClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIResultsPage] ISceneFlowService not found. Retry will do nothing.");
                return;
            }

            AsyncUtils.FireAndForget(_sceneFlow.GoToGameplayAsync(), context: nameof(UIResultsPage));
        }

        private void OnMenuClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIResultsPage] ISceneFlowService not found. Menu will do nothing.");
                return;
            }

            AsyncUtils.FireAndForget(_sceneFlow.GoToMenuAsync(), context: nameof(UIResultsPage));
        }
    }
}
