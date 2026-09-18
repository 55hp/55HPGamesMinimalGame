using UnityEngine;
using hp55games.Mobile.Core.Config;

namespace hp55games.Blockout.Config
{
    // Tuning for BlockoutTimeDifficultyModifier - the sole source of the fall step delay.
    // Purely time-based: the delay decays every DecayIntervalSeconds by DecayPercent, down to
    // MinStepDelay, and never depends on how many pieces or layers have been cleared.
    [CreateAssetMenu(fileName = "BlockoutTimeDifficulty", menuName = "hp55games/Blockout/Time Difficulty Config")]
    public sealed class BlockoutTimeDifficultyConfig : ScriptableObject, IConfigAsset
    {
        [Header("Step Delay")]
        [Tooltip("Interval in seconds between piece steps at the start of a session.")]
        [SerializeField] private float _startStepDelay = 3.0f;

        [Header("Decay Over Time")]
        [Tooltip("How often, in seconds, the step delay is reduced.")]
        [SerializeField] private float _decayIntervalSeconds = 10.0f;
        [Tooltip("Fraction the step delay is reduced by on every decay tick (e.g. 0.1 = -10%).")]
        [SerializeField, Range(0f, 1f)] private float _decayPercent = 0.1f;

        [Header("Limits")]
        [Tooltip("Minimum step delay in seconds; decay never goes below this.")]
        [SerializeField] private float _minStepDelay = 0.7f;

        public float StartStepDelay => _startStepDelay;
        public float DecayIntervalSeconds => _decayIntervalSeconds;
        public float DecayPercent => _decayPercent;
        public float MinStepDelay => _minStepDelay;
    }
}
