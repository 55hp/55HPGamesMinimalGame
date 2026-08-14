using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace hp55games.Polycubes.Shapes
{
    public sealed class PolycubeShape
    {
        public IReadOnlyList<Vector3Int> Cells { get; }

        public PolycubeShape(IEnumerable<Vector3Int> cells)
        {
            Cells = cells.ToList();
        }

        public PolycubeShape RotatedX(int steps90) => Rotate(steps90, c => new Vector3Int(c.x, -c.z, c.y));
        public PolycubeShape RotatedY(int steps90) => Rotate(steps90, c => new Vector3Int(c.z, c.y, -c.x));
        public PolycubeShape RotatedZ(int steps90) => Rotate(steps90, c => new Vector3Int(-c.y, c.x, c.z));

        private PolycubeShape Rotate(int steps90, System.Func<Vector3Int, Vector3Int> quarterTurn)
        {
            int steps = ((steps90 % 4) + 4) % 4;
            IEnumerable<Vector3Int> cells = Cells;
            for (int i = 0; i < steps; i++)
            {
                cells = cells.Select(quarterTurn);
            }
            return new PolycubeShape(cells);
        }
    }
}
