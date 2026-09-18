using UnityEngine;
using hp55games.Mobile.Core;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.UI;

namespace hp55games.Mobile.UI
{
    /// <summary>
    /// Registers all UI-level services once UIRoot scene (91_UI_Root) is loaded.
    /// This runs before we start the first GameState.
    /// </summary>
    public sealed class UIServiceInstaller : MonoBehaviour
    {
        private void Awake()
        {
            // UI services implementations (già esistenti nel tuo progetto)
            ServiceRegistry.Register<IUIPopupService>(new UIPopupService());
            ServiceRegistry.Register<IUINavigationService>(new UINavigationService());

            var overlay = new UIOverlayService();
            ServiceRegistry.Register<IUIOverlayService>(overlay);
            // Warms the fade overlay's Addressables instantiate now, while the menu is still
            // loading, instead of paying for it on the first real FadeInAsync call (the Play tap)
            // - see IUIOverlayService.PrewarmAsync's remarks.
            AsyncUtils.FireAndForget(overlay.PrewarmAsync(), context: nameof(UIServiceInstaller));

            ServiceRegistry.Register<IUIToastService>(new UIToastService());
            ServiceRegistry.Register<IMusicService>(new UIMusicService());

            UIRuntime.MarkServicesReady();
        }
    }
}