using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.UI;
using hp55games.Ui; // UIRoot

namespace hp55games.Mobile.UI
{
    /// <summary>
    /// Gestisce Overlays: Fade nero, Loading globale, Input Blocker invisibile.
    /// Richiede due prefab Addressables:
    /// - Addr.Content.UI.Overlays.FadeFull    (Image full-screen + CanvasGroup, alpha iniziale 0)
    /// - Addr.Content.UI.Overlays.LoadingFull (spinner + CanvasGroup, alpha iniziale 0)
    /// Il Blocker viene creato al volo (invisibile, solo raycastTarget).
    /// </summary>
    public sealed class UIOverlayService : IUIOverlayService
    {
        private readonly IContentLoader _loader;
        private UIRoot _ui;

        private GameObject _fadeGo;       // istanza del prefab Fade
        private CanvasGroup _fadeCg;

        private GameObject _loadingGo;    // istanza del prefab Loading
        private CanvasGroup _loadingCg;

        private GameObject _blockerGo;    // invisibile, intercetta raycast

        // Caches the in-flight fade-instantiate so PrewarmAsync and FadeInAsync racing each other
        // (e.g. FadeInAsync fired before PrewarmAsync's own InstantiateAsync has returned) await
        // the same instantiate instead of each seeing _fadeGo == null and starting their own,
        // which used to orphan a duplicate overlay instance.
        private Task _fadeGoTask;

        // Address (metti le tue costanti in Addr.cs)
        private const string FADE_ADDR    = hp55games.Addr.Content.UI.Overlays.FadeFull;
        private const string LOADING_ADDR = hp55games.Addr.Content.UI.Overlays.LoadingFull;

        public UIOverlayService()
        {
            _loader = ServiceRegistry.Resolve<IContentLoader>();
            _ui = UIRoot.FindOrCache();
        }

        public async Task PrewarmAsync() => await EnsureFadeGoAsync();

        public async Task FadeInAsync(float duration = 0.2f)
        {
            await EnsureFadeGoAsync();
            if (_fadeGo == null) return;

            await TweenAlpha(_fadeCg, target: 1f, duration);
        }

        // Lazily creates and caches the fade overlay's GameObject via Addressables (alpha stays
        // at 0, invisible). Called both from PrewarmAsync (ahead of time, off the timed path) and
        // FadeInAsync (which used to do this instantiate inline - see PrewarmAsync's remarks on
        // IUIOverlayService for why that made the first fade of a session routinely miss
        // SceneFlowService's OverlayTimeoutMs).
        //
        // Not itself async: the _fadeGo/_fadeGoTask check-then-set below must complete in one
        // synchronous stretch (no await before caching the task) so two callers racing each other
        // - see the _fadeGoTask field doc - are guaranteed to observe the same cached task rather
        // than a window where both see _fadeGo == null and _fadeGoTask == null.
        private Task EnsureFadeGoAsync()
        {
            if (_fadeGo != null) return Task.CompletedTask;
            return _fadeGoTask ??= CreateFadeGoAsync();
        }

        private async Task CreateFadeGoAsync()
        {
            await EnsureUIRootAsync();
            if (_ui == null) return;

            var go = await _loader.InstantiateAsync(FADE_ADDR, _ui.overlays);
            if (go == null) { Debug.LogError("[UIOverlayService] Fade prefab non trovato."); return; }

            _fadeCg = RequireCanvasGroup(go);
            _fadeCg.alpha = 0f;
            _fadeCg.blocksRaycasts = false; // di solito il fade non blocca input, lo fa Blocker
            _fadeCg.interactable = false;
            StretchFull(go);
            _fadeGo = go; // set last: EnsureFadeGoAsync's fast path only sees it once fully configured
        }

        public async Task FadeOutAsync(float duration = 0.2f)
        {
            if (_fadeGo == null || _fadeCg == null) return;
            await TweenAlpha(_fadeCg, target: 0f, duration);
            // Non rilasciamo subito: teniamo il GO in cache per fade successivi
        }

        public async Task ShowLoadingAsync(string note = null)
        {
            await EnsureUIRootAsync();
            if (_ui == null) return;

            if (_loadingGo == null)
            {
                _loadingGo = await _loader.InstantiateAsync(LOADING_ADDR, _ui.overlays);
                if (_loadingGo == null) { Debug.LogError("[UIOverlayService] Loading prefab non trovato."); return; }
                _loadingCg = RequireCanvasGroup(_loadingGo);
                _loadingCg.alpha = 0f;
                _loadingCg.blocksRaycasts = true; // lo spinner blocca input sotto
                _loadingCg.interactable = false;
                StretchFull(_loadingGo);
            }

            // nota opzionale (se presente componente UILoadingView)
            var view = _loadingGo.GetComponent<UILoadingView>();
            if (view != null) view.SetNote(note);

            await TweenAlpha(_loadingCg, target: 1f, duration: 0.15f);
        }

        public void HideLoading()
        {
            if (_loadingGo == null || _loadingCg == null) return;
            // istantaneo per semplicità (puoi farlo async con fade out se preferisci)
            _loadingCg.alpha = 0f;
            _loadingCg.blocksRaycasts = false;
        }

        public void BlockInput(bool on)
        {
            EnsureBlocker();
            if (_blockerGo != null) _blockerGo.SetActive(on);
        }

        // ---------- helpers ----------

        private async Task EnsureUIRootAsync()
        {
            if (_ui != null) return;
            for (int i = 0; i < 180 && _ui == null; i++)
            {
                _ui = UIRoot.FindOrCache();
                await Task.Yield();
            }
            if (_ui == null)
                Debug.LogError("[UIOverlayService] UIRoot non trovato (91_UI_Root non in scena?).");
        }

        private static CanvasGroup RequireCanvasGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private async Task TweenAlpha(CanvasGroup cg, float target, float duration)
        {
            if (cg == null) return;
            float start = cg.alpha;
            float t = 0f;
            while (t < duration)
            {
                if (cg == null) return;
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                await Task.Yield();
            }
            cg.alpha = target;
        }

        private void EnsureBlocker()
        {
            if (_blockerGo != null) return;
            if (_ui == null || _ui.overlays == null) return;

            _blockerGo = new GameObject("Overlay_Blocker", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)_blockerGo.transform;
            rt.SetParent(_ui.overlays, false);
            StretchFull(_blockerGo);

            var img = _blockerGo.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0f);  // invisibile
            img.raycastTarget = true;           // ma blocca i click

            var cg = _blockerGo.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = true;
            cg.interactable = false;

            _blockerGo.SetActive(false);
        }
    }
}
