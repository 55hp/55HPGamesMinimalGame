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

        // Stand-in for a real skin's BaseColor for tests that actually check color content;
        // tests that don't care just pass Color.white.
        private static readonly Color TestPieceColor = Color.red;

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
            // Every storage cell occupied (headroom included - a piece's reference cell spawns at
            // Y = h - 1, and cells above the well are storable): simulates the stack having reached the spawn point.
            var grid = TestGrid.Make(2, 2, 2);
            TestGrid.Fill(grid);

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

            var tallShape = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0) });
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { tallShape }, 1, 2, 2, 2, Color.white, null);

            Assert.IsTrue(spawner.SpawnBlockedWellFull);
            Assert.IsNull(spawner.CurrentPiece); // detected and reported, not a piece silently locked in place
            Assert.IsTrue(wellFullFired); // BlockoutGameplayState listens for this to trigger game over

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Initialize_SpawnsPiece_WhenStartPositionIsFree()
        {
            var grid = TestGrid.Make(3, 3, 3);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            // This test is about the spawn-success logic, not rendering, so it deliberately
            // doesn't set up a WellCellRenderer - Initialize() logs its own error about that
            // (BlockoutSpawner.cs:103). (?i) inline flag, not RegexOptions.IgnoreCase - see the
            // comment in Initialize_DoesNotSpawnPiece_WhenComputedStartPositionIsAlreadyOccupied.
            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer"));

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 3, 3, 3, Color.white, null);

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
            spawner1.Initialize(TestGrid.Make(5, 10, 5), _fallCurve, _timeDifficultyConfig, shapes, 1, 5, 10, 5, TestPieceColor, null);
            var color1 = cellRenderer.GetCellColor(spawner1.CurrentPiece.GridPosition);

            // spawner2.Initialize below hides spawner1's piece from cellRenderer (HideAll) as a
            // side effect - color1 is already captured, so that's fine.
            var go2 = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner2 = go2.AddComponent<BlockoutSpawner>();
            spawner2.Initialize(TestGrid.Make(5, 10, 5), _fallCurve, _timeDifficultyConfig, shapes, 1, 5, 10, 5, TestPieceColor, null);
            var color2 = cellRenderer.GetCellColor(spawner2.CurrentPiece.GridPosition);

            Assert.AreEqual(color1, color2); // the skin's single base colour, every run

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        [Test]
        public void HardDrop_ShowsLockedCellAtFinalPosition_WithItsLevelColor()
        {
            var cellRenderer = CreateCellRenderer();
            var grid = TestGrid.Make(3, 5, 3);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var wellConfig = ScriptableObject.CreateInstance<BlockoutWellConfig>();
            typeof(BlockoutWellConfig).GetField("_levelColors", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(wellConfig, new[] { Color.cyan, Color.magenta });

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 3, 5, 3, TestPieceColor, null, null, wellConfig);

            var activeColor = cellRenderer.GetCellColor(spawner.CurrentPiece.GridPosition);

            eventBus.Publish(new HardDropRequestedEvent());

            // CenteredTopStart centers a single-cell shape at x=(3-1)/2=1, z=1; hard drop always
            // reaches the empty floor, y=0.
            var finalPos = new Vector3Int(1, 0, 1);
            Assert.IsTrue(cellRenderer.IsCellShown(finalPos));

            Assert.AreEqual(TestPieceColor, activeColor); // falling: the skin's base colour
            Assert.AreEqual(Color.cyan, cellRenderer.GetCellColor(finalPos)); // locked: level 0's colour

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
            Object.DestroyImmediate(wellConfig);
        }

        [Test]
        public void Initialize_ForwardsCellSurface_ToEveryShownCell()
        {
            // Periodic Table GDD: BlockoutGameplayState.StartSpawning derives this from the
            // active skin (null for a non-element skin) and passes it straight through. An
            // emissive surface is used so the forwarding is observable as a material swap.
            var cellRenderer = CreateCellRenderer();
            var emissive = new Material(Shader.Find("Sprites/Default"));
            typeof(WellCellRenderer).GetField("_emissiveMaterial", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cellRenderer, emissive);
            var grid = TestGrid.Make(3, 5, 3);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 3, 5, 3, TestPieceColor, null, new CellSurface(0f, 0.5f, Color.red));

            var shownAt = spawner.CurrentPiece.GridPosition;
            var shownCells = (Dictionary<Vector3Int, PooledObject>)typeof(WellCellRenderer)
                .GetField("_shownCells", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(cellRenderer);
            var actualMaterial = shownCells[shownAt].GetComponent<Renderer>().sharedMaterial;

            Assert.AreEqual(emissive, actualMaterial);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
            Object.DestroyImmediate(emissive);
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
            var grid = TestGrid.Make(7, 7, 7);
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
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { shape }, 1, 7, 7, 7, Color.white, null);

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

            spawner.Initialize(TestGrid.Make(3, 3, 3), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 3, 3, 3, Color.white, null);

            var spawnPos = spawner.CurrentPiece.GridPosition;
            eventBus.Publish(new HardDropRequestedEvent()); // locks the first piece, spawns a second
            var lockedPos = new Vector3Int(spawnPos.x, 0, spawnPos.z); // hard drop always reaches the floor
            Assert.IsTrue(cellRenderer.IsCellShown(lockedPos)); // still visible - "run 1"'s locked placeholder

            // Starting a new run should sweep away the previous one's locked cells.
            spawner.Initialize(TestGrid.Make(3, 3, 3), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 3, 3, 3, Color.white, null);

            Assert.IsFalse(cellRenderer.IsCellShown(lockedPos));

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        // ---- Game over: judged once, after the lock AND any clear/collapse, on locked cells at
        // Y >= h (outside the well). The active piece is never blocked by the ceiling (see PieceControllerTests).

        private static PolycubeShape Vertical(int length)
        {
            var cells = new Vector3Int[length];
            for (int i = 0; i < length; i++) cells[i] = new Vector3Int(0, i, 0);
            return new PolycubeShape(cells);
        }

        private static PolycubeShape Line5() => new PolycubeShape(new[]
        {
            Vector3Int.zero, new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0), new Vector3Int(3, 0, 0), new Vector3Int(4, 0, 0),
        });

        // Spawns `shape` (the only shape in the set) into a width x 3 x 1 well (h = 3); the piece's
        // reference cell starts at Y = 2 (h - 1, the top row). wellFullFired[0] flips when WellFull is raised.
        private BlockoutSpawner SpawnInto(VoxelGrid grid, int width, PolycubeShape shape, out EventBus eventBus, out bool[] wellFullFired)
        {
            eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            var fired = new bool[1];
            spawner.WellFull += () => fired[0] = true;
            wellFullFired = fired;

            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer")); // no renderer needed here
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { shape }, 1, width, 3, 1, Color.white, null);
            Assert.IsNotNull(spawner.CurrentPiece);
            Assert.AreEqual(2, spawner.CurrentPiece.GridPosition.y); // reference cell spawns at Y = h - 1
            return spawner;
        }

        // Occupies every x of layerY except exceptX, so a piece filling that x completes the layer.
        private static void FillLayerExcept(VoxelGrid grid, int layerY, int exceptX)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (x != exceptX) grid.SetOccupied(x, layerY, 0, true);
            }
        }

        [Test]
        public void GameOver_LockFullyBelowH_IsNotGameOver()
        {
            var grid = TestGrid.Make(3, 3, 1);
            var spawner = SpawnInto(grid, 3, SingleCellShape(), out var eventBus, out var wellFullFired);

            eventBus.Publish(new HardDropRequestedEvent()); // falls to the floor

            Assert.IsFalse(wellFullFired[0]);
            Assert.IsNotNull(spawner.CurrentPiece);
            Assert.IsFalse(grid.AnyOccupiedAtOrAbove(1));

            Object.DestroyImmediate(spawner.gameObject);
        }

        [Test]
        public void GameOver_LockInTheTopRow_IsNotGameOver()
        {
            var grid = TestGrid.Make(3, 3, 1);
            var spawner = SpawnInto(grid, 3, SingleCellShape(), out var eventBus, out var wellFullFired);
            grid.SetOccupied(1, 1, 0, true); // stack right below the spawn cell: locks in the top row (Y = 2)

            eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsTrue(grid.IsOccupied(1, 2, 0));
            Assert.IsFalse(wellFullFired[0]);

            Object.DestroyImmediate(spawner.gameObject);
        }

        [Test]
        public void GameOver_LockAboveH_ResolvedByClearAndCollapse_IsNotGameOver()
        {
            var grid = TestGrid.Make(3, 3, 1);
            var spawner = SpawnInto(grid, 3, Vertical(2), out var eventBus, out var wellFullFired); // piece at x = 1
            grid.SetOccupied(1, 1, 0, true);  // blocks the piece at Y = 2 (cells Y = 2, 3 - one outside the well)
            FillLayerExcept(grid, 2, 1);      // the piece completes layer 2...
            FillLayerExcept(grid, 3, 1);      // ...and layer 3 - both clear, nothing is left outside the well

            eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsFalse(grid.AnyOccupiedAtOrAbove(2));
            Assert.IsFalse(wellFullFired[0]);
            Assert.IsNotNull(spawner.CurrentPiece);

            Object.DestroyImmediate(spawner.gameObject);
        }

        [Test]
        public void GameOver_LockAboveH_WithNoClear_IsGameOver()
        {
            var grid = TestGrid.Make(3, 3, 1);
            var spawner = SpawnInto(grid, 3, Vertical(2), out var eventBus, out var wellFullFired);
            grid.SetOccupied(1, 1, 0, true); // locks with cells at Y = 2 and 3 (3 = h, outside the well)

            eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsTrue(wellFullFired[0]);
            Assert.IsTrue(spawner.SpawnBlockedWellFull);
            Assert.IsNull(spawner.CurrentPiece);

            Object.DestroyImmediate(spawner.gameObject);
        }

        [Test]
        public void GameOver_LockAboveH_WithInsufficientClear_IsGameOver()
        {
            var grid = TestGrid.Make(3, 3, 1);
            var spawner = SpawnInto(grid, 3, Vertical(3), out var eventBus, out var wellFullFired); // cells Y = 2, 3, 4
            grid.SetOccupied(1, 1, 0, true);
            FillLayerExcept(grid, 2, 1); // only layer 2 clears; the piece's other cells collapse to Y = 2, 3

            eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsTrue(grid.IsOccupied(1, 3, 0)); // still outside the well after the collapse
            Assert.IsTrue(wellFullFired[0]);
            Assert.IsNull(spawner.CurrentPiece);

            Object.DestroyImmediate(spawner.gameObject);
        }

        [Test]
        public void BlockoutShapeSet_IncludesTheStraightFiveCellPentacube()
        {
            bool found = false;
            foreach (var shape in BlockoutShapeSet.AllShapes())
            {
                if (shape.Cells.Count != 5) continue;
                var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
                var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                foreach (var c in shape.Cells)
                {
                    min = Vector3Int.Min(min, c);
                    max = Vector3Int.Max(max, c);
                }
                var span = max - min + Vector3Int.one;
                if (span.x * span.y * span.z == 5) found = true; // 5x1x1 in some orientation
            }
            Assert.IsTrue(found);
        }

        [Test]
        public void StraightPentacube_StoodUpAtTheCeiling_ClearingEnough_IsNotGameOver()
        {
            var grid = TestGrid.Make(5, 3, 1);
            var spawner = SpawnInto(grid, 5, Line5(), out var eventBus, out var wellFullFired);
            eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 }); // cells Y = 2..6
            grid.SetOccupied(0, 1, 0, true); // blocks it right there
            for (int y = 2; y <= 6; y++) FillLayerExcept(grid, y, 0); // the piece completes all five layers

            eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsFalse(grid.AnyOccupiedAtOrAbove(2));
            Assert.IsFalse(wellFullFired[0]);

            Object.DestroyImmediate(spawner.gameObject);
        }

        [Test]
        public void StraightPentacube_StoodUpAtTheCeiling_ClearingTooLittle_IsGameOver()
        {
            var grid = TestGrid.Make(5, 3, 1);
            var spawner = SpawnInto(grid, 5, Line5(), out var eventBus, out var wellFullFired);
            eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 }); // cells Y = 2..6
            grid.SetOccupied(0, 1, 0, true);
            FillLayerExcept(grid, 2, 0); // only the bottom layer completes; four cells collapse to Y = 2..5

            eventBus.Publish(new HardDropRequestedEvent());

            Assert.IsTrue(wellFullFired[0]);
            Assert.IsNull(spawner.CurrentPiece);

            Object.DestroyImmediate(spawner.gameObject);
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
            var grid = TestGrid.Make(1, 3, 1);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var behaviour = ScriptableObject.CreateInstance<RecordingClearBehaviour>();
            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 3, 1, Color.white, behaviour);

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
            var grid = TestGrid.Make(1, 3, 1);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 3, 1, Color.white, null);

            var spawnPos = spawner.CurrentPiece.GridPosition; // (0, 2, 0) - Y = h - 1, the top row

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
            // BlockoutTimeDifficultyModifier per run, so the step delay restarts at
            // StartStepDelay instead of carrying over the previous run's decay.
            var cellRenderer = CreateCellRenderer();

            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();
            spawner.Initialize(TestGrid.Make(1, 5, 1), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 5, 1, Color.white, null);

            // Reach the private modifier directly (same pattern as the WellCellRenderer field
            // access above) to force decay without waiting DecayIntervalSeconds of real time.
            var timeDifficultyField = typeof(BlockoutSpawner).GetField("_timeDifficulty", BindingFlags.NonPublic | BindingFlags.Instance);
            var modifier = (BlockoutTimeDifficultyModifier)timeDifficultyField.GetValue(spawner);
            modifier.Tick(_timeDifficultyConfig.DecayIntervalSeconds + 1f);

            spawner.CurrentPiece.Tick(0f); // refresh the snapshot without advancing any fall-state timer
            Assert.Less(spawner.CurrentPiece.CurrentInterval, _timeDifficultyConfig.StartStepDelay); // the decay reached this piece

            spawner.Initialize(TestGrid.Make(1, 5, 1), _fallCurve, _timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 5, 1, Color.white, null);

            Assert.AreEqual(_timeDifficultyConfig.StartStepDelay, spawner.CurrentPiece.CurrentInterval, 0.0001f); // back to the start delay

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cellRenderer.gameObject);
        }

        [Test]
        public void SpawnOrder_IsDeterministic_ForTheSameSeed()
        {
            var shapes = new List<PolycubeShape>
            {
                SingleCellShape().WithName("A"),
                SingleCellShape().WithName("B"),
                SingleCellShape().WithName("C"),
            };
            const int seed = 12345;

            var sequence1 = RecordSpawnSequence(shapes, seed, spawnCount: 20);
            var sequence2 = RecordSpawnSequence(shapes, seed, spawnCount: 20);

            CollectionAssert.AreEqual(sequence1, sequence2);
        }

        [Test]
        public void SpawnOrder_NeverRepeatsThePieceConsecutively_WithMultipleShapesEnabled()
        {
            var shapes = new List<PolycubeShape>
            {
                SingleCellShape().WithName("A"),
                SingleCellShape().WithName("B"),
                SingleCellShape().WithName("C"),
            };

            var sequence = RecordSpawnSequence(shapes, seed: 999, spawnCount: 40);

            for (int i = 1; i < sequence.Count; i++)
            {
                Assert.AreNotEqual(sequence[i - 1], sequence[i], $"Piece repeated back-to-back at index {i}.");
            }
        }

        [Test]
        public void SpawnOrder_AlwaysSpawnsTheOnlyShape_WhenOnlyOneIsEnabled()
        {
            var shapes = new List<PolycubeShape> { SingleCellShape().WithName("Solo") };

            var sequence = RecordSpawnSequence(shapes, seed: 7, spawnCount: 15);

            Assert.IsTrue(sequence.TrueForAll(name => name == "Solo"));
        }

        // Drives spawnCount spawns via repeated hard drops and records which shape (by Name) was
        // active at each step. All test shapes here are a single cell, so CenteredTopStart always
        // starts them at the same X/Z - a 3x3 base is wide enough that stacking spawnCount of them
        // straight down one column never completes a full layer (9 cells needed, only 1 ever
        // occupied), so no layer clear or WellFull ever interferes with the recorded sequence.
        private List<string> RecordSpawnSequence(IReadOnlyList<PolycubeShape> shapes, int seed, int spawnCount)
        {
            var go = new GameObject(nameof(BlockoutSpawnerTests));
            var spawner = go.AddComponent<BlockoutSpawner>();

            var grid = TestGrid.Make(3, spawnCount + 5, 3);
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);

            spawner.Initialize(grid, _fallCurve, _timeDifficultyConfig, shapes, seed, 3, spawnCount + 5, 3, Color.white, null);

            var sequence = new List<string>();
            for (int i = 0; i < spawnCount; i++)
            {
                Assert.IsNotNull(spawner.CurrentPiece, $"Spawning stopped early at spawn {i} (well full?).");
                sequence.Add(spawner.CurrentPiece.Shape.Name);
                eventBus.Publish(new HardDropRequestedEvent()); // locks, spawns the next piece synchronously
            }

            Object.DestroyImmediate(go);
            return sequence;
        }
    }
}
