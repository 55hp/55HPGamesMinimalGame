using System.Collections;
using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Shakes Camera.main via coroutine by randomly offsetting its local position.
    /// Saves and restores the original local position after the shake completes.
    /// If _damping is true, intensity decreases linearly over the duration.
    /// </summary>
    public sealed class CameraShakeFeedback : MonoBehaviour, IFeedback
    {
        [Header("Timing")]
        [Tooltip("Seconds to wait before the shake begins.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Shake")]
        [SerializeField] private float _intensity = 0.2f;
        [SerializeField] private float _duration  = 0.3f;
        [Tooltip("If true, intensity decreases linearly to zero over the duration.")]
        [SerializeField] private bool  _damping   = true;

        private Coroutine _activeRoutine;

        public void Activate(Transform origin = null)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _activeRoutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[CameraShakeFeedback] Camera.main is null — shake skipped.", this);
                yield break;
            }

            Vector3 originalLocalPos = cam.transform.localPosition;
            float   elapsed          = 0f;

            while (elapsed < _duration)
            {
                yield return new WaitForEndOfFrame();

                elapsed += Time.deltaTime;
                float t         = Mathf.Clamp01(elapsed / _duration);
                float currentIntensity = _damping ? _intensity * (1f - t) : _intensity;

                cam.transform.localPosition = originalLocalPos + (Vector3)Random.insideUnitCircle * currentIntensity;
            }

            cam.transform.localPosition = originalLocalPos;
            _activeRoutine = null;
        }
    }
}
