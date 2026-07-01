using System.Collections.Generic;
using System.Threading.Tasks;
using hp55games.Mobile.Core.Architecture;
using UnityEngine;
using UnityEngine.Serialization;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Composes a list of IFeedback atoms and plays them all together.
    /// Serializes MonoBehaviour references in the Inspector; each entry
    /// must implement IFeedback — a warning is logged for invalid entries.
    ///
    /// TWO registration paths — choose one per instance:
    ///
    ///   STATIC  — leave _selfRegisterOnStart = false.
    ///             The FeedbackService maps this player to an id via its
    ///             Inspector entries. No id needed here.
    ///
    ///   DYNAMIC — set _selfRegisterOnStart = true and fill _registrationId.
    ///             Use for players that live in additively-loaded scenes or
    ///             prefabs (e.g. HUD) and cannot be pre-wired in FeedbackService.
    ///
    /// Play()      — fire-and-forget, safe from any context.
    /// PlayAsync() — awaitable wrapper, compatible with async call sites.
    /// </summary>
    public sealed class FeedbackPlayer : MonoBehaviour
    {
        [SerializeField] private List<MonoBehaviour> _feedbacks = new();

        [Header("Self-registration (HUD / additive scenes only)")]
        [Tooltip("Enable ONLY for FeedbackPlayers that live in additively-loaded scenes or prefabs " +
                 "(e.g. HUD). They cannot be pre-wired in FeedbackService, so they register " +
                 "themselves at Start() using the id below.\n\n" +
                 "Leave OFF for players that already appear in FeedbackService's Inspector entries — " +
                 "the id field below is ignored in that case.")]
        [SerializeField] private bool _selfRegisterOnStart = false;

        [FormerlySerializedAs("_feedbackId")]
        [Tooltip("The id under which this player registers itself at runtime. " +
                 "Only used when Self-Register On Start is enabled.")]
        [SerializeField] private string _registrationId = string.Empty;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            if (!_selfRegisterOnStart) return;

            if (string.IsNullOrWhiteSpace(_registrationId))
            {
                Debug.LogWarning($"[FeedbackPlayer] Self-register is ON but Registration Id is empty on '{gameObject.name}'.", this);
                return;
            }

            if (!ServiceRegistry.TryResolve<IFeedbackService>(out var service))
            {
                Debug.LogWarning($"[FeedbackPlayer] Self-register failed: IFeedbackService not found in ServiceRegistry. " +
                                 "Ensure FeedbackService exists in the gameplay scene.", this);
                return;
            }

            service.Register(_registrationId, this);
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void Play(Transform origin = null)
        {
            foreach (var mb in _feedbacks)
            {
                if (mb is IFeedback feedback)
                    feedback.Activate(origin);
            }
        }

        /// <summary>
        /// Calls Play() and returns a completed Task.
        /// Exists so FeedbackPlayer can be awaited in async methods without
        /// requiring callers to know whether feedbacks are sync or async.
        /// </summary>
        public Task PlayAsync(Transform origin = null)
        {
            Play(origin);
            return Task.CompletedTask;
        }

        // ── Editor validation ────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = 0; i < _feedbacks.Count; i++)
            {
                var mb = _feedbacks[i];
                if (mb != null && mb is not IFeedback)
                    Debug.LogWarning(
                        $"[FeedbackPlayer] Entry [{i}] '{mb.GetType().Name}' on '{gameObject.name}' " +
                        $"does not implement IFeedback and will be skipped at runtime.", this);
            }

            if (_selfRegisterOnStart && string.IsNullOrWhiteSpace(_registrationId))
                Debug.LogWarning($"[FeedbackPlayer] Self-register is ON but Registration Id is empty on '{gameObject.name}'.", this);
        }
#endif
    }
}
