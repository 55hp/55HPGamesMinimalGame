using System.Threading.Tasks;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.SceneFlow;
using hp55games.Mobile.Core.UI;
using hp55games.Mobile.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Game.UI
{
    /// <summary>
    /// Main Menu UI controller.
    /// Wires buttons to high-level services:
    /// - Play -> ISceneFlowService.GoToGameplayAsync()
    /// - Options -> IUINavigationService.PushAsync(optionsPageAddress)
    /// - Credits -> IUINavigationService.PushAsync(creditsPageAddress)
    /// - Exit -> Application.Quit()
    /// 
    /// Addresses are plain strings so you can plug your Addressable keys
    /// (e.g. "content/ui/pages/options_page").
    /// </summary>
    public sealed class UIMainMenuPage : UIPageBase
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button playButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button exitButton;

        private string optionsPageAddress = Addr.Content.UI.Pages.Options_Page;
        private string creditsPageAddress = Addr.Content.UI.Pages.Credits_Page;

        private ISceneFlowService _sceneFlow;
        private IUINavigationService _navigation;

        private void Awake()
        {
            if (!ServiceRegistry.TryResolve<ISceneFlowService>(out _sceneFlow))
            {
                Debug.LogWarning("[UIMainMenuPage] ISceneFlowService not available. Play button will do nothing.");
            }

            if (!ServiceRegistry.TryResolve<IUINavigationService>(out _navigation))
            {
                Debug.LogWarning("[UIMainMenuPage] IUINavigationService not available. Options/Credits will do nothing.");
            }

            Bind(playButton, OnPlayClicked);
            Bind(optionsButton, OnOptionsClicked);
            Bind(creditsButton, OnCreditsClicked);
            Bind(exitButton, OnExitClicked);
        }

        private void OnPlayClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIMainMenuPage] Play clicked but ISceneFlowService is null.");
                return;
            }

            AsyncUtils.FireAndForget(OnPlayClickedAsync(), context: nameof(UIMainMenuPage));
        }

        private async Task OnPlayClickedAsync()
        {
            // 1) Chiudi la pagina del menu se hai un navigation service
            if (_navigation != null)
            {
                await _navigation.PopAsync();
            }

            // 2) Vai al gameplay (scene + state via SceneFlowService)
            await _sceneFlow.GoToGameplayAsync();
        }

        private void OnOptionsClicked()
        {
            if (_navigation == null)
            {
                Debug.LogWarning("[UIMainMenuPage] Options clicked but IUINavigationService is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(optionsPageAddress))
            {
                Debug.LogWarning("[UIMainMenuPage] Options clicked but optionsPageAddress is empty.");
                return;
            }

            AsyncUtils.FireAndForget(_navigation.PushAsync(optionsPageAddress), context: nameof(UIMainMenuPage));
        }

        private void OnCreditsClicked()
        {
            if (_navigation == null)
            {
                Debug.LogWarning("[UIMainMenuPage] Credits clicked but IUINavigationService is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(creditsPageAddress))
            {
                Debug.LogWarning("[UIMainMenuPage] Credits clicked but creditsPageAddress is empty.");
                return;
            }

            AsyncUtils.FireAndForget(_navigation.PushAsync(creditsPageAddress), context: nameof(UIMainMenuPage));
        }

        private void OnExitClicked()
        {
            Debug.Log("[UIMainMenuPage] Exit clicked. Quitting application.");
            Application.Quit();

            // In Editor, Application.Quit() does nothing; the log at least shows it's wired.
        }
    }
}
