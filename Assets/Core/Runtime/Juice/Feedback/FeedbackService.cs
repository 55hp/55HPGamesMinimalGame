using System;
using System.Collections.Generic;
using hp55games.Mobile.Core.Architecture;
using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Registers named FeedbackPlayers and dispatches Play() calls by id.
    /// Attach to a persistent GameObject in the bootstrap or gameplay scene.
    /// Self-registers as IFeedbackService in Awake.
    /// </summary>
    public sealed class FeedbackService : MonoBehaviour, IFeedbackService
    {
        [Serializable]
        public sealed class FeedbackEntry
        {
            public string       id;
            public FeedbackPlayer player;
        }

        [SerializeField] private List<FeedbackEntry> _entries = new();

        private Dictionary<string, FeedbackPlayer> _map;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildMap();
            ServiceRegistry.Register<IFeedbackService>(this);

            // Boot log: mostra tutti gli id registrati per facilitare il debug.
            Debug.Log($"[FeedbackService] Registered {_map.Count} entries: [ {string.Join(", ", _map.Keys)} ]", this);
        }

        // ── IFeedbackService ─────────────────────────────────────────────────

        public void Play(string feedbackId, Transform origin = null)
        {
            if (_map.TryGetValue(feedbackId, out var player))
            {
                player.Play(origin);
                return;
            }

            Debug.LogWarning($"[FeedbackService] No entry found for id '{feedbackId}'. Registered ids: [ {string.Join(", ", _map.Keys)} ]", this);
        }

        public void Register(string feedbackId, FeedbackPlayer player)
        {
            if (string.IsNullOrWhiteSpace(feedbackId) || player == null)
            {
                Debug.LogWarning("[FeedbackService] Register() called with invalid id or null player.", this);
                return;
            }

            if (!_map.TryAdd(feedbackId, player))
            {
                Debug.LogWarning($"[FeedbackService] Register() — id '{feedbackId}' already registered. Overwriting.", this);
                _map[feedbackId] = player;
            }

            Debug.Log($"[FeedbackService] Runtime registered '{feedbackId}' → '{player.gameObject.name}'.", this);
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void BuildMap()
        {
            _map = new Dictionary<string, FeedbackPlayer>(_entries.Count, StringComparer.Ordinal);

            foreach (var entry in _entries)
            {
                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    Debug.LogWarning("[FeedbackService] Entry with empty id skipped.", this);
                    continue;
                }

                if (entry.player == null)
                {
                    Debug.LogWarning($"[FeedbackService] Entry '{entry.id}' has no FeedbackPlayer assigned.", this);
                    continue;
                }

                if (!_map.TryAdd(entry.id, entry.player))
                    Debug.LogWarning($"[FeedbackService] Duplicate id '{entry.id}' — second entry ignored.", this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.id) && !seen.Add(entry.id))
                    Debug.LogWarning($"[FeedbackService] Duplicate id '{entry.id}' detected in Inspector.", this);
            }
        }
#endif
    }
}
