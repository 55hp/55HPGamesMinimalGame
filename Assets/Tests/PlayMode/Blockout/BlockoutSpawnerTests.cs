using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;
using hp55games.Blockout.InputSystem;
using hp55games.Blockout.Rendering;
using hp55games.Polycubes.Grid;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout.Tests
{
    public class BlockoutSpawnerTests
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

        private static PolycubeShape SingleCellShape() => new PolycubeShape(new[] { Vector3Int.zero });

        // Only needed by tests that verify visuals: BlockoutSpawner finds this via
        // FindObjectOfType, same as BlockoutGameplayState finds BlockoutSpawner itself.
        private static WellCellRenderer CreateCellRenderer()
        {
            ServiceRegistry.Register<IObjectPoolService>(new ObjectPoolService());
            var go = new GameObject(nameof(BlockoutSpawnerTests) + ".WellCellRenderer");
            return go.AddComponent<WellCellRenderer>();
        }

        [Test]
        public void Initialize_DoesNotSpawnPiece_WhenComputedStartPositionIsAlreadyOccupied()
        {
            // Every cell occupied: guarantees the spawn position is blocked regardless of exactly
            // where CenteredTopStart (private) places it - simulates the stack having reached the
            // spawn point, without depending on that formula.
            var grid = new VoxelGrid(2, 2, 2);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                        grid.SetOccupied(x, y, z, true);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            LogAssert.Expect(LogType.Error, new Regex("well is full", RegexOptions.IgnoreCase));

            bool wellFullFired = false;
            spawner.WellFull += () => wellFullFired = true;

            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 2, 2, 2);

            Assert.IsTrue(spawner.SpawnBlockedWellFull);
            Assert.IsNull(spawner.CurrentPiece); // detected and reported, not a piece silently locked in place
            Assert.IsTrue(wellFullFired); // BlockoutGameplayState listens for this to trigger game over

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Initialize_SpawnsPiece_WhenStartPositionIsFree()
        {
            var grid = new VoxelGrid(3, 3, 3);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3);

            Assert.IsFalse(spawner.SpawnBlockedWellFull);
            Assert.IsNotNull(spawner.CurrentPiece);
            Assert.IsFalse(spawner.CurrentPiece.IsLocked);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SpawnedPieceColor_IsDeterministic_ForSameShapeIndex()
        {
            // Regression test: color used to come from an unseeded Random.value, so this would
            // fail intermittently before the fixed-palette change.
            var cellRenderer = CreateCellRenderer();
            var shapes = new List<PolycubeShape> { SingleCellShape() };

            var go1 = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner1 = go1.AddComponent<BlockoutSpawner>();
            spawner1.Initialize(new VoxelGrid(5, 10, 5), _fallCurve, shapes, 5, 10, 5);
            var color1 = cellRenderer.GetCellColor(spawner1.CurrentPiece.GridPosition);

            // spawner2.Initialize below hides spawner1's piece from cellRenderer (HideAll) as a
            // side effect - color1 is already captured, so that's fine.
            var go2 = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner2 = go2.AddComponent<BlockoutSpawner>();
            spawner2.Initialize(new VoxelGrid(5, 10, 5), _fallCurve, shapes, 5, 10, 5);
            var color2 = cellRenderer.GetCellColor(spawner2.CurrentPiece.GridPosition);

            Assert.AreEqual(color1, color2); // shape index 0 in both -> same color every run

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        [Test]
        public void HardDrop_ShowsLockedCellAtFinalPosition_WithDimmedColor()
        {
            var cellRenderer = CreateCellRenderer();
            var grid = new VoxelGrid(3, 5, 3);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 5, 3);

            var activeColor = cellRenderer.GetCellColor(spawner.CurrentPiece.GridPosition);

            eventBus.Publish(new HardDropRequestedEvent());

            // CenteredTopStart centers a single-cell shape at x=(3-1)/2=1, z=1; hard drop always
            // reaches the empty floor, y=0.
            var finalPos = new Vector3Int(1, 0, 1);
            Assert.IsTrue(cellRenderer.IsCellShown(finalPos));

            var lockedColor = cellRenderer.GetCellColor(finalPos);
            Assert.AreNotEqual(activeColor, lockedColor); // dimmed in place, not left at the bright active color

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        [UnityTest]
        public IEnumerator Rotation_ShowsCellsAtRotatedPositions_AndHidesTheOnesNoLongerOccupied()
        {
            // Regression test: BlockoutSpawner used to cache each cube's cell offset once at
            // spawn time and reuse it every frame, so a successful rotation (which replaces
            // PieceController.Shape with a new rotated instance) never showed up visually.
            // Needs a real frame (UnityTest): the visual sync only runs from
            // BlockoutSpawner.Update().
            var cellRenderer = CreateCellRenderer();
            var grid = new VoxelGrid(7, 7, 7);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            // Bent in Y and Z (never X) so rotation about the current AxisA mapping (Z - see
            // PieceController.AxisAMapsTo, RotatedZ: new_y = old_x) can't push a cell above the
            // well's ceiling regardless of spawn height, since old_x is 0 for every cell here.
            var shape = new PolycubeShape(new[]
            {
                Vector3Int.zero,
                new Vector3Int(0, 1, 0),
                new Vector3Int(0, 1, 1),
            });

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { shape }, 7, 7, 7);

            var controller = spawner.CurrentPiece;
            var originalCells = new Vector3Int[shape.Cells.Count];
            for (int i = 0; i < shape.Cells.Count; i++)
                originalCells[i] = controller.GridPosition + shape.Cells[i];

            foreach (var cell in originalCells) Assert.IsTrue(cellRenderer.IsCellShown(cell));

            eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });
            Assert.AreNotSame(shape, controller.Shape); // rotation was applied to the logical shape

            yield return null; // let BlockoutSpawner.Update() run once against the rotated shape

            var rotatedCells = controller.Shape.Cells;
            var newWorldCells = new Vector3Int[rotatedCells.Count];
            for (int i = 0; i < rotatedCells.Count; i++)
            {
                newWorldCells[i] = controller.GridPosition + rotatedCells[i];
                Assert.IsTrue(cellRenderer.IsCellShown(newWorldCells[i]));
            }

            // Any pre-rotation cell that isn't also part of the rotated shape should now be hidden.
            foreach (var oldCell in originalCells)
            {
                if (System.Array.IndexOf(newWorldCells, oldCell) < 0)
                {
                    Assert.IsFalse(cellRenderer.IsCellShown(oldCell));
                }
            }

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        [Test]
        public void Initialize_HidesPreviousRunsLockedCells()
        {
            // Regression test: a fresh Initialize() used to leave every previously-locked cell
            // visible forever, since nothing tracked or referenced them once their
            // PieceController was discarded.
            var cellRenderer = CreateCellRenderer();
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(new VoxelGrid(3, 3, 3), _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3);

            var spawnPos = spawner.CurrentPiece.GridPosition;
            eventBus.Publish(new HardDropRequestedEvent()); // locks the first piece, spawns a second
            var lockedPos = new Vector3Int(spawnPos.x, 0, spawnPos.z); // hard drop always reaches the floor
            Assert.IsTrue(cellRenderer.IsCellShown(lockedPos)); // still visible - "run 1"'s locked placeholder

            // Starting a new run should sweep away the previous one's locked cells.
            spawner.Initialize(new VoxelGrid(3, 3, 3), _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3);

            Assert.IsFalse(cellRenderer.IsCellShown(lockedPos));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }
    }
}
