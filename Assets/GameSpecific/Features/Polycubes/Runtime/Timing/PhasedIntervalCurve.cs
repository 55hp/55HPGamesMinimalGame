using System;

namespace hp55games.Polycubes.Timing
{
    public static class PhasedIntervalCurve
    {
        public static float IntervalForPhase(
            int phaseIndex,
            float baseInterval,
            float stage1Divisor,
            int stage1End,
            float stage2Divisor,
            int stage2End)
        {
            // Stage 3 pins the value reached at stage2End-1 rather than continuing to divide, so every
            // phase from stage2End onward is folded onto that same capped index before counting divisions.
            int cappedPhase = Math.Min(Math.Max(phaseIndex, 0), Math.Max(stage2End - 1, 0));

            int stage1Divisions = Math.Min(cappedPhase, stage1End);
            int stage2Divisions = Math.Min(Math.Max(cappedPhase - stage1End, 0), stage2End - stage1End);

            float interval = baseInterval;
            interval /= (float)Math.Pow(stage1Divisor, stage1Divisions);
            interval /= (float)Math.Pow(stage2Divisor, stage2Divisions);
            return interval;
        }
    }
}
