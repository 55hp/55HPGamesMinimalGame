using System;
using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.Core.UI;

namespace hp55games.Mobile.Game.UI
{
    public sealed class UIPopup_Pause : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _leaveButton;

        [Header("Panels")]
        [SerializeField] private GameObject _optionsPanel;

        private ISceneFlowService _sceneFlow;
        private IUIPopupService _popupService;

        private void Awake()
        {
            _sceneFlow = ServiceRegistry.Resolve<ISceneFlowService>();
            _popupService = ServiceRegistry.Resolve<IUIPopupService>();

            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(OnResumeClicked);

            if (_optionsButton != null)
                _optionsButton.onClick.AddListener(OnOptionsClicked);

            if (_leaveButton != null)
                _leaveButton.onClick.AddListener(OnLeaveClicked);

            if(_optionsPanel != null)
                _optionsPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_resumeButton != null)
                _resumeButton.onClick.RemoveListener(OnResumeClicked);

            if (_optionsButton != null)
                _optionsButton.onClick.RemoveListener(OnOptionsClicked);

            if (_leaveButton != null)
                _leaveButton.onClick.RemoveListener(OnLeaveClicked);
        }

        private async void OnResumeClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIPopup_Pause] ISceneFlowService not found.");
                return;
            }

            await _sceneFlow.ResumeFromPauseAsync();
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

        private async void OnLeaveClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIPopup_Pause] ISceneFlowService not found. Leave will do nothing.");
                return;
            }

            await _sceneFlow.GoToResultsAsync();
        }
    }
}