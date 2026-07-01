using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using hp55games.Mobile.Core.UI;

namespace hp55games.Mobile.Core.Architecture.States
{
    public sealed class PauseState : IGameState
    {
        private const string PausePopupAddress = Addr.Content.UI.Popups.Popup_Pause;

        private float _previousTimeScale;
        private GameObject _pausePopup;
        private readonly IGameState _previousState;

        public PauseState(IGameState previousState = null)
        {
            _previousState = previousState;
        }

        public async Task EnterAsync(CancellationToken ct)
        {
            Debug.Log("[PauseState] Enter");

            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            
            try
            {
                AudioListener.pause = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PauseState] Failed to pause audio: {ex.Message}");
            }

            var popupService = ServiceRegistry.Resolve<IUIPopupService>();

            try
            {
                _pausePopup = await popupService.OpenAsync(PausePopupAddress);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PauseState] Failed to open pause popup at '{PausePopupAddress}': {ex}");
            }
        }

        public async Task ExitAsync(CancellationToken ct)
        {
            Debug.Log("[PauseState] Exit");

            Time.timeScale = _previousTimeScale;
            
            try
            {
                AudioListener.pause = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PauseState] Failed to resume audio: {ex.Message}");
            }

            var popupService = ServiceRegistry.Resolve<IUIPopupService>();

            if (_pausePopup != null)
            {
                try
                {
                    popupService.Close(_pausePopup);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PauseState] Failed to close pause popup: {ex}");
                }
                finally
                {
                    _pausePopup = null;
                }
            }
            
            await Task.CompletedTask;
        }

        public IGameState GetPreviousState() => _previousState;
    }
}