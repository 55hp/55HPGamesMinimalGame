using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace hp55games.Mobile.Core.UI
{
    /// <summary>
    /// Base for navigation pages (Main Menu, Results, Credits, ...). Offers Bind() for
    /// buttons, which registers the listener and auto-unregisters it in OnDestroy.
    /// </summary>
    public abstract class UIPageBase : MonoBehaviour
    {
        private readonly List<(Button button, UnityAction action)> _boundButtons = new();

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
    }
}
