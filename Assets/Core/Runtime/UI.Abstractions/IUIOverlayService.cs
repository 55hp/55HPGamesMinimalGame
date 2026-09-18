using System.Threading.Tasks;

namespace hp55games.Mobile.Core.UI
{
    public interface IUIOverlayService
    {
        // Instantiates the fade overlay via Addressables ahead of time, so the first real
        // FadeInAsync call (always the Play tap - see SceneFlowService's OverlayTimeoutMs
        // remarks) only pays for the tween, not a cold asset load. Safe to call multiple times;
        // a no-op once the fade GameObject already exists.
        Task PrewarmAsync();

        // Fade nero sopra tutto (Overlays)
        Task FadeInAsync(float duration = 0.2f);   // alpha 0 -> 1
        Task FadeOutAsync(float duration = 0.2f);  // alpha 1 -> 0

        // Loading globale (spinner + nota opzionale)
        Task ShowLoadingAsync(string note = null);
        void HideLoading();

        // Blocca input globale (invisibile, solo raycast blocker)
        void BlockInput(bool on);
    }
}