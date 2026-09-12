using UnityEngine;
using hp55games.Mobile.Core.Config;
using hp55games.Polycubes.Timing;

namespace hp55games.Blockout.Config
{
    [CreateAssetMenu(fileName = "BlockoutFallCurve", menuName = "hp55games/Blockout/Fall Curve Config")]
    public sealed class BlockoutFallCurveConfig : ScriptableObject, IConfigAsset
    {
        [SerializeField] private float _stepDuration = 0.3f;
        [SerializeField] private float _baseInterval = 3.0f;
        [SerializeField] private float _stage1Divisor = 1.2f;
        [SerializeField] private int _stage1End = 10;
        [SerializeField] private float _stage2Divisor = 1.1f;
        [SerializeField] private int _stage2End = 20;

        public float StepDuration => _stepDuration;
        public float BaseInterval => _baseInterval;
        public float Stage1Divisor => _stage1Divisor;
        public int Stage1End => _stage1End;
        public float Stage2Divisor => _stage2Divisor;
        public int Stage2End => _stage2End;

        public float IntervalForPhase(int phaseIndex) =>
            PhasedIntervalCurve.IntervalForPhase(phaseIndex, BaseInterval, Stage1Divisor, Stage1End, Stage2Divisor, Stage2End);
    }
}
