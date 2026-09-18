using UnityEngine;
using hp55games.Mobile.Core.Config;

namespace hp55games.Blockout.Config
{
    [CreateAssetMenu(fileName = "BlockoutFallCurve", menuName = "hp55games/Blockout/Fall Curve Config")]
    public sealed class BlockoutFallCurveConfig : ScriptableObject, IConfigAsset
    {
        [Header("Fall Step Timing")]
        [Tooltip("Seconds for a piece to visually fall one grid cell.")]
        [SerializeField] private float _stepDuration = 0.3f;

        [Tooltip("Easing curve for the visual movement between two vertical grid cells. The horizontal axis is normalized movement time and the vertical axis is normalized movement distance.")]
        [SerializeField] private AnimationCurve _stepCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Fallback wait interval between fall steps, used only when no BlockoutTimeDifficultyModifier is wired (e.g. in tests). In normal play, BlockoutTimeDifficultyConfig's step delay takes over instead.")]
        [SerializeField] private float _baseInterval = 3.0f;

        public float StepDuration => _stepDuration;
        public float BaseInterval => _baseInterval;
        public AnimationCurve StepCurve => _stepCurve;

        public float EvaluateStepCurve(float normalizedTime)
        {
            if (_stepCurve == null) return Mathf.Clamp01(normalizedTime);
            return Mathf.Clamp01(_stepCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
        }
    }
}
