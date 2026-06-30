using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Flashes one or more text elements to a target color for a fixed duration,
    /// then restores their original colors.
    /// If _targets is empty, searches this GameObject and its children for
    /// TMP_Text first, then falls back to legacy Text.
    /// </summary>
    public sealed class SimpleColorFeedback : MonoBehaviour, IFeedback
    {
        [Header("Targets")]
        [Tooltip("TMP_Text elements to flash. If empty, searches this GameObject and its children.")]
        [SerializeField] private List<TMP_Text> _targets = new();

        [Header("Timing")]
        [Tooltip("Seconds to wait before the flash begins.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Feedback")]
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _duration   = 0.15f;

        // Explicit pair: component reference + original color captured at registration time.
        // Using per-entry structs instead of a shared progressive index removes the
        // ordering dependency that caused wrong color restoration when targets were
        // added at runtime (AddTarget inserts TMP after legacy, breaking the index map).
        private readonly struct ColorEntry
        {
            public readonly TMP_Text  Tmp;
            public readonly Text      Legacy;
            public readonly Color     OriginalColor;

            public ColorEntry(TMP_Text t)   { Tmp = t; Legacy = null;  OriginalColor = t.color; }
            public ColorEntry(Text t)        { Tmp = null; Legacy = t; OriginalColor = t.color; }
        }

        private List<ColorEntry> _entries;
        private Coroutine        _activeRoutine;

        private void Awake()
        {
            _entries = new List<ColorEntry>();

            if (_targets.Count > 0)
            {
                foreach (var t in _targets)
                {
                    if (t == null) continue;
                    _entries.Add(new ColorEntry(t));
                }
            }
            else
            {
                // Auto-discovery fallback: TMP first, then legacy.
                var found = GetComponentsInChildren<TMP_Text>(includeInactive: true);
                if (found.Length > 0)
                {
                    foreach (var t in found)
                        _entries.Add(new ColorEntry(t));
                }
                else
                {
                    var legacy = GetComponentsInChildren<Text>(includeInactive: true);
                    foreach (var t in legacy)
                        _entries.Add(new ColorEntry(t));

                    if (legacy.Length == 0)
                        Debug.LogWarning($"[SimpleColorFeedback] No TMP_Text or Text found on '{gameObject.name}' or its children.", this);
                }
            }
        }

        /// <summary>
        /// Aggiunge un target a runtime risolvendo TMP_Text o Text dal GameObject.
        /// Questo overload non espone TMPro nella firma — usabile da assembly
        /// che non referenziano direttamente Unity.TextMeshPro.
        /// </summary>
        public void AddTarget(GameObject go)
        {
            if (go == null) return;
            if (_entries == null) _entries = new List<ColorEntry>();

            var t = go.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (t != null) { _entries.Add(new ColorEntry(t)); return; }

            var leg = go.GetComponentInChildren<Text>(includeInactive: true);
            if (leg != null) _entries.Add(new ColorEntry(leg));
        }

        public void Activate(Transform origin = null)
        {
            if (_entries == null || _entries.Count == 0)
                return;

            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _activeRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);
            SetAll(_flashColor);
            yield return new WaitForSeconds(_duration);
            RestoreAll();
            _activeRoutine = null;
        }

        private void SetAll(Color color)
        {
            foreach (var e in _entries)
            {
                if (e.Tmp != null)    e.Tmp.color    = color;
                if (e.Legacy != null) e.Legacy.color = color;
            }
        }

        private void RestoreAll()
        {
            foreach (var e in _entries)
            {
                if (e.Tmp != null)    e.Tmp.color    = e.OriginalColor;
                if (e.Legacy != null) e.Legacy.color = e.OriginalColor;
            }
        }
    }
}
