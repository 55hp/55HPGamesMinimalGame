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

            // Destroying the spawners cleans up their active piece/cubes too (OnDestroy).
            Object.DestroyImmediate(go1);
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
            Assert.IsNotNull(material);

            Object.DestroyImmediate(go); // triggers BlockoutSpawner.OnDestroy(), which also
                                          // destroys the active piece's cube/controller (ClearPreviousRun)

            Assert.IsTrue(material == null); // Unity's overridden == treats a destroyed Object as null
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

            // Destroying the spawner cleans up both the locked piece's cubes (_lockedCubes) and
            // the newly-respawned active piece (OnDestroy/ClearPreviousRun).
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

            // Bent in two dimensions so rotation about the current AxisA mapping (Z - see
            // PieceController.AxisAMapsTo) visibly changes local offsets. The shape is planar in Y
            // (every cell has y=0), and RotatedZ negates y (new_y = -old_y = -0 = 0), so this also
            // can't clip the well's ceiling regardless of spawn height.
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

            Object.DestroyImmediate(go); // cleans up the active piece/cubes too (OnDestroy)
        }

        [Test]
        public void Initialize_DestroysPreviousRunsLockedCubes()
        {
            // Regression test: a fresh Initialize() used to leave every previously-locked piece's
            // cubes sitting in the scene forever, since nothing tracked or referenced them once
            // their PieceController was discarded.
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(new VoxelGrid(3, 3, 3), _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3);

            var lockedCube = spawner.CurrentPieceCubes[0];
            eventBus.Publish(new HardDropRequestedEvent()); // locks the first piece, spawns a second
            Assert.IsNotNull(lockedCube); // still alive - this is "run 1"'s locked placeholder

            // Starting a new run should sweep away the previous one's locked cubes.
            spawner.Initialize(new VoxelGrid(3, 3, 3), _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3);

            Assert.IsTrue(lockedCube == null); // destroyed by the fresh Initialize(), not left behind

            Object.DestroyImmediate(go);
        }
    }
}
