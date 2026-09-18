using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Config;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Tests
{
    public class BlockoutShapeSetTests
    {
        // BlockoutShapeSelectionConfig's enabled overrides only apply if a real
        // IConfigCatalogService is registered - unregistered (the state before/after every test
        // in this file, since none of them call SetUp/Register) means ResolveEnabledByName()
        // finds nothing and every piece defaults to enabled, which is what the plain
        // BuildDefault()/AllPieceNames tests below rely on.
        private sealed class FakeConfigCatalogService : IConfigCatalogService
        {
            private readonly BlockoutShapeSelectionConfig _config;
            public FakeConfigCatalogService(BlockoutShapeSelectionConfig config) => _config = config;

            public T Get<T>() where T : ScriptableObject, IConfigAsset => throw new System.NotSupportedException();

            public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IConfigAsset
            {
                if (typeof(T) == typeof(BlockoutShapeSelectionConfig) && _config != null)
                    return (IReadOnlyList<T>)(object)new List<BlockoutShapeSelectionConfig> { _config };
                return new List<T>();
            }
        }

        private static BlockoutShapeSelectionConfig CreateConfig(params (string name, bool enabled)[] toggles)
        {
            var config = ScriptableObject.CreateInstance<BlockoutShapeSelectionConfig>();
            var list = new List<BlockoutShapeSelectionConfig.PieceToggle>();
            foreach (var (name, enabled) in toggles)
                list.Add(new BlockoutShapeSelectionConfig.PieceToggle { pieceName = name, enabled = enabled });

            typeof(BlockoutShapeSelectionConfig)
                .GetField("_pieces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(config, list);

            return config;
        }

        private IConfigCatalogService _registeredCatalog;
        private BlockoutShapeSelectionConfig _registeredConfig;

        private void RegisterCatalog(BlockoutShapeSelectionConfig config)
        {
            _registeredConfig = config;
            _registeredCatalog = new FakeConfigCatalogService(config);
            ServiceRegistry.Register(_registeredCatalog);
        }

        [TearDown]
        public void TearDown()
        {
            // Unregister (not Register(null) - see ServiceRegistry.Unregister's own doc for why
            // that would be unsafe) so the next test/file in the same domain doesn't inherit this
            // fake - none of the tests below that don't call RegisterCatalog rely on that.
            if (_registeredCatalog != null)
                ServiceRegistry.Unregister(_registeredCatalog);

            if (_registeredConfig != null)
                Object.DestroyImmediate(_registeredConfig);
        }

        [Test]
        public void BuildDefault_ReturnsExactlyTwelveShapes()
        {
            var shapes = BlockoutShapeSet.BuildDefault();
            Assert.AreEqual(12, shapes.Count);
        }

        [Test]
        public void AllPieceNames_HasTwelveUniqueNames()
        {
            Assert.AreEqual(12, BlockoutShapeSet.AllPieceNames.Count);
            CollectionAssert.AllItemsAreUnique(BlockoutShapeSet.AllPieceNames);
        }

        [Test]
        public void GetPieceEntries_AllEnabledByDefault_WhenNoConfigIsRegistered()
        {
            var entries = BlockoutShapeSet.GetPieceEntries();

            Assert.AreEqual(12, entries.Count);
            Assert.IsTrue(entries.All(e => e.Enabled));
        }

        [Test]
        public void BuildDefault_ExcludesAPieceDisabledInConfig()
        {
            var disabledName = BlockoutShapeSet.AllPieceNames[0];
            RegisterCatalog(CreateConfig((disabledName, false)));

            var shapes = BlockoutShapeSet.BuildDefault();

            Assert.AreEqual(11, shapes.Count);
            CollectionAssert.DoesNotContain(shapes.Select(s => s.Name).ToList(), disabledName);
        }

        [Test]
        public void GetPieceEntries_ReflectsConfigOverride_ForTheListedPieceOnly()
        {
            var disabledName = BlockoutShapeSet.AllPieceNames[0];
            RegisterCatalog(CreateConfig((disabledName, false)));

            var entries = BlockoutShapeSet.GetPieceEntries();

            Assert.IsFalse(entries.Single(e => e.Name == disabledName).Enabled);
            Assert.AreEqual(11, entries.Count(e => e.Enabled));
        }

        [Test]
        public void BuildDefault_FallsBackToAllTwelve_WhenConfigDisablesEveryPiece()
        {
            var allDisabled = BlockoutShapeSet.AllPieceNames.Select(n => (n, false)).ToArray();
            RegisterCatalog(CreateConfig(allDisabled));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("(?i)disables every piece"));
            var shapes = BlockoutShapeSet.BuildDefault();

            Assert.AreEqual(12, shapes.Count);
        }

        [Test]
        public void BuildDefault_ContainsEightTetracubesAndFourPentacubes()
        {
            var shapes = BlockoutShapeSet.BuildDefault();

            Assert.AreEqual(8, shapes.Count(s => s.Cells.Count == 4));
            Assert.AreEqual(4, shapes.Count(s => s.Cells.Count == 5));
        }

        [Test]
        public void BuildDefault_NoShapeHasAnIsolatedOrDiagonalOnlyCell()
        {
            // PolycubeGenerator only ever grows shapes via face-adjacent (orthogonal) neighbors,
            // so an isolated/diagonal-only cell is structurally impossible here — assert connectivity holds.
            var shapes = BlockoutShapeSet.BuildDefault();

            foreach (var shape in shapes)
            {
                Assert.IsTrue(IsOrthogonallyConnected(shape));
            }
        }

        private static bool IsOrthogonallyConnected(hp55games.Polycubes.Shapes.PolycubeShape shape)
        {
            var cells = new System.Collections.Generic.HashSet<UnityEngine.Vector3Int>(shape.Cells);
            var start = shape.Cells[0];
            var visited = new System.Collections.Generic.HashSet<UnityEngine.Vector3Int> { start };
            var queue = new System.Collections.Generic.Queue<UnityEngine.Vector3Int>();
            queue.Enqueue(start);

            UnityEngine.Vector3Int[] offsets =
            {
                new UnityEngine.Vector3Int(1, 0, 0), new UnityEngine.Vector3Int(-1, 0, 0),
                new UnityEngine.Vector3Int(0, 1, 0), new UnityEngine.Vector3Int(0, -1, 0),
                new UnityEngine.Vector3Int(0, 0, 1), new UnityEngine.Vector3Int(0, 0, -1),
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var offset in offsets)
                {
                    var neighbor = current + offset;
                    if (cells.Contains(neighbor) && visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited.Count == cells.Count;
        }
    }
}
