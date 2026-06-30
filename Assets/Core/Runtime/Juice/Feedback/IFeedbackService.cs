using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Centralized service for triggering named feedback effects.
    /// Callers identify a feedback by id without holding a direct reference
    /// to any FeedbackPlayer.
    /// </summary>
    public interface IFeedbackService
    {
        void Play(string feedbackId, Transform origin = null);

        /// <summary>
        /// Registra un FeedbackPlayer con un id a runtime.
        /// Usato da FeedbackPlayer con _autoRegister abilitato,
        /// tipicamente da prefab caricati additivamente (es. HUD).
        /// </summary>
        void Register(string feedbackId, FeedbackPlayer player);
    }
}
