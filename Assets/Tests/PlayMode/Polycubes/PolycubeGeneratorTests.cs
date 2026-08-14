using NUnit.Framework;
using System.Linq;
using UnityEngine;
using hp55games.Polycubes.Shapes;

namespace hp55games.Polycubes.Tests
{
    public class PolycubeGeneratorTests
    {
        [Test]
        public void GenerateAllConnected_OneCell_ReturnsSingleShape()
        {
            var shapes = PolycubeGenerator.GenerateAllConnected(1);

            Assert.AreEqual(1, shapes.Count);
            Assert.AreEqual(1, shapes[0].Cells.Count);
        }

        [Test]
        public void GenerateAllConnected_FourCells_ReturnsEightTetracubes()
        {
            var shapes = PolycubeGenerator.GenerateAllConnected(4);

            Assert.AreEqual(8, shapes.Count);
            Assert.IsTrue(shapes.All(s => s.Cells.Count == 4));
        }

        [Test]
        public void GenerateAllConnected_ShapesAreDistinctUnderRotation()
        {
            var shapes = PolycubeGenerator.GenerateAllConnected(4);

            var canonicalKeys = shapes
                .Select(CanonicalKeyAcrossAllOrientations)
                .Distinct()
                .Count();

            Assert.AreEqual(shapes.Count, canonicalKeys);
        }

        [Test]
        public void GenerateAllConnected_EveryShapeIsOrthogonallyConnected()
        {
            var shapes = PolycubeGenerator.GenerateAllConnected(5);

            foreach (var shape in shapes)
            {
                Assert.IsTrue(IsOrthogonallyConnected(shape), $"Disconnected shape found: {PolycubeGenerator.NormalizedCellKey(shape)}");
            }
        }

        [Test]
        public void GenerateAllConnected_FiveCells_ReturnsTwentyNinePentacubes()
        {
            // 29 matches the known count of connected polycubes of 5 cells under rotation
            // (cross-checked independently before trusting it as the Phase 1 reference value).
            var pentacubes = PolycubeGenerator.GenerateAllConnected(5);

            Assert.AreEqual(29, pentacubes.Count);
            Assert.IsTrue(pentacubes.All(s => s.Cells.Count == 5));
        }

        private static bool IsOrthogonallyConnected(PolycubeShape shape)
        {
            var cells = new System.Collections.Generic.HashSet<Vector3Int>(shape.Cells);
            var start = shape.Cells[0];
            var visited = new System.Collections.Generic.HashSet<Vector3Int> { start };
            var queue = new System.Collections.Generic.Queue<Vector3Int>();
            queue.Enqueue(start);

            Vector3Int[] offsets =
            {
                new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
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

        private static string CanonicalKeyAcrossAllOrientations(PolycubeShape shape)
        {
            string best = null;
            for (int rx = 0; rx < 4; rx++)
            for (int ry = 0; ry < 4; ry++)
            for (int rz = 0; rz < 4; rz++)
            {
                var key = PolycubeGenerator.NormalizedCellKey(shape.RotatedX(rx).RotatedY(ry).RotatedZ(rz));
                if (best == null || string.CompareOrdinal(key, best) < 0) best = key;
            }
            return best;
        }
    }
}
