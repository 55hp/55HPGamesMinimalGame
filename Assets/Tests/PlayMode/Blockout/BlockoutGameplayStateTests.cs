using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Context;
using hp55games.Mobile.Core.Save;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;
using hp55games.Blockout.Gameplay.Events;
using hp55games.Polycubes.Grid;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout.Tests
{
    public class BlockoutGameplayStateTests
    {
        private sealed class FakeSaveService : ISaveService
        {
            // coins: 0, not SaveData's own default (1000, a fresh-save starting balance) - these
            // tests assert the exact coins AwardCoins earns, independent of whatever starting
            // balance production code happens to default to.
            public SaveData Data { get; } = new SaveData { coins = 0 };
            public int SaveCallCount { get; private set; }
            public void Load() { }
            public void Save() => SaveCallCount++;
        }

        private static PolycubeShape SingleCellShape() => new PolycubeShape(new[] { Vector3Int.zero });

        // BlockoutGameplayState.EnterAsync returns Task, which Unity's Test Framework [Test]
        // attribute doesn't support (only [UnityTest]/IEnumerator) - waits for it frame-by-frame
        // instead. With isResuming: true the awaited branch is skipped entirely (see call sites
        // below) so this completes within a frame or two, not a real async wait.
        private static IEnumerator WaitFor(System.Threading.Tasks.Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception;
        }

        [UnityTest]
        public IEnumerator OnWellFull_AwardsCoins_BaseFromFinalScorePlusBonusFromMultiLayerClears()
        {
            // Technical Doc Phase 6 formula: floor(finalScore / 100) + 5*N per multi-layer
            // (N >= 2) clear event during the run. Here: a 3-layer clear (900 pts, +15 bonus) and
            // a 1-layer clear (100 pts, no bonus) -> finalScore=1000, coins = 10 + 15 = 25.
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var context = new GameContextService();
            ServiceRegistry.Register<IGameContextService>(context);
            var saveService = new FakeSaveService();
            ServiceRegistry.Register<ISaveService>(saveService);

            var spawnerGo = new GameObject(nameof(BlockoutGameplayStateTests));
            var spawner = spawnerGo.AddComponent<BlockoutSpawner>();

            // isResuming: true skips EnterAsync's first-entry branch (music/navigation/
            // StartSpawning, none of which are set up here) while still wiring the
            // LayersClearedEvent subscription and spawner.WellFull handler this test needs.
            var state = new BlockoutGameplayState(isResuming: true);
            yield return WaitFor(state.EnterAsync(CancellationToken.None));

            eventBus.Publish(new LayersClearedEvent { LayerCount = 3, PointsAwarded = 900 });
            eventBus.Publish(new LayersClearedEvent { LayerCount = 1, PointsAwarded = 100 });

            // Triggers spawner.WellFull the same way BlockoutSpawner itself would - through a
            // real Initialize() call against an already-full grid - rather than reaching for the
            // event directly (WellFull's invocation is restricted to BlockoutSpawner itself).
            var fallCurve = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();
            var timeDifficultyConfig = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
            var grid = TestGrid.Make(1, 1, 1);
            TestGrid.Fill(grid); // whole storage - the spawn point (Y = h) included

            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer"));
            LogAssert.Expect(LogType.Error, new Regex("(?i)well is full"));
            spawner.Initialize(grid, fallCurve, timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 1, 1, null, null);

            Assert.AreEqual(1000, context.Score); // 900 + 100
            Assert.AreEqual(1, saveService.SaveCallCount);
            Assert.AreEqual(25, saveService.Data.coins);
            Assert.AreEqual(25, saveService.Data.progress.lifetimeCoins);

            Object.DestroyImmediate(spawnerGo);
            Object.DestroyImmediate(fallCurve);
            Object.DestroyImmediate(timeDifficultyConfig);
        }

        [UnityTest]
        public IEnumerator OnWellFull_AwardsNoBonus_WhenEveryClearWasSingleLayer()
        {
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var context = new GameContextService();
            ServiceRegistry.Register<IGameContextService>(context);
            var saveService = new FakeSaveService();
            ServiceRegistry.Register<ISaveService>(saveService);

            var spawnerGo = new GameObject(nameof(BlockoutGameplayStateTests));
            var spawner = spawnerGo.AddComponent<BlockoutSpawner>();

            var state = new BlockoutGameplayState(isResuming: true);
            yield return WaitFor(state.EnterAsync(CancellationToken.None));

            eventBus.Publish(new LayersClearedEvent { LayerCount = 1, PointsAwarded = 100 });

            var fallCurve = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();
            var timeDifficultyConfig = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
            var grid = TestGrid.Make(1, 1, 1);
            TestGrid.Fill(grid); // whole storage - the spawn point (Y = h) included

            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer"));
            LogAssert.Expect(LogType.Error, new Regex("(?i)well is full"));
            spawner.Initialize(grid, fallCurve, timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 1, 1, null, null);

            Assert.AreEqual(1, saveService.Data.coins); // floor(100/100) + 0 bonus

            Object.DestroyImmediate(spawnerGo);
            Object.DestroyImmediate(fallCurve);
            Object.DestroyImmediate(timeDifficultyConfig);
        }

        [UnityTest]
        public IEnumerator OnWellFull_FloorsTheBaseTerm_WhenFinalScoreIsNotAMultipleOf100()
        {
            // A real clear's PointsAwarded (100*N^2) is always a multiple of 100, so this
            // publishes a synthetic score directly to exercise AwardCoins' floor(finalScore/100)
            // in isolation - LayerCount=1 keeps the bonus term at 0 so it can't mask the result.
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var context = new GameContextService();
            ServiceRegistry.Register<IGameContextService>(context);
            var saveService = new FakeSaveService();
            ServiceRegistry.Register<ISaveService>(saveService);

            var spawnerGo = new GameObject(nameof(BlockoutGameplayStateTests));
            var spawner = spawnerGo.AddComponent<BlockoutSpawner>();

            var state = new BlockoutGameplayState(isResuming: true);
            yield return WaitFor(state.EnterAsync(CancellationToken.None));

            eventBus.Publish(new LayersClearedEvent { LayerCount = 1, PointsAwarded = 250 });

            var fallCurve = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();
            var timeDifficultyConfig = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
            var grid = TestGrid.Make(1, 1, 1);
            TestGrid.Fill(grid); // whole storage - the spawn point (Y = h) included

            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer"));
            LogAssert.Expect(LogType.Error, new Regex("(?i)well is full"));
            spawner.Initialize(grid, fallCurve, timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 1, 1, null, null);

            Assert.AreEqual(250, context.Score);
            Assert.AreEqual(2, saveService.Data.coins); // floor(2.5) = 2, not 3

            Object.DestroyImmediate(spawnerGo);
            Object.DestroyImmediate(fallCurve);
            Object.DestroyImmediate(timeDifficultyConfig);
        }

        [UnityTest]
        public IEnumerator OnWellFull_SumsTheBonus_AcrossMultipleMultiLayerClearsInTheSameRun()
        {
            var eventBus = new EventBus();
            ServiceRegistry.Register<IEventBus>(eventBus);
            var context = new GameContextService();
            ServiceRegistry.Register<IGameContextService>(context);
            var saveService = new FakeSaveService();
            ServiceRegistry.Register<ISaveService>(saveService);

            var spawnerGo = new GameObject(nameof(BlockoutGameplayStateTests));
            var spawner = spawnerGo.AddComponent<BlockoutSpawner>();

            var state = new BlockoutGameplayState(isResuming: true);
            yield return WaitFor(state.EnterAsync(CancellationToken.None));

            eventBus.Publish(new LayersClearedEvent { LayerCount = 2, PointsAwarded = 400 }); // +10 bonus
            eventBus.Publish(new LayersClearedEvent { LayerCount = 3, PointsAwarded = 900 }); // +15 bonus

            var fallCurve = ScriptableObject.CreateInstance<BlockoutFallCurveConfig>();
            var timeDifficultyConfig = ScriptableObject.CreateInstance<BlockoutTimeDifficultyConfig>();
            var grid = TestGrid.Make(1, 1, 1);
            TestGrid.Fill(grid); // whole storage - the spawn point (Y = h) included

            LogAssert.Expect(LogType.Error, new Regex("(?i)WellCellRenderer"));
            LogAssert.Expect(LogType.Error, new Regex("(?i)well is full"));
            spawner.Initialize(grid, fallCurve, timeDifficultyConfig, new List<PolycubeShape> { SingleCellShape() }, 1, 1, 1, 1, null, null);

            Assert.AreEqual(1300, context.Score); // 400 + 900
            Assert.AreEqual(38, saveService.Data.coins); // floor(1300/100)=13 base + (10+15)=25 bonus

            Object.DestroyImmediate(spawnerGo);
            Object.DestroyImmediate(fallCurve);
            Object.DestroyImmediate(timeDifficultyConfig);
        }
    }
}
