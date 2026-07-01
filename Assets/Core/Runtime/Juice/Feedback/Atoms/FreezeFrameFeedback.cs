using System.Collections;
using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Sets Time.timeScale to 0 for _duration real seconds, then restores the previous value.
    /// Uses WaitForSecondsRealtime so the hold is unaffected by the frozen timescale.
    /// Re-entrant calls cancel the previous hold and start a new one.
    /// </summary>
    public sealed class FreezeFrameFeedback : MonoBehaviour, IFeedback
    {
        [Header("Timing")]
        [Tooltip("Seconds to wait (real time) before the freeze begins.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Freeze")]
        [SerializeField] private float _duration = 0.05f;

        private Coroutine _activeRoutine;

        public void Activate(Transform origin = null)
        {
            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            _activeRoutine = StartCoroutine(FreezeRoutine());
        }

        private IEnumerator FreezeRoutine()
        {
            if (_startDelay > 0f)
                yield return new WaitForSecondsRealtime(_startDelay);

            float previous = Time.timeScale;
            Time.timeScale = 0f;

            yield return new WaitForSecondsRealtime(_duration);

            Time.timeScale = previous;
            _activeRoutine = null;
        }
    }
}
