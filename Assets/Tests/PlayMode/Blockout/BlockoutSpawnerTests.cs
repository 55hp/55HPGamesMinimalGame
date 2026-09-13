using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;
using hp55games.Blockout.InputSystem;
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
            var shapes = new List<PolycubeShape> { SingleCellShape() };

            var go1 = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner1 = go1.AddComponent<BlockoutSpawner>();
            spawner1.Initialize(new VoxelGrid(5, 10, 5), _fallCurve, shapes, 5, 10, 5);
            var color1 = spawner1.CurrentPieceCubes[0].GetComponent<Renderer>().material.color;

            var go2 = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner2 = go2.AddComponent<BlockoutSpawner>();
            spawner2.Initialize(new VoxelGrid(5, 10, 5), _fallCurve, shapes, 5, 10, 5);
            var color2 = spawner2.CurrentPieceCubes[0].GetComponent<Renderer>().material.color;

            Assert.AreEqual(color1, color2); // shape index 0 in both -> same color every run

            Object.DestroyImmediate(spawner1.CurrentPieceCubes[0].gameObject);
            Object.DestroyImmediate(spawner1.CurrentPiece.gameObject);
            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(spawner2.CurrentPieceCubes[0].gameObject);
            Object.DestroyImmediate(spawner2.CurrentPiece.gameObject);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void OnDestroy_DestroysTrackedMaterialInstances()
        {
            // Regression test: Renderer.material always instantiates a unique Material that Unity
            // never destroys on its own. Locked cubes are never despawned, so the only safe
            // cleanup point is the spawner's own OnDestroy - this confirms it actually runs.
            var grid = new VoxelGrid(3, 3, 3);
            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3);

            var cubeGameObject = spawner.CurrentPieceCubes[0].gameObject;
            var material = cubeGameObject.GetComponent<Renderer>().material;
            var controllerGameObject = spawner.CurrentPiece.gameObject;
            Assert.IsNotNull(material);

            Object.DestroyImmediate(go); // triggers BlockoutSpawner.OnDestroy()

            Assert.IsTrue(material == null); // Unity's overridden == treats a destroyed Object as null

            // Cleanup: the spawner's cube/controller are independent root GameObjects, not
            // children of `go`, so destroying the spawner doesn't remove them.
            Object.DestroyImmediate(cubeGameObject);
            Object.DestroyImmediate(controllerGameObject);
        }

        [Test]
        public void HardDrop_SyncsCubePositions_ToFinalLockedGridPosition()
        {
            // Regression test: BlockoutSpawner used to only sync cube transforms to GridPosition
            // in Update() while the piece was still unlocked. Locking-and-respawning is fully
            // synchronous (no frame boundary in between), so Update() never got a chance to see
            // the piece's final resting position once IsLocked flipped mid-hard-drop - the cubes
            // were left wherever they were on the last regular frame, looking frozen mid-air.
            var grid = new VoxelGrid(3, 5, 3);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 5, 3);

            var lockedController = spawner.CurrentPiece;
            var lockedCubes = new List<Transform>(spawner.CurrentPieceCubes);

            eventBus.Publish(new HardDropRequestedEvent());

            // Destroy() defers to end of frame, so the just-locked controller and the cube
            // references captured above are still valid to inspect here.
            Assert.IsTrue(lockedController.IsLocked);
            Assert.AreEqual((Vector3)lockedController.GridPosition, lockedCubes[0].position);

            // Cleanup: the spawner's pieces/cubes are independent root GameObjects, not children
            // of `go`, so the old (locked) and new (respawned) ones need tearing down explicitly.
            foreach (var cube in lockedCubes) Object.DestroyImmediate(cube.gameObject);
            Object.DestroyImmediate(lockedController.gameObject);
            foreach (var cube in spawner.CurrentPieceCubes) Object.DestroyImmediate(cube.gameObject);
            Object.DestroyImmediate(spawner.CurrentPiece.gameObject);
            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator SyncCubesToCurrentGridPosition_ReflectsRotatedShape_AfterSuccessfulRotation()
        {
            // Regression test: BlockoutSpawner used to cache each cube's cell offset once at
            // spawn time and reuse it every frame. A successful rotation replaces
            // PieceController.Shape with a new rotated instance (same cell count/order, just
            // repositioned), which the cached offsets never picked up - the rotation was correct
            // in the grid but invisible on screen. Needs a real frame (UnityTest) since the sync
            // only runs from BlockoutSpawner.Update().
            var grid = new VoxelGrid(7, 7, 7);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            // Bent in two dimensions so rotation about the current AxisA mapping (Y - see
            // PieceController.AxisAMapsTo) visibly changes local offsets; RotatedY leaves Y
            // unchanged, so this also can't clip the well's ceiling regardless of spawn height.
            var shape = new PolycubeShape(new[]
            {
                Vector3Int.zero,
                new Vector3Int(1, 0, 0),
                new Vector3Int(1, 0, 1),
            });

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { shape }, 7, 7, 7);

            var controller = spawner.CurrentPiece;

            eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });
            Assert.AreNotSame(shape, controller.Shape); // rotation was applied to the logical shape

            yield return null; // let BlockoutSpawner.Update() run once against the rotated shape

            var cubes = spawner.CurrentPieceCubes;
            var rotatedCells = controller.Shape.Cells;
            for (int i = 0; i < cubes.Count; i++)
            {
                Vector3 expected = (Vector3)(controller.GridPosition + rotatedCells[i]);
                Assert.AreEqual(expected, cubes[i].position);
            }

            foreach (var cube in cubes) Object.DestroyImmediate(cube.gameObject);
            Object.DestroyImmediate(controller.gameObject);
            Object.DestroyImmediate(go);
        }
    }
}
