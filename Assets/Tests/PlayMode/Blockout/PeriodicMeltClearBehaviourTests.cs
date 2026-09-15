using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Config;
using hp55games.Mobile.Core.Pooling;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Tests
{
    public class PeriodicMeltClearBehaviourTests
    {
        private const int PerLayerSpherules = 24;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private PeriodicMeltClearBehaviour _behaviour;
        private PooledObject _prefab;
        private BlockoutWellConfig _wellConfig;

        private sealed class FakeConfigCatalogService : IConfigCatalogService
        {
            private readonly BlockoutWellConfig _wellConfig;
            public FakeConfigCatalogService(BlockoutWellConfig wellConfig) => _wellConfig = wellConfig;

            public T Get<T>() where T : ScriptableObject, IConfigAsset =>
                typeof(T) == typeof(BlockoutWellConfig) ? (T)(object)_wellConfig : null;

            public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IConfigAsset => new List<T>();
        }

        // Minimal stand-in for the real BlockoutSkinService - this behaviour only ever reads
        // ActiveSkin.DensityNormalized off it, so every other member just needs to compile.
        private sealed class FakeSkinService : IBlockoutSkinService
        {
            public BlockoutSkin ActiveSkin { get; set; }
            public bool IsUnlocked(BlockoutSkin skin) => true;
            public void SetActiveSkin(string skinId) { }
            public UnlockSkinResult TryUnlockSkin(string skinId) => UnlockSkinResult.Success;
            public void GrantUnlock(string skinId) { }

            public IReadOnlyList<BlockoutSkinShopEntry> GetElementShopEntries() => new List<BlockoutSkinShopEntry>();
        }

        [SetUp]
        public void SetUp()
        {
            ServiceRegistry.Register<IObjectPoolService>(new ObjectPoolService());

            _wellConfig = ScriptableObject.CreateInstance<BlockoutWellConfig>(); // defaults: 5 x 10 x 5
            ServiceRegistry.Register<IConfigCatalogService>(new FakeConfigCatalogService(_wellConfig));
            ServiceRegistry.Register<IBlockoutSkinService>(new FakeSkinService { ActiveSkin = CreateSkinWithDensity(0.5f) });

            _behaviour = ScriptableObject.CreateInstance<PeriodicMeltClearBehaviour>();
            _prefab = CreateTestPrefab();
            SetPrefabField(_behaviour, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            // Spawned instances must be cleared before the prefab itself is destroyed -
            // FindAllSpawnedInstances compares against _prefab.gameObject.
            foreach (var instance in FindAllSpawnedInstances()) Object.DestroyImmediate(instance.gameObject);

            Object.DestroyImmediate(_behaviour);
            Object.DestroyImmediate(_prefab.gameObject);
            Object.DestroyImmediate(_wellConfig);
        }

        private static PooledObject CreateTestPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.AddComponent<Rigidbody>();
            return go.AddComponent<PooledObject>();
        }

        // BlockoutSkin's fields are private with no test-facing setter - same reflection
        // approach as BlockoutSkinServiceTests.CreateSkin.
        private static BlockoutSkin CreateSkinWithDensity(float densityNormalized)
        {
            var skin = ScriptableObject.CreateInstance<BlockoutSkin>();
            typeof(BlockoutSkin).GetField("_densityNormalized", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(skin, densityNormalized);
            return skin;
        }

        private static void SetPrefabField(PeriodicMeltClearBehaviour behaviour, PooledObject prefab)
        {
            typeof(PeriodicMeltClearBehaviour)
                .GetField("_spherulePrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(behaviour, prefab);
        }

        private static BlockoutClearContext SingleLayerContext(int y, int cellCount = 3)
        {
            var positions = new List<Vector3Int>();
            var colors = new List<Color>();
            for (int i = 0; i < cellCount; i++)
            {
                positions.Add(new Vector3Int(i, y, 0));
                colors.Add(Color.magenta);
            }
            return new BlockoutClearContext(positions, colors, layerCount: 1);
        }

        [Test]
        public void OnLayersCleared_Spawns24Spherules_PerClearedLayer()
        {
            _behaviour.OnLayersCleared(SingleLayerContext(y: 2));

            Assert.AreEqual(PerLayerSpherules, FindAllSpawnedInstances().Count);
        }

        [Test]
        public void OnLayersCleared_Spawns24SpherulesPerLayer_ForEachDistinctClearedLayer()
        {
            var positions = new List<Vector3Int> { new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0) };
            var colors = new List<Color> { Color.white, Color.white };
            var context = new BlockoutClearContext(positions, colors, layerCount: 2);

            _behaviour.OnLayersCleared(context);

            Assert.AreEqual(PerLayerSpherules * 2, FindAllSpawnedInstances().Count);
        }

        [Test]
        public void OnLayersCleared_PositionsSpherules_OnTheWellPerimeter_AtTheClearedLayersHeight()
        {
            _behaviour.OnLayersCleared(SingleLayerContext(y: 3));

            foreach (var instance in FindAllSpawnedInstances())
            {
                var pos = instance.transform.position;
                Assert.AreEqual(3f, pos.y, 0.001f);

                bool onXWall = Mathf.Approximately(pos.x, -0.5f) || Mathf.Approximately(pos.x, 4.5f);
                bool onZWall = Mathf.Approximately(pos.z, -0.5f) || Mathf.Approximately(pos.z, 4.5f);
                Assert.IsTrue(onXWall || onZWall, $"Spherule at {pos} is not on the well perimeter.");
            }
        }

        [Test]
        public void OnLayersCleared_ColorsSpherules_ToMatchTheClearedCellColor()
        {
            _behaviour.OnLayersCleared(SingleLayerContext(y: 0));

            var block = new MaterialPropertyBlock();
            FindAllSpawnedInstances()[0].GetComponent<Renderer>().GetPropertyBlock(block);

            Assert.AreEqual(Color.magenta, block.GetColor(BaseColorId));
        }

        [Test]
        public void OnLayersCleared_LaunchesSpherulesUpward_WhenTheActiveElementIsLight()
        {
            ServiceRegistry.Register<IBlockoutSkinService>(new FakeSkinService { ActiveSkin = CreateSkinWithDensity(0f) });

            _behaviour.OnLayersCleared(SingleLayerContext(y: 0));

            foreach (var instance in FindAllSpawnedInstances())
                Assert.Greater(instance.GetComponent<Rigidbody>().velocity.y, 0f);
        }

        [Test]
        public void OnLayersCleared_LaunchesSpherulesDownward_WhenTheActiveElementIsDense()
        {
            ServiceRegistry.Register<IBlockoutSkinService>(new FakeSkinService { ActiveSkin = CreateSkinWithDensity(1f) });

            _behaviour.OnLayersCleared(SingleLayerContext(y: 0));

            foreach (var instance in FindAllSpawnedInstances())
                Assert.Less(instance.GetComponent<Rigidbody>().velocity.y, 0f);
        }

        [Test]
        public void OnLayersCleared_KeepsGravityOn_UnlikeJuicyClear()
        {
            _behaviour.OnLayersCleared(SingleLayerContext(y: 0));

            foreach (var instance in FindAllSpawnedInstances())
                Assert.IsTrue(instance.GetComponent<Rigidbody>().useGravity);
        }

        [Test]
        public void OnLayersCleared_LogsErrorAndSpawnsNothing_WhenPrefabIsUnassigned()
        {
            SetPrefabField(_behaviour, null);

            LogAssert.Expect(LogType.Error, new Regex("(?i)_spherulePrefab"));
            _behaviour.OnLayersCleared(SingleLayerContext(y: 0));

            Assert.AreEqual(0, FindAllSpawnedInstances().Count);
        }

        // Same OriginPrefab-filtering approach as JuicyClearBehaviourTests, for the same reason:
        // immune to an unrelated PooledObject left behind by another test fixture in the scene.
        private List<PooledObject> FindAllSpawnedInstances()
        {
            var result = new List<PooledObject>();
            foreach (var instance in Object.FindObjectsOfType<PooledObject>(includeInactive: true))
            {
                if (instance.OriginPrefab == _prefab.gameObject) result.Add(instance);
            }
            return result;
        }
    }
}
