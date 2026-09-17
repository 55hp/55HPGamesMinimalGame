using NUnit.Framework;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;
using hp55games.Blockout.Gameplay.Events;
using hp55games.Blockout.InputSystem;
using hp55games.Polycubes.Grid;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout.Tests
{
    public class PieceControllerTests
    {
        private BlockoutFallCurveConfig _fallCurve;
        private IEventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _fallCurve = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();
            // A fresh bus per test: PieceController.Awake() subscribes to it, and tests below
            // publish requests directly (gesture recognition itself is out of scope here - only
            // the resulting piece-state logic is under test).
            _eventBus = new EventBus();
            ServiceRegistry.Register(_eventBus);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_fallCurve);
        }

        private static PolycubeShape SingleCellShape() => new PolycubeShape(new[] { Vector3Int.zero });

        private static PolycubeShape TwoCellShape() => new PolycubeShape(new[]
        {
            Vector3Int.zero,
            new Vector3Int(1, 0, 0),
        });

        // Bent in two dimensions (X and Z) so no single-axis rotation (X, Y, or Z) leaves it
        // unchanged - unlike a straight line, which is invariant under rotation about its own
        // axis. Used to test rotation rejection independent of which physical axis AxisA/AxisB
        // currently map to (PieceController.AxisAMapsTo / AxisBMapsTo).
        private static PolycubeShape LShape() => new PolycubeShape(new[]
        {
            Vector3Int.zero,
            new Vector3Int(1, 0, 0),
            new Vector3Int(1, 0, 1),
        });

        // Spacious grid and a start position far from any wall: used by the timing tests below,
        // which only care about phase/interval behavior and must never lock mid-test.
        private PieceController CreateController() =>
            CreateController(SingleCellShape(), new Vector3Int(1, 5, 1), new VoxelGrid(3, 10, 3));

        private PieceController CreateController(PolycubeShape shape, Vector3Int start, VoxelGrid grid)
        {
            var go = new GameObject(nameof(PieceControllerTests));
            var controller = go.AddComponent<PieceController>();
            controller.Initialize(shape, start, _fallCurve, grid);
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

        [Test]
        public void Initialize_AppliesTimeDifficultyModifier_ToCurrentInterval()
        {
            var timeDifficultyConfig = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
            var modifier = new BlockoutTimeDifficultyModifier(timeDifficultyConfig);
            modifier.Tick(timeDifficultyConfig.TimerTickIntervalSeconds + 1f); // one reduction applied

            var go = new GameObject(nameof(PieceControllerTests));
            var controller = go.AddComponent<PieceController>();
            controller.Initialize(SingleCellShape(), new Vector3Int(1, 5, 1), _fallCurve, new VoxelGrid(3, 10, 3), modifier);

            float expectedRatio = modifier.SessionInterval / timeDifficultyConfig.SessionBaseInterval;
            float expected = Mathf.Max(_fallCurve.IntervalForPhase(0) * expectedRatio, timeDifficultyConfig.CombinedFloorInterval);
            Assert.AreEqual(expected, controller.CurrentInterval, 0.0001f);

            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(timeDifficultyConfig);
        }

        [Test]
        public void Tick_RefreshesCurrentInterval_WhenTimeDifficultyModifierChanges_EvenWithoutAPhaseAdvance()
        {
            // Regression guard: CurrentInterval used to only change on AdvancePhase, but the
            // time-based modifier's own 15s timer can shift the combined value independently of
            // PhaseIndex, mid-piece.
            var timeDifficultyConfig = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
            var modifier = new BlockoutTimeDifficultyModifier(timeDifficultyConfig);

            var go = new GameObject(nameof(PieceControllerTests));
            var controller = go.AddComponent<PieceController>();
            controller.Initialize(SingleCellShape(), new Vector3Int(1, 5, 1), _fallCurve, new VoxelGrid(3, 10, 3), modifier);

            float intervalBeforeTimerTick = controller.CurrentInterval;
            int raiseCount = 0;
            controller.FallIntervalChanged += _ => raiseCount++;

            modifier.Tick(timeDifficultyConfig.TimerTickIntervalSeconds + 1f); // session interval drops
            controller.Tick(0.001f); // far short of a phase transition - only the refresh should react

            Assert.AreEqual(0, controller.PhaseIndex); // confirms this wasn't a phase-driven change
            Assert.AreEqual(1, raiseCount);
            Assert.Less(controller.CurrentInterval, intervalBeforeTimerTick);

            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(timeDifficultyConfig);
        }

        [Test]
        public void Tick_LocksPiece_WhenNextStepWouldGoBelowWellFloor()
        {
            var grid = new VoxelGrid(3, 3, 3);
            var start = new Vector3Int(1, 0, 1); // already resting on the floor (y = 0)
            var controller = CreateController(SingleCellShape(), start, grid);

            AdvanceBy(controller, _fallCurve.IntervalForPhase(0) + 0.001f);

            Assert.IsTrue(controller.IsLocked);
            Assert.AreEqual(start, controller.GridPosition); // never moved below the floor
            Assert.IsTrue(grid.IsOccupied(1, 0, 1));

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void Tick_LocksPiece_WhenNextStepWouldOverlapAlreadyLockedCell()
        {
            var grid = new VoxelGrid(3, 3, 3);
            grid.SetOccupied(1, 0, 1, true); // stand-in for a previously locked piece

            var start = new Vector3Int(1, 1, 1);
            var controller = CreateController(SingleCellShape(), start, grid);

            AdvanceBy(controller, _fallCurve.IntervalForPhase(0) + 0.001f);

            Assert.IsTrue(controller.IsLocked);
            Assert.AreEqual(start, controller.GridPosition); // never moved into the occupied cell
            Assert.IsTrue(grid.IsOccupied(1, 1, 1));

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void Tick_WritesEveryShapeCellIntoGrid_WhenLocked()
        {
            var grid = new VoxelGrid(3, 3, 3);
            var start = new Vector3Int(0, 0, 1); // resting on the floor
            var controller = CreateController(TwoCellShape(), start, grid);

            AdvanceBy(controller, _fallCurve.IntervalForPhase(0) + 0.001f);

            Assert.IsTrue(controller.IsLocked);
            Assert.IsTrue(grid.IsOccupied(0, 0, 1));
            Assert.IsTrue(grid.IsOccupied(1, 0, 1));

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void Tick_PublishesLayersClearedEvent_WhenLockCompletesAFullLayer()
        {
            var grid = new VoxelGrid(2, 3, 1);
            grid.SetOccupied(1, 0, 0, true); // layer 0 needs just one more cell to be full

            var start = new Vector3Int(0, 0, 0);
            var controller = CreateController(SingleCellShape(), start, grid);

            LayersClearedEvent received = null;
            _eventBus.Subscribe<LayersClearedEvent>(evt => received = evt);

            AdvanceBy(controller, _fallCurve.IntervalForPhase(0) + 0.001f);

            Assert.IsTrue(controller.IsLocked);
            Assert.IsNotNull(received);
            Assert.AreEqual(1, received.LayerCount);
            Assert.AreEqual(ScoreCalculator.PointsForSimultaneousClears(1), received.PointsAwarded);

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void Locked_ReportsTheClearedLayerY_WhenLockCompletesAFullLayer()
        {
            // BlockoutSpawner needs the exact Y (not just the count) to mirror the collapse in
            // WellCellRenderer before spawning the next piece - see BlockoutSpawner.OnPieceLocked.
            var grid = new VoxelGrid(2, 3, 1);
            grid.SetOccupied(1, 0, 0, true); // layer 0 needs just one more cell to be full

            var start = new Vector3Int(0, 0, 0);
            var controller = CreateController(SingleCellShape(), start, grid);

            int[] receivedClearedLayerYs = null;
            controller.Locked += clearedLayerYs => receivedClearedLayerYs = clearedLayerYs;

            AdvanceBy(controller, _fallCurve.IntervalForPhase(0) + 0.001f);

            Assert.IsTrue(controller.IsLocked);
            CollectionAssert.AreEqual(new[] { 0 }, receivedClearedLayerYs);

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void Locked_ReportsAnEmptyArray_WhenLockCompletesNoFullLayer()
        {
            var grid = new VoxelGrid(3, 3, 3);
            var start = new Vector3Int(1, 0, 1);
            var controller = CreateController(SingleCellShape(), start, grid);

            int[] receivedClearedLayerYs = null;
            controller.Locked += clearedLayerYs => receivedClearedLayerYs = clearedLayerYs;

            AdvanceBy(controller, _fallCurve.IntervalForPhase(0) + 0.001f);

            Assert.IsTrue(controller.IsLocked);
            Assert.IsNotNull(receivedClearedLayerYs);
            Assert.AreEqual(0, receivedClearedLayerYs.Length);

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleMoveRequested_DoesNotMove_WhenTargetCellIsAlreadyLocked()
        {
            var grid = new VoxelGrid(3, 3, 3);
            grid.SetOccupied(2, 1, 1, true); // stand-in for a previously locked piece

            var start = new Vector3Int(1, 1, 1);
            var controller = CreateController(SingleCellShape(), start, grid);

            _eventBus.Publish(new PieceMoveRequestedEvent { Direction = MoveDirection.Right });

            Assert.AreEqual(start, controller.GridPosition); // rejected, never moved into the occupied cell

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleMoveRequested_Moves_WhenTargetCellIsFree()
        {
            var grid = new VoxelGrid(3, 3, 3);
            var start = new Vector3Int(1, 1, 1);
            var controller = CreateController(SingleCellShape(), start, grid);

            _eventBus.Publish(new PieceMoveRequestedEvent { Direction = MoveDirection.Left });

            Assert.AreEqual(new Vector3Int(0, 1, 1), controller.GridPosition);

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleRotateRequested_Rotates_WhenTargetOrientationOnlyExceedsTheOpenTop()
        {
            // LShape is flat (Y-extent 1) - rotating about either exposed axis preserves that
            // axis's own extent and swaps the other two, so the previously-flat dimension always
            // becomes the one that grows (see PlacementRules.CanPlaceAt's allowAboveTop remarks -
            // this is exactly the "piece stands up through the well's open top" case). A Height=1
            // grid used to reject this; it's now allowed - only the side walls/floor still block
            // rotation, see the two tests below.
            var grid = new VoxelGrid(2, 1, 2);
            var shape = LShape();
            var controller = CreateController(shape, Vector3Int.zero, grid);

            _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });

            Assert.AreNotSame(shape, controller.Shape); // committed - a new (rotated) shape instance

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleRotateRequested_DoesNotRotate_WhenRotationWouldExitASideWall()
        {
            // A 3-cell vertical line: rotating about Z (AxisA) turns Y into X with a sign flip
            // (RotatedZ: x' = -y), landing two cells at negative X - out of bounds regardless of
            // grid width, a genuine side-wall violation unrelated to the open-top exemption
            // (which only ever relaxes the upper Y bound).
            var grid = new VoxelGrid(2, 3, 1);
            var shape = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0), new Vector3Int(0, 2, 0) });
            var controller = CreateController(shape, Vector3Int.zero, grid);

            _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });

            Assert.AreSame(shape, controller.Shape); // rejected - still the exact original instance

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleRotateRequested_DoesNotRotate_WhenRotationWouldExitTheFloor()
        {
            // AxisB (X rotation) turns LShape's Z-extent into the new Y-extent with a sign flip -
            // one cell lands at Y = -1. The open-top exemption only ever relaxes the upper bound
            // (Y >= Height); Y < 0 (the floor) is still enforced.
            var grid = new VoxelGrid(2, 3, 2);
            var shape = LShape();
            var controller = CreateController(shape, Vector3Int.zero, grid);

            _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisB, Steps90 = 1 });

            Assert.AreSame(shape, controller.Shape); // rejected - still the exact original instance

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleRotateRequested_Rotates_WhenTargetOrientationFits()
        {
            var grid = new VoxelGrid(3, 3, 3);
            var shape = SingleCellShape();
            var controller = CreateController(shape, new Vector3Int(1, 1, 1), grid);

            _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });

            Assert.AreNotSame(shape, controller.Shape); // committed - a new (rotated) shape instance

            Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleHardDropRequested_DropsToFloorImmediately_AndLocks()
        {
            var grid = new VoxelGrid(3, 5, 3);
            var start = new Vector3Int(1, 4, 1);
            var controller = CreateController(SingleCellShape(), start, grid);

            _eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsTrue(controller.IsLocked);
            Assert.AreEqual(new Vector3Int(1, 0, 1), controller.GridPosition);
            Assert.IsTrue(grid.IsOccupied(1, 0, 1));

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
