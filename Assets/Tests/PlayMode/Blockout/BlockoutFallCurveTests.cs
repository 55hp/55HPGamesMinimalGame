using NUnit.Framework;
using UnityEngine;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Tests
{
    public class BlockoutFallCurveTests
    {
        [Test]
        public void IntervalForPhase_AtPhaseNineteen_MatchesComputedFloorValue()
        {
            // Open Question 2 (Technical Spec Phase 2): this is the interval floor once the curve flattens,
            // using the default config values (3.0 / 1.2^10 / 1.1^9 seconds ≈ 0.205s between drop steps).
            // Independently computed and cross-checked before trusting it — surfaced here for Franci's
            // playability sanity check, since ~4.9 steps/second is fast and may need tuning.
            var config = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();

            Assert.AreEqual(0.20548f, config.IntervalForPhase(19), 0.0001f);

            Object.DestroyImmediate(config);
        }
    }
}
