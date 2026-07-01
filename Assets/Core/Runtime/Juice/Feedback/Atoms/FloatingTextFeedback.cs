using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Spawns a world-space TextMeshPro label at the origin position that rises upward
    /// and fades out over _duration seconds, then destroys itself.
    /// The TMP GameObject is created entirely at runtime — no prefab dependency.
    /// </summary>
    public sealed class FloatingTextFeedback : MonoBehaviour, IFeedback
    {
        [Header("Timing")]
        [Tooltip("Seconds to wait before the text appears.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Text")]
        [SerializeField] private string _text      = "Hit!";
        [SerializeField] private Color  _color     = Color.white;
        [SerializeField] private float  _fontSize  = 36f;

        [Header("Animation")]
        [SerializeField] private float _duration   = 0.8f;
        [SerializeField] private float _riseSpeed  = 1.5f;

        public void Activate(Transform origin = null)
        {
            Vector3 spawnPos = origin != null ? origin.position : transform.position;
            StartCoroutine(FloatRoutine(spawnPos));
        }

        private IEnumerator FloatRoutine(Vector3 worldPos)
        {
            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);

            // --- Build world-space Canvas ---
            var canvasGo = new GameObject("[FloatingText_Canvas]");
            canvasGo.transform.position = worldPos;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(4f, 2f);
            canvasRect.localScale = Vector3.one * 0.01f;

            // Face the main camera if available.
            if (Camera.main != null)
                canvasGo.transform.rotation = Camera.main.transform.rotation;

            // --- Build TMP label ---
            var textGo   = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp           = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text          = _text;
            tmp.color         = _color;
            tmp.fontSize      = _fontSize;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;

            // --- Animate ---
            float elapsed = 0f;
            Color startColor = _color;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / _duration);

                canvasGo.transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);

                Color c = startColor;
                c.a     = Mathf.Lerp(1f, 0f, t);
                tmp.color = c;

                // Keep facing camera while rising.
                if (Camera.main != null)
                    canvasGo.transform.rotation = Camera.main.transform.rotation;

                yield return null;
            }

            Destroy(canvasGo);
        }
    }
}
