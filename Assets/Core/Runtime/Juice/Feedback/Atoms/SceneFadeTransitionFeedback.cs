using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Fades the screen to a solid color then back via a transient ScreenSpaceOverlay Canvas.
    /// Sequence: fade out (transparent → color) → hold → fade in (color → transparent).
    /// Fire-and-forget: Activate() starts the coroutine and returns immediately.
    /// Re-entrant calls are ignored while a transition is already running.
    /// </summary>
    public sealed class SceneFadeTransitionFeedback : MonoBehaviour, IFeedback
    {
        [Header("Timing")]
        [Tooltip("Seconds to wait before the fade begins.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Fade")]
        [SerializeField] private Color _color           = Color.black;
        [SerializeField] private float _fadeOutDuration = 0.3f;
        [SerializeField] private float _holdDuration    = 0.1f;
        [SerializeField] private float _fadeInDuration  = 0.3f;

        private bool _isRunning;

        public void Activate(Transform origin = null)
        {
            if (_isRunning) return;
            StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            _isRunning = true;

            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);

            // --- Build transient overlay ---
            var canvasGo = new GameObject("[SceneFade]", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(canvasGo);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var imageGo = new GameObject("Image", typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);

            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageGo.GetComponent<Image>();

            // --- Fade out: transparent → solid color ---
            yield return AnimateAlpha(image, 0f, 1f, _fadeOutDuration);

            // --- Hold ---
            if (_holdDuration > 0f)
                yield return new WaitForSeconds(_holdDuration);

            // --- Fade in: solid color → transparent ---
            yield return AnimateAlpha(image, 1f, 0f, _fadeInDuration);

            Destroy(canvasGo);
            _isRunning = false;
        }

        private IEnumerator AnimateAlpha(Image image, float from, float to, float duration)
        {
            float elapsed = 0f;
            Color c       = _color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a       = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                image.color = c;
                yield return null;
            }

            c.a         = to;
            image.color = c;
        }
    }
}
