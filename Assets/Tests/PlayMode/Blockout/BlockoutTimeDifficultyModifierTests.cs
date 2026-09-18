using NUnit.Framework;
using UnityEngine;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Tests
{
    public class BlockoutTimeDifficultyModifierTests
    {
        private BlockoutTimeDifficultyConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void CurrentStepDelay_StartsAtStartStepDelay_BeforeAnyTick()
        {
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            Assert.AreEqual(_config.StartStepDelay, modifier.CurrentStepDelay, 0.0001f);
        }

        [Test]
        public void Tick_ReducesStepDelayByDecayPercent_OnceTheBoundaryIsCrossed()
        {
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.DecayIntervalSeconds + 1f); // one boundary crossed

            float expected = _config.StartStepDelay * (1f - _config.DecayPercent);
            Assert.AreEqual(expected, modifier.CurrentStepDelay, 0.0001f);
        }

        [Test]
        public void Tick_AppliesOneReductionPerBoundaryCrossed_InASingleCall()
        {
            // A single call spanning several boundaries (e.g. a lag spike) is real elapsed time,
            // not a duplicate trigger - every boundary crossed should reduce the delay once, and
            // the reductions compound (multiplicative, not additive).
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.DecayIntervalSeconds * 3f + 1f); // 3 boundaries crossed

            float expected = _config.StartStepDelay * Mathf.Pow(1f - _config.DecayPercent, 3f);
            Assert.AreEqual(expected, modifier.CurrentStepDelay, 0.0001f);
        }

        [Test]
        public void Tick_IsIgnored_OnASecondCallWithinTheSameFrame()
        {
            // Regression guard: an accidental double call within the same Unity frame (e.g. two
            // Update() paths landing here) must not double-apply the same real-time slice. Both
            // calls below happen synchronously, so Time.frameCount is identical for both.
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.DecayIntervalSeconds + 1f);
            float afterFirstTick = modifier.CurrentStepDelay;

            modifier.Tick(_config.DecayIntervalSeconds + 1f); // same frame - must be a no-op

            Assert.AreEqual(afterFirstTick, modifier.CurrentStepDelay, 0.0001f);
        }

        [Test]
        public void Tick_NeverGoesBelowMinStepDelay_EvenAfterManyBoundaries()
        {
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.DecayIntervalSeconds * 1000f); // enough boundaries to hit the floor

            Assert.AreEqual(_config.MinStepDelay, modifier.CurrentStepDelay, 0.0001f);
        }
    }
}
