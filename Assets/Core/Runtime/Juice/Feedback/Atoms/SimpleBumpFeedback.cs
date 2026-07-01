using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Scales one or more target Transforms to a bump size for a fixed duration,
    /// then restores their original local scales.
    /// If _targets is empty, falls back to the component's own transform.
    /// </summary>
    public sealed class SimpleBumpFeedback : MonoBehaviour, IFeedback
    {
        [Header("Targets")]
        [Tooltip("Transforms to scale. If empty, uses this GameObject's own transform.")]
        [SerializeField] private List<Transform> _targets = new();

        [Header("Timing")]
        [Tooltip("Seconds to wait before the bump begins.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Feedback")]
        [SerializeField] private float _scaleFactor = 1.2f;
        [SerializeField] private float _duration    = 0.15f;

        private List<Transform> _resolved;
        private List<Vector3>   _originalScales;
        private Coroutine       _activeRoutine;

        // Fallback scale captured once in Awake so BumpRoutine never reads
        // transform.localScale mid-animation (which would capture the inflated scale).
        private Vector3         _fallbackOriginalScale;

        // Pre-allocated fallback lists — avoids heap allocation in BumpRoutine
        // when _resolved is empty (no explicit targets assigned in the Inspector).
        private List<Transform> _fallbackTargets;
        private List<Vector3>   _fallbackScales;

        private void Awake()
        {
            _fallbackOriginalScale = transform.localScale;

            _resolved       = new List<Transform>(_targets);
            _originalScales = new List<Vector3>(_resolved.Count);
            foreach (var t in _resolved)
                _originalScales.Add(t != null ? t.localScale : Vector3.one);

            _fallbackTargets = new List<Transform> { transform };
            _fallbackScales  = new List<Vector3>   { _fallbackOriginalScale };
        }

        /// <summary>
        /// Aggiunge un target a runtime.
        /// Usato da HUDFeedbackBinder dopo il caricamento additivo dell'HUD.
        /// </summary>
        public void AddTarget(Transform t)
        {
            if (t == null) return;
            if (_resolved == null) _resolved = new List<Transform>();
            if (_originalScales == null) _originalScales = new List<Vector3>();
            _resolved.Add(t);
            _originalScales.Add(t.localScale);
        }

        public void Activate(Transform origin = null)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _activeRoutine = StartCoroutine(BumpRoutine());
        }

        private IEnumerator BumpRoutine()
        {
            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);
            // Use the pre-captured fallback scale, never transform.localScale at call time.
            var targets = _resolved.Count > 0 ? _resolved : _fallbackTargets;
            var scales  = _resolved.Count > 0 ? _originalScales : _fallbackScales;

            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null)
                    targets[i].localScale = scales[i] * _scaleFactor;

            yield return new WaitForSeconds(_duration);

            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null)
                    targets[i].localScale = scales[i];

            _activeRoutine = null;
        }
    }
}
