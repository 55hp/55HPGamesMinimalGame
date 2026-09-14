using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Tests
{
    public class JuicyClearBehaviourTests
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private JuicyClearBehaviour _behaviour;
        private PooledObject _prefab;
        private readonly List<GameObject> _spawnedTracker = new();

        [SetUp]
        public void SetUp()
        {
            ServiceRegistry.Register<IObjectPoolService>(new ObjectPoolService());
            _behaviour = ScriptableObject.CreateInstance<JuicyClearBehaviour>();
            _prefab = CreateTestPrefab();
            SetPrefabField(_behaviour, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_behaviour);
            Object.DestroyImmediate(_prefab.gameObject);
        }

        private static PooledObject CreateTestPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.AddComponent<Rigidbody>();
            return go.AddComponent<PooledObject>();
        }

        // _physicsObjectPrefab has no test-facing setter (same reasoning as BlockoutSkin's
        // private fields - see BlockoutSkinServiceTests.CreateSkin) since Franci always assigns
        // it in the Inspector in practice.
        private static void SetPrefabField(JuicyClearBehaviour behaviour, PooledObject prefab)
        {
            typeof(JuicyClearBehaviour)
                .GetField("_physicsObjectPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(behaviour, prefab);
        }

        private static BlockoutClearContext SingleCellContext(Vector3Int position, Color color)
        {
            return new BlockoutClearContext(new[] { position }, new[] { color }, layerCount: 1);
        }

        [Test]
        public void OnLayersCleared_SpawnsOnePhysicsObjectPerClearedCell_AtItsPosition()
        {
            var positions = new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0) };
            var colors = new[] { Color.red, Color.green, Color.blue };
            var context = new BlockoutClearContext(positions, colors, layerCount: 1);

            _behaviour.OnLayersCleared(context);

            var spawned = FindAllSpawnedInstances();
            var spawnedPositions = new List<Vector3Int>();
            foreach (var instance in spawned)
            {
                spawnedPositions.Add(Vector3Int.RoundToInt(instance.transform.position));
                _spawnedTracker.Add(instance.gameObject);
            }

            Assert.AreEqual(3, spawnedPositions.Count);
            CollectionAssert.Contains(spawnedPositions, positions[0]);
            CollectionAssert.Contains(spawnedPositions, positions[1]);
            CollectionAssert.Contains(spawnedPositions, positions[2]);

            foreach (var go in _spawnedTracker) Object.DestroyImmediate(go);
        }

        [Test]
        public void OnLayersCleared_ColorsTheSpawnedObject_ToMatchTheClearedCellColor()
        {
            var context = SingleCellContext(new Vector3Int(0, 0, 0), Color.magenta);

            _behaviour.OnLayersCleared(context);

            var spawned = FindSpawnedInstance();
            var renderer = spawned.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            Assert.AreEqual(Color.magenta, block.GetColor(BaseColorId));

            Object.DestroyImmediate(spawned.gameObject);
        }

        [Test]
        public void OnLayersCleared_DisablesGravity_AndLaunchesUpward()
        {
            var context = SingleCellContext(new Vector3Int(0, 0, 0), Color.white);

            _behaviour.OnLayersCleared(context);

            var spawned = FindSpawnedInstance();
            var rb = spawned.GetComponent<Rigidbody>();

            Assert.IsFalse(rb.useGravity); // "inverted gravity" - real gravity must not also apply
            Assert.Greater(rb.velocity.y, 0f); // launched upward, not left at rest

            Object.DestroyImmediate(spawned.gameObject);
        }

        [Test]
        public void OnLayersCleared_LogsErrorAndSpawnsNothing_WhenPrefabIsUnassigned()
        {
            SetPrefabField(_behaviour, null);
            var context = SingleCellContext(new Vector3Int(0, 0, 0), Color.white);

            LogAssert.Expect(LogType.Error, new Regex("(?i)_physicsObjectPrefab"));
            _behaviour.OnLayersCleared(context);

            Assert.IsNull(FindSpawnedInstance());
        }

        // Filters by OriginPrefab (set by ObjectPoolService.Get on every instance it creates)
        // rather than just "isn't _prefab" - keeps this immune to an unrelated PooledObject left
        // behind by another test fixture sharing the same scene, not only to this prefab's own
        // clones.
        private List<PooledObject> FindAllSpawnedInstances()
        {
            var result = new List<PooledObject>();
            foreach (var instance in Object.FindObjectsOfType<PooledObject>(includeInactive: true))
            {
                if (instance.OriginPrefab == _prefab.gameObject) result.Add(instance);
            }
            return result;
        }

        private PooledObject FindSpawnedInstance()
        {
            var all = FindAllSpawnedInstances();
            return all.Count > 0 ? all[0] : null;
        }
    }
}
