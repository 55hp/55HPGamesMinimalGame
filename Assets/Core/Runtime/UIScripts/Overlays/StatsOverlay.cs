using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Core.UIScripts.Overlays
{
    /// <summary>
    /// Persistent FPS / performance counter, top-left corner.
    /// Self-contained: builds its own Canvas + UI at runtime, no prefab needed.
    /// Safe across additive scene loads and unloads: uses a singleton guard so
    /// only one instance ever exists, and survives every scene transition via
    /// DontDestroyOnLoad on its own GameObject.
    /// </summary>
    public sealed class StatsOverlay : MonoBehaviour
    {
        // ── Configuration ──────────────────────────────────────────────────────────
        private const float UPDATE_INTERVAL = 0.5f;
        private const int   FONT_SIZE       = 26;
        private const float PANEL_WIDTH     = 240f;
        private const float PANEL_HEIGHT    = 100f;
        private const float PANEL_PADDING   = 10f;

        private static readonly Color TEXT_COLOR = Color.white;
        private static readonly Color BG_COLOR   = new Color(0f, 0f, 0f, 0.55f);

        // ── Singleton guard ────────────────────────────────────────────────────────
        // Prevents duplicate overlays when the component is in a scene that gets
        // unloaded and reloaded (SceneFlowService pattern).
        private static StatsOverlay _instance;

        // ── Sampling state ─────────────────────────────────────────────────────────
        private Text  _label;
        private float _timer;

        // Accumulated samples for true interval average
        private float _fpsAccum;
        private int   _frameCount;

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Destroy the duplicate — the first instance owns the canvas.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime; // unscaled: shows true perf even on pause
            if (dt <= 0f) return;

            _fpsAccum  += 1f / dt;
            _frameCount++;
            _timer     += dt;

            if (_timer < UPDATE_INTERVAL) return;

            float avgFps = _fpsAccum / _frameCount;
            float avgMs  = 1000f / avgFps;

            // GetTotalAllocatedMemory is available in all build configs since Unity 2019.
            long memMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

            _label.text = $"FPS  {avgFps:F0}\n" +
                          $"MS   {avgMs:F2}\n" +
                          $"MEM  {memMB} MB";

            _fpsAccum  = 0f;
            _frameCount = 0;
            _timer     -= UPDATE_INTERVAL;
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas lives as a child of this GameObject so DontDestroyOnLoad
            // on the parent keeps everything together — no orphaned objects.
            var canvasGo = new GameObject("[StatsOverlay] Canvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            canvasGo.AddComponent<CanvasScaler>();
            // No GraphicRaycaster: the overlay is purely visual.
            // Adding one would intercept input events before the game UI receives them.

            // Background panel — anchored top-left
            var panelGo = new GameObject("[StatsOverlay] Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);

            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color         = BG_COLOR;
            panelImage.raycastTarget = false;

            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin        = new Vector2(0f, 1f);
            panelRt.anchorMax        = new Vector2(0f, 1f);
            panelRt.pivot            = new Vector2(0f, 1f);
            panelRt.sizeDelta        = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
            panelRt.anchoredPosition = new Vector2(PANEL_PADDING, -PANEL_PADDING);

            // Text label
            var textGo = new GameObject("[StatsOverlay] Text");
            textGo.transform.SetParent(panelGo.transform, false);

            _label               = textGo.AddComponent<Text>();
            _label.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize      = FONT_SIZE;
            _label.color         = TEXT_COLOR;
            _label.text          = "FPS  --\nMS   --\nMEM  --";
            _label.alignment     = TextAnchor.UpperLeft;
            _label.raycastTarget = false;

            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f,   4f);
            textRt.offsetMax = new Vector2(-8f, -4f);
        }
    }
}
