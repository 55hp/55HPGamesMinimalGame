using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Awaitable game-over sequence.
    /// Delegates visual/audio effects to a FeedbackPlayer, then holds
    /// for duration seconds before completing so the caller can proceed
    /// (e.g. GoToResultsAsync).
    ///
    /// Activate() — IFeedback fire-and-forget entry point.
    /// PlayAsync() — awaitable, used directly by GameplayController.
    /// </summary>
    public sealed class GameOverJuiceFeedback : MonoBehaviour, IFeedback
    {
        [SerializeField] private FeedbackPlayer _feedbackPlayer;
        [SerializeField] private float          _duration = 1f;

        [Header("Timing")]
        [Tooltip("Seconds to wait (real time) before the sequence begins.")]
        [SerializeField] private float _startDelay = 0f;

        private bool _isPlaying;

        /// <summary>IFeedback entry point. Fires PlayAsync() without blocking the caller.</summary>
        public void Activate(Transform origin = null) => _ = PlayAsync();

        /// <summary>
        /// Triggers the FeedbackPlayer then waits duration seconds (real time).
        /// Returns immediately if already playing.
        /// </summary>
        public Task PlayAsync()
        {
            if (_isPlaying)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(Routine(tcs));
            return tcs.Task;
        }

        private IEnumerator Routine(TaskCompletionSource<bool> tcs)
        {
            _isPlaying = true;

            if (_startDelay > 0f)
                yield return new WaitForSecondsRealtime(_startDelay);

            if (_feedbackPlayer != null)
                _ = _feedbackPlayer.PlayAsync();

            yield return new WaitForSecondsRealtime(_duration);

            _isPlaying = false;
            tcs.SetResult(true);
        }
    }
}
