using UnityEngine;
using hp55games.Mobile.Core;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.UI;

namespace hp55games.Mobile.UI
{
    // Routes the Android hardware back button (KeyCode.Escape in the legacy Input Manager this
    // project uses - also the Editor/desktop Escape key, which doubles as a convenient way to
    // test this without a device) through the existing navigation stack, instead of each screen
    // wiring its own back handling.
    //
    // Priority, checked fresh every press (no per-screen special-casing):
    // 1) A popup is open -> close the topmost one (same action UIScrimCatcher's tap-outside-to-
    //    dismiss already performs).
    // 2) Otherwise, the page stack has somewhere to go back to -> pop it.
    // 3) Otherwise (at the root, e.g. the main menu) -> no-op. Quit-on-back at the root is a
    //    deliberate product decision, not implemented here - add it explicitly if wanted.
    //
    // Not scene/prefab-authored - has no [SerializeField] to wire. Bezi: attach this to any
    // persistent (DontDestroyOnLoad) GameObject that's always alive, e.g. GameBootstrap's, so it
    // runs for the whole app session regardless of which content scene is active.
    public sealed class AndroidBackButtonHandler : MonoBehaviour
    {
        private IUIPopupService _popups;
        private IUINavigationService _navigation;

        // Both services are registered after this component's Awake (UIServiceInstaller lives in
        // the additively loaded UI root), so they're resolved lazily: retried each frame until both
        // are available, then never again.
        private bool _servicesResolved;

        private void TryResolveServices()
        {
            if (_servicesResolved) return;

            if (_popups == null) ServiceRegistry.TryResolve(out _popups);
            if (_navigation == null) ServiceRegistry.TryResolve(out _navigation);

            _servicesResolved = _popups != null && _navigation != null;
        }

        private void Update()
        {
            TryResolveServices();

            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (!_servicesResolved)
                Debug.LogWarning("[AndroidBackButtonHandler] Back pressed but IUIPopupService and/or IUINavigationService is not registered yet.", this);

            if (_popups != null && _popups.HasOpenPopups)
            {
                _popups.CloseTop();
                return;
            }

            if (_navigation != null && _navigation.CanGoBack)
            {
                AsyncUtils.FireAndForget(_navigation.PopAsync(), context: nameof(AndroidBackButtonHandler));
            }
        }
    }
}
