using System.Threading;
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
    /// - Shop -> IUINavigationService.PushAsync(shopPageAddress)
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
        [SerializeField] private Button shopButton;
        [SerializeField] private Button exitButton;

        private string optionsPageAddress = Addr.Content.UI.Pages.Options_Page;
        private string creditsPageAddress = Addr.Content.UI.Pages.Credits_Page;
        private string shopPageAddress = Addr.Content.UI.Pages.Periodic_Table_Shop_Page;

        private ISceneFlowService _sceneFlow;
        private IUINavigationService _navigation;

        // Cancelled/replaced whenever a new push-triggering button is tapped (Options/Credits/
        // Shop/Play), so a stale in-flight push (e.g. Shop, still loading via Addressables) can
        // never land after a later action - see 01_fsm_shop_bug.md.
        private CancellationTokenSource _pendingPushCts;

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
            Bind(shopButton, OnShopClicked);
            Bind(exitButton, OnExitClicked);
        }

        private void OnPlayClicked()
        {
            if (_sceneFlow == null)
            {
                Debug.LogWarning("[UIMainMenuPage] Play clicked but ISceneFlowService is null.");
                return;
            }

            // Supersede any Options/Credits/Shop push still loading - it must not land after we've
            // already moved on to gameplay.
            _pendingPushCts?.Cancel();

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

            PushPage(optionsPageAddress);
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

            PushPage(creditsPageAddress);
        }

        private void OnShopClicked()
        {
            if (_navigation == null)
            {
                Debug.LogWarning("[UIMainMenuPage] Shop clicked but IUINavigationService is null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(shopPageAddress))
            {
                Debug.LogWarning("[UIMainMenuPage] Shop clicked but shopPageAddress is empty.");
                return;
            }

            PushPage(shopPageAddress);
        }

        // Cancels whatever push is still in flight (e.g. a rapid re-tap, or Options/Credits/Shop
        // tapped one after another) before starting a new one, so only the most recent request can
        // ever land - see 01_fsm_shop_bug.md.
        private void PushPage(string address)
        {
            _pendingPushCts?.Cancel();
            _pendingPushCts = new CancellationTokenSource();
            AsyncUtils.FireAndForget(_navigation.PushAsync(address, _pendingPushCts.Token), context: nameof(UIMainMenuPage));
        }

        private void OnExitClicked()
        {
            Debug.Log("[UIMainMenuPage] Exit clicked. Quitting application.");
            Application.Quit();

            // In Editor, Application.Quit() does nothing; the log at least shows it's wired.
        }
    }
}
