using NUnit.Framework;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;
using hp55games.Blockout.Gameplay.Events;

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
        public void ApplyTo_ReturnsPhaseIntervalUnchanged_AtSessionStart()
        {
            // No time elapsed, no clears yet - ratio is 1.0, the modifier should be a no-op.
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            Assert.AreEqual(1.5f, modifier.ApplyTo(1.5f), 0.0001f);
        }

        [Test]
        public void Tick_ReducesSessionInterval_OnceThe15sBoundaryIsCrossed()
        {
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.TimerTickIntervalSeconds + 1f); // one boundary crossed

            Assert.AreEqual(_config.SessionBaseInterval - _config.TimerDecrementPerTick, modifier.SessionInterval, 0.0001f);
        }

        [Test]
        public void Tick_AppliesOneReductionPerBoundaryCrossed_InASingleCall()
        {
            // A single call spanning several boundaries (e.g. a lag spike) is real elapsed time,
            // not a duplicate trigger - every boundary crossed should reduce the interval once.
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.TimerTickIntervalSeconds * 3f + 1f); // 3 boundaries crossed

            Assert.AreEqual(_config.SessionBaseInterval - _config.TimerDecrementPerTick * 3f, modifier.SessionInterval, 0.0001f);
        }

        [Test]
        public void Tick_IsIgnored_OnASecondCallWithinTheSameFrame()
        {
            // Regression guard: an accidental double call within the same Unity frame (e.g. two
            // Update() paths landing here) must not double-apply the same real-time slice. Both
            // calls below happen synchronously, so Time.frameCount is identical for both.
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.TimerTickIntervalSeconds + 1f);
            float afterFirstTick = modifier.SessionInterval;

            modifier.Tick(_config.TimerTickIntervalSeconds + 1f); // same frame - must be a no-op

            Assert.AreEqual(afterFirstTick, modifier.SessionInterval, 0.0001f);
        }

        [Test]
        public void LayersCleared_ReducesSessionInterval_ByAFixedAmount_RegardlessOfLayerCount()
        {
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            eventBus.Publish(new LayersClearedEvent { LayerCount = 4, PointsAwarded = 1600 });

            Assert.AreEqual(_config.SessionBaseInterval - _config.LayerClearDecrement, modifier.SessionInterval, 0.0001f);
        }

        [Test]
        public void LayersCleared_StacksWithATimerTick_WhenBothOccur()
        {
            // Explicitly-wanted edge case: a timer tick and a layer clear landing together both
            // apply - this is a reward for synchronized timing, not a bug to prevent.
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.TimerTickIntervalSeconds + 1f);
            eventBus.Publish(new LayersClearedEvent { LayerCount = 1, PointsAwarded = 100 });

            float expected = _config.SessionBaseInterval - _config.TimerDecrementPerTick - _config.LayerClearDecrement;
            Assert.AreEqual(expected, modifier.SessionInterval, 0.0001f);
        }

        [Test]
        public void ApplyTo_NeverGoesBelowTheCombinedFloor_EvenOnceTheSessionIntervalIsFullyDepleted()
        {
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Tick(_config.TimerTickIntervalSeconds * 1000f); // drive the session interval to 0

            Assert.AreEqual(0f, modifier.SessionInterval, 0.0001f);
            Assert.AreEqual(_config.CombinedFloorInterval, modifier.ApplyTo(10f), 0.0001f);
        }

        [Test]
        public void Dispose_StopsFurtherReductions_FromLayerClearEvents()
        {
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var modifier = new BlockoutTimeDifficultyModifier(_config);

            modifier.Dispose();
            eventBus.Publish(new LayersClearedEvent { LayerCount = 1, PointsAwarded = 100 });

            Assert.AreEqual(_config.SessionBaseInterval, modifier.SessionInterval, 0.0001f);
        }
    }
}
