using NUnit.Framework;
using System.Linq;
using hp55games.Blockout;

namespace hp55games.Blockout.Tests
{
    public class BlockoutShapeSetTests
    {
        [Test]
        public void BuildDefault_ReturnsExactlyTwelveShapes()
        {
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
