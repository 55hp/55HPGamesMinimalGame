using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        private BlockoutTimeDifficultyConfig _timeDifficultyConfig;

        [SetUp]
        public void SetUp()
        {
            _fallCurve = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();
            _timeDifficultyConfig = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_fallCurve);
            Object.DestroyImmediate(_timeDifficultyConfig);
        }

        private static PolycubeShape SingleCellShape() => new PolycubeShape(new[] { Vector3Int.zero });

        // Stand-in for a real skin's PieceColors (Technical Doc Phase 3 - BlockoutSpawner no
        // longer has a fixed palette of its own) for tests that actually check color content;
        // tests that don't care pass null and get BlockoutSpawner's Color.white fallback instead.
        private static readonly Color[] TestPieceColors = { Color.red, Color.blue, Color.green };

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

            // This test is about the spawn-blocked/WellFull logic, not rendering, so it
            // deliberately doesn't set up a WellCellRenderer - Initialize() logs its own error
            // about that (BlockoutSpawner.cs:103), on top of the "well is full" one below.
            //
            // (?i) inline flag, not the RegexOptions.IgnoreCase overload: LogAssert.Expect stores
            // the regex via ToString() and reconstructs it later, which silently drops
            // RegexOptions - only inline flags baked into the pattern text survive that round
            // trip. Passing RegexOptions.IgnoreCase here compiles and looks right but never
            // actually applies, a known Unity Test Framework gotcha.
            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer"));
            LogAssert.Expect(LogType.Error, new Regex("(?i)well is full"));

            bool wellFullFired = false;
            spawner.WellFull += () => wellFullFired = true;

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 2, 2, 2, null, null);

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

            // This test is about the spawn-success logic, not rendering, so it deliberately
            // doesn't set up a WellCellRenderer - Initialize() logs its own error about that
            // (BlockoutSpawner.cs:103). (?i) inline flag, not RegexOptions.IgnoreCase - see the
            // comment in Initialize_DoesNotSpawnPiece_WhenComputedStartPositionIsAlreadyOccupied.
            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer"));

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3, null, null);

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
            spawner1.Initialize(new VoxelGrid(5, 10, 5), _fallCurve, _timeDifficultyConfig, shapes, 5, 10, 5, TestPieceColors, null);
            var color1 = cellRenderer.GetCellColor(spawner1.CurrentPiece.GridPosition);

            // spawner2.Initialize below hides spawner1's piece from cellRenderer (HideAll) as a
            // side effect - color1 is already captured, so that's fine.
            var go2 = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner2 = go2.AddComponent<BlockoutSpawner>();
            spawner2.Initialize(new VoxelGrid(5, 10, 5), _fallCurve, _timeDifficultyConfig, shapes, 5, 10, 5, TestPieceColors, null);
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

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 3, 5, 3, TestPieceColors, null);

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

        [Test]
        public void Initialize_ForwardsMaterialCategory_ToEveryShownCell()
        {
            // Periodic Table GDD: BlockoutGameplayState.StartSpawning computes this from the
            // active skin (null for a non-element skin) and passes it straight through.
            var cellRenderer = CreateCellRenderer();
            var metallic = new Material(Shader.Find("Sprites/Default"));
            typeof(WellCellRenderer).GetField("_metallicMaterial", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cellRenderer, metallic);
            var grid = new VoxelGrid(3, 5, 3);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 3, 5, 3, TestPieceColors, null, PieceMaterialCategory.Metallic);

            var shownAt = spawner.CurrentPiece.GridPosition;
            var shownCells = (Dictionary<Vector3Int, PooledObject>)typeof(WellCellRenderer)
                .GetField("_shownCells", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(cellRenderer);
            var actualMaterial = shownCells[shownAt].GetComponent<Renderer>().sharedMaterial;

            Assert.AreEqual(metallic, actualMaterial);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
            Object.DestroyImmediate(metallic);
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
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { shape }, 7, 7, 7, null, null);

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

            spawner.Initialize(new VoxelGrid(3, 3, 3), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3, null, null);

            var spawnPos = spawner.CurrentPiece.GridPosition;
            eventBus.Publish(new HardDropRequestedEvent()); // locks the first piece, spawns a second
            var lockedPos = new Vector3Int(spawnPos.x, 0, spawnPos.z); // hard drop always reaches the floor
            Assert.IsTrue(cellRenderer.IsCellShown(lockedPos)); // still visible - "run 1"'s locked placeholder

            // Starting a new run should sweep away the previous one's locked cells.
            spawner.Initialize(new VoxelGrid(3, 3, 3), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 3, 3, 3, null, null);

            Assert.IsFalse(cellRenderer.IsCellShown(lockedPos));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        // Stand-in BlockoutClearBehaviour for assertions - see also
        // BlockoutClearBehaviourTests.RecordingClearBehaviour, same idea.
        private sealed class RecordingClearBehaviour : BlockoutClearBehaviour
        {
            public BlockoutClearContext? LastContext;
            public override void OnLayersCleared(BlockoutClearContext context) => LastContext = context;
        }

        [Test]
        public void HardDrop_DispatchesToClearBehaviour_WithTheClearedCellsPositionsAndColors()
        {
            // width=1, depth=1: a single-cell piece landing on the floor always completes the
            // (only possible) layer, so this hard drop is guaranteed to trigger a clear.
            var cellRenderer = CreateCellRenderer();
            var grid = new VoxelGrid(1, 3, 1);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var behaviour = ScriptableObject.CreateInstance<RecordingClearBehaviour>();
            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 3, 1, null, behaviour);

            eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsTrue(behaviour.LastContext.HasValue);
            Assert.AreEqual(1, behaviour.LastContext.Value.LayerCount);
            Assert.AreEqual(1, behaviour.LastContext.Value.ClearedCellPositions.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), behaviour.LastContext.Value.ClearedCellPositions[0]);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
            Object.DestroyImmediate(behaviour);
        }

        [Test]
        public void HardDrop_DoesNotDragTheNewlySpawnedPieceDownWithTheCollapse()
        {
            // Regression guard: the clear/collapse dispatch used to run from a LayersClearedEvent
            // handler, which fired AFTER the next piece had already spawned and been shown - the
            // collapse's shift-everything-above-down pass would then wrongly drag the new piece's
            // just-shown cells down too. Must run before SpawnNext() instead (see
            // BlockoutSpawner.OnPieceLocked).
            var cellRenderer = CreateCellRenderer();
            var grid = new VoxelGrid(1, 3, 1);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 3, 1, null, null);

            var spawnPos = spawner.CurrentPiece.GridPosition; // (0, 2, 0) - top of the well

            eventBus.Publish(new HardDropRequestedEvent()); // locks at y=0, clears the only layer, spawns piece 2

            Assert.IsNotNull(spawner.CurrentPiece);
            Assert.IsFalse(spawner.CurrentPiece.IsLocked);
            Assert.AreEqual(spawnPos, spawner.CurrentPiece.GridPosition); // still at the real spawn point, not shifted down
            Assert.IsTrue(cellRenderer.IsCellShown(spawnPos)); // its cell is where it's supposed to be

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        [Test]
        public void Initialize_ResetsTimeDifficulty_ForANewRun()
        {
            // Regression guard: BlockoutSpawner.Initialize() must construct a fresh
            // BlockoutTimeDifficultyModifier per run (and dispose the previous one's
            // subscription) so the session timer restarts from zero on every new game.
            //
            // The session-interval reduction is triggered by an actual layer clear now, not by
            // publishing LayersClearedEvent directly - BlockoutSpawner no longer publishes it
            // itself (the clear/collapse dispatch moved to PieceController.Locked, a synchronous
            // Action<int[]>, to fix a real sequencing bug - see
            // HardDrop_DoesNotDragTheNewlySpawnedPieceDownWithTheCollapse above). A width=1,
            // depth=1 well makes a hard drop guaranteed to complete the only layer, same
            // technique as HardDrop_DispatchesToClearBehaviour_WithTheClearedCellsPositionsAndColors
            // above. PieceController still publishes LayersClearedEvent itself (for scoring/
            // BlockoutTimeDifficultyModifier), so that part of the wiring is unchanged - only how
            // this test triggers it needed to catch up.
            var cellRenderer = CreateCellRenderer();
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(new VoxelGrid(1, 5, 1), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 5, 1, null, null);

            eventBus.Publish(new HardDropRequestedEvent()); // locks, clears the only layer, reduces run 1's session interval

            // PieceController.CurrentInterval is a snapshot taken at Initialize() and only
            // refreshed by Tick()/AdvancePhase() - piece 2 was already spawned (inside the
            // Locked?.Invoke() call chain) BEFORE PieceController.Lock() goes on to publish
            // LayersClearedEvent, so its snapshot predates the reduction. A real frame's Update()
            // would refresh it the same way; Tick(0f) does the same synchronously, without
            // advancing any fall-state timer (deltaTime=0), since this is a [Test] with no frame
            // boundary to wait for.
            spawner.CurrentPiece.Tick(0f);

            float phaseInterval = _fallCurve.IntervalForPhase(0);
            Assert.Less(spawner.CurrentPiece.CurrentInterval, phaseInterval); // the reduction reached this piece

            spawner.Initialize(new VoxelGrid(1, 5, 1), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 5, 1, null, null);

            Assert.AreEqual(phaseInterval, spawner.CurrentPiece.CurrentInterval, 0.0001f); // back to ratio 1.0

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }
    }
}
