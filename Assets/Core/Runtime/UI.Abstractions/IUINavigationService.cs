using System.Threading;
using System.Threading.Tasks;

namespace hp55games.Mobile.Core.UI
{
    public interface IUINavigationService
    {
        bool CanGoBack { get; }

        // ct lets a caller supersede its own in-flight push (e.g. Play superseding a stale Shop
        // push) - checked after the Addressables load, before the page is activated/stacked, so a
        // cancelled push never becomes visible.
        Task PushAsync(string address, CancellationToken ct = default);
        Task ReplaceAsync(string address);
        Task PopAsync();
    }
}