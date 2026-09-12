using NUnit.Framework;
using hp55games.Polycubes.Timing;

namespace hp55games.Polycubes.Tests
{
    public class PhasedIntervalCurveTests
    {
        // Blockout's own parameters, used directly here so this test exercises the exact curve shape Blockout relies on.
        private const float BaseInterval = 3.0f;
        private const float Stage1Divisor = 1.2f;
        private const int Stage1End = 10;
        private const float Stage2Divisor = 1.1f;
        private const int Stage2End = 20;

        private static float Interval(int phase) =>
            PhasedIntervalCurve.IntervalForPhase(phase, BaseInterval, Stage1Divisor, Stage1End, Stage2Divisor, Stage2End);

        [Test]
        public void IntervalForPhase_AtPhaseZero_EqualsBaseInterval()
        {
            Assert.AreEqual(3.0f, Interval(0));
        }

        [Test]
        public void IntervalForPhase_StrictlyDecreases_AcrossPhasesZeroToNineteen()
        {
            for (int phase = 0; phase < 19; phase++)
            {
                Assert.Less(Interval(phase + 1), Interval(phase),
                    $"Expected interval to strictly decrease from phase {phase} to {phase + 1}.");
            }
        }

        [Test]
        public void IntervalForPhase_IsFixed_FromStage2EndOnward()
        {
            float floor = Interval(Stage2End - 1);

            Assert.AreEqual(floor, Interval(Stage2End));
            Assert.AreEqual(floor, Interval(Stage2End + 1));
            Assert.AreEqual(floor, Interval(Stage2End + 80));
        }
    }
}
