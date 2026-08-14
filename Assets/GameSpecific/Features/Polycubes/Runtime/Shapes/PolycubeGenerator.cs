using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace hp55games.Polycubes.Shapes
{
    public static class PolycubeGenerator
    {
        private static readonly Vector3Int[] FaceOffsets =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        public static List<PolycubeShape> GenerateAllConnected(int cellCount)
        {
            if (cellCount <= 0) throw new ArgumentOutOfRangeException(nameof(cellCount));

            var shapes = new List<PolycubeShape> { new PolycubeShape(new[] { Vector3Int.zero }) };
            for (int size = 1; size < cellCount; size++)
            {
                shapes = GrowByOneCell(shapes);
            }
            return shapes;
        }

        // Growing every stored (canonical) representative by one face-adjacent cell and re-canonicalizing
        // is sufficient to reach every larger shape up to rotation: any connected shape has some cell whose
        // removal keeps it connected, and rotating that smaller shape onto the stored representative carries
        // the removed cell's position along with it.
        private static List<PolycubeShape> GrowByOneCell(List<PolycubeShape> shapes)
        {
            var seenCanonicalKeys = new HashSet<string>();
            var result = new List<PolycubeShape>();

            foreach (var shape in shapes)
            {
                var occupied = new HashSet<Vector3Int>(shape.Cells);
                foreach (var cell in shape.Cells)
                {
                    foreach (var offset in FaceOffsets)
                    {
                        var candidate = cell + offset;
                        if (occupied.Contains(candidate)) continue;

                        var grown = new PolycubeShape(shape.Cells.Append(candidate));
                        if (seenCanonicalKeys.Add(CanonicalKeyUnderRotation(grown)))
                        {
                            result.Add(grown);
                        }
                    }
                }
            }

            return result;
        }

        private static string CanonicalKeyUnderRotation(PolycubeShape shape)
        {
            string best = null;
            for (int rx = 0; rx < 4; rx++)
            {
                for (int ry = 0; ry < 4; ry++)
                {
                    for (int rz = 0; rz < 4; rz++)
                    {
                        var oriented = shape.RotatedX(rx).RotatedY(ry).RotatedZ(rz);
                        var key = NormalizedCellKey(oriented);
                        if (best == null || string.CompareOrdinal(key, best) < 0)
                        {
                            best = key;
                        }
                    }
                }
            }
            return best;
        }

        public static string NormalizedCellKey(PolycubeShape shape)
        {
            var cells = shape.Cells;
            int minX = cells.Min(c => c.x);
            int minY = cells.Min(c => c.y);
            int minZ = cells.Min(c => c.z);

            var ordered = cells
                .Select(c => new Vector3Int(c.x - minX, c.y - minY, c.z - minZ))
                .OrderBy(c => c.x).ThenBy(c => c.y).ThenBy(c => c.z);

            return string.Join(";", ordered.Select(c => $"{c.x},{c.y},{c.z}"));
        }
    }
}
