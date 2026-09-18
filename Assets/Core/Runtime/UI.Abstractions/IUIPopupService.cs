using System;
using System.Threading.Tasks;
using UnityEngine;

namespace hp55games.Mobile.Core.UI
{
    public interface IUIPopupService
    {
        Task<GameObject> OpenAsync(string address);
        Task<T> OpenAsync<T>(string address) where T : Component;

        // configure runs on the instantiated component right after instantiation, before the
        // popup is added to the open stack or the scrim fades in - so a caller with data to show
        // (e.g. which item this popup is for) can apply it before the popup is ever visible,
        // instead of after OpenAsync returns (by which point it's already been on-screen,
        // showing stale/default content, for the whole scrim fade).
        Task<T> OpenAsync<T>(string address, Action<T> configure) where T : Component;

        void Close(GameObject popup);
        void CloseTop();
        void CloseAll();

        // Lets a global back-button handler decide whether to close a popup or fall back to
        // page navigation, without either needing to special-case the other.
        bool HasOpenPopups { get; }
    }
}