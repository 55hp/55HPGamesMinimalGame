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

            spawner.Initialize(grid, _fallCurve, new List<PolycubeShape> { SingleCellShape() }, 2, 2, 2);

            Assert.IsTrue(spawner.SpawnBlockedWellFull);
            Assert.IsNull(spawner.CurrentPiece); // detected and reported, not a piece silently locked in place

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
    }
}
