using NUnit.Framework;
using UnityEngine;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout.Tests
{
    public class PieceControllerTests
    {
        private BlockoutFallCurveConfig _fallCurve;

        [SetUp]
        public void SetUp()
        {
            _fallCurve = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_fallCurve);
        }

        private PieceController CreateController()
        {
            var go = new GameObject(nameof(PieceControllerTests));
            var controller = go.AddComponent<PieceController>();
            controller.Initialize(new PolycubeShape(new[] { Vector3Int.zero }), Vector3Int.zero, _fallCurve);
            return controller;
        }

        [Test]
        public void Tick_DoesNotRaiseFallIntervalChanged_WhileStillWaitingOnFirstPhase()
        {
            var controller = CreateController();
            int raiseCount = 0;
            controller.FallIntervalChanged += _ => raiseCount++;

            for (int i = 0; i < 50; i++)
            {
                controller.Tick(0.01f); // 0.5s total, well short of phase 0's 3.0s interval
            }

            Assert.AreEqual(0, raiseCount);
            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void Tick_RaisesFallIntervalChanged_ExactlyOncePerPhaseTransition_NotPerFrame()
        {
            var controller = CreateController();
            int raiseCount = 0;
            controller.FallIntervalChanged += _ => raiseCount++;

            // Cross Waiting (3.0s) then Stepping (0.3s) in many small per-frame-sized ticks: if the
            // controller published once per frame instead of once per transition, this would be >> 1.
            float fullCycle = _fallCurve.IntervalForPhase(0) + _fallCurve.StepDuration;
            AdvanceBy(controller, fullCycle + 0.001f);

            Assert.AreEqual(1, raiseCount);
            Assert.AreEqual(1, controller.PhaseIndex);

            Object.DestroyImmediate(controller.gameObject);
        }

        private static void AdvanceBy(PieceController controller, float totalSeconds)
        {
            const float step = 0.01f;
            float remaining = totalSeconds;
            while (remaining > 0f)
            {
                float dt = Mathf.Min(step, remaining);
                controller.Tick(dt);
                remaining -= dt;
            }
        }
    }
}
