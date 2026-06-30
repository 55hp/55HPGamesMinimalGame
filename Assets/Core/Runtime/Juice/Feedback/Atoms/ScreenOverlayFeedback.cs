using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Creates a fullscreen UI Canvas overlay (ScreenSpaceOverlay, sortingOrder 999) on Activate.
    /// Flash  — solid color that fades from full alpha to zero over _fadeDuration.
    /// Vignette — uses _vignetteSprite if assigned; otherwise falls back to Flash behavior.
    /// The overlay Canvas and Image are created at runtime and destroyed after the animation.
    /// </summary>
    public sealed class ScreenOverlayFeedback : MonoBehaviour, IFeedback
    {
        public enum OverlayStyle { Flash, Vignette }

        [Header("Timing")]
        [Tooltip("Seconds to wait before the overlay appears.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Overlay")]
        [SerializeField] private Color        _color        = Color.white;
        [SerializeField] private float        _fadeDuration = 0.2f;
        [SerializeField] private OverlayStyle _style        = OverlayStyle.Flash;

        [Header("Vignette (optional)")]
        [Tooltip("Radial gradient sprite used when _style is Vignette. Falls back to Flash if null.")]
        [SerializeField] private Sprite _vignetteSprite;

        public void Activate(Transform origin = null)
        {
            StartCoroutine(OverlayRoutine());
        }

        private IEnumerator OverlayRoutine()
        {
            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);

            // --- Build transient overlay ---
            var canvasGo = new GameObject("[ScreenOverlay]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasGo);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;

            var imageGo = new GameObject("Image", typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);

            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageGo.GetComponent<Image>();

            bool useSprite = _style == OverlayStyle.Vignette && _vignetteSprite != null;
            if (useSprite)
            {
                image.sprite = _vignetteSprite;
                image.type   = Image.Type.Simple;
            }

            Color startColor = _color;
            startColor.a = _color.a > 0f ? _color.a : 1f;
            image.color  = startColor;

            // --- Fade out ---
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);
                Color c = startColor;
                c.a        = Mathf.Lerp(startColor.a, 0f, t);
                image.color = c;
                yield return null;
            }

            Destroy(canvasGo);
        }
    }
}
