using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using hp55games.Mobile.Core.Architecture;

namespace hp55games.Mobile.Core.UI
{
    /// <summary>
    /// Base for popups instantiated via IUIPopupService.OpenAsync. Resolves the service once
    /// in Awake and exposes ClosePopup() so popups close themselves through the service —
    /// keeping the service's internal bookkeeping (and the scrim) in sync with what's on
    /// screen, instead of only hiding the popup's own CanvasGroup. Also exposes Bind() for
    /// registering button listeners without repeating onClick.AddListener/RemoveListener in
    /// every popup. Derived classes overriding Awake/OnDestroy must call base.
    /// </summary>
    public abstract class UIPopupBase : MonoBehaviour
    {
        protected IUIPopupService PopupService { get; private set; }

        private readonly List<(Button button, UnityAction action)> _boundButtons = new();

        protected virtual void Awake()
        {
            PopupService = ServiceRegistry.Resolve<IUIPopupService>();
        }

        protected virtual void OnDestroy()
        {
            foreach (var (button, action) in _boundButtons)
                if (button != null) button.onClick.RemoveListener(action);
            _boundButtons.Clear();
        }

        /// <summary>Registers a listener on the button and auto-unregisters it in OnDestroy.</summary>
        protected void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.AddListener(action);
            _boundButtons.Add((button, action));
        }

        /// <summary>Closes this popup through the service (releases the instance, not just hides it).</summary>
        protected void ClosePopup()
        {
            PopupService?.Close(gameObject);
        }
    }
}
