using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.Core.UI;
using hp55games.Mobile.Core;

namespace hp55games.Mobile.Game.UI
{
    public sealed class UIPopup_Pause : UIPopupBase
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _leaveButton;

        [Header("Panels")]
        [SerializeField] private GameObject _optionsPanel;

        private ISceneFlowService _sceneFlow;

        protected override void Awake()
        {
            base.Awake();

            _sceneFlow = ServiceRegistry.Resolve<ISceneFlowService>();

            Bind(_resumeButton, OnResumeClicked);
            Bind(_optionsButton, OnOptionsClicked);
            Bind(_leaveButton, OnLeaveClicked);

            if (_optionsPanel != null)
                _optionsPanel.SetActive(false);
        }

        private void OnResumeClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIPopup_Pause] ISceneFlowService not found.");
                return;
            }

            AsyncUtils.FireAndForget(_sceneFlow.ResumeFromPauseAsync(), context: nameof(UIPopup_Pause));
        }

        private void OnOptionsClicked()
        {
            if (_optionsPanel == null)
            {
                Debug.LogWarning("[UIPopup_Pause] _optionsPanel not found or not referenced in the inspector. Options button will do nothing.");
                return;
            }

            _optionsPanel.SetActive(true);
        }

        private void OnLeaveClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIPopup_Pause] ISceneFlowService not found. Leave will do nothing.");
                return;
            }

            AsyncUtils.FireAndForget(_sceneFlow.GoToResultsAsync(), context: nameof(UIPopup_Pause));
        }
    }
}