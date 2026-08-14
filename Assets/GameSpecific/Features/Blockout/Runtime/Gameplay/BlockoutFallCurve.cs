using hp55games.Polycubes.Timing;

namespace hp55games.Blockout.Gameplay
{
    public static class BlockoutFallCurve
    {
        public const float StepDuration = 0.3f;
        public const float BaseInterval = 3.0f;

        public static float IntervalForPhase(int phaseIndex) =>
            PhasedIntervalCurve.IntervalForPhase(phaseIndex, BaseInterval, 1.2f, 10, 1.1f, 20);
    }
}
