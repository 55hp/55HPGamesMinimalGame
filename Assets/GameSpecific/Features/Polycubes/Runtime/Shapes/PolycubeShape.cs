using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace hp55games.Polycubes.Shapes
{
    public sealed class PolycubeShape
    {
        public IReadOnlyList<Vector3Int> Cells { get; }

        // Optional stable identifier - null/empty for a shape that's never been named (e.g. a raw
        // PolycubeGenerator.GenerateAllConnected result). Game-agnostic on purpose (just "this
        // shape has a name", no enabled/disabled concept - that's a Blockout-specific curation
        // decision, see BlockoutShapeSet).
        public string Name { get; }

        public PolycubeShape(IEnumerable<Vector3Int> cells, string name = null)
        {
            Cells = cells.ToList();
            Name = name;
        }

        // Copy with a name attached/replaced - PolycubeShape is otherwise immutable.
        public PolycubeShape WithName(string name) => new PolycubeShape(Cells, name);

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
            return new PolycubeShape(cells, Name); // rotation doesn't change which piece this is
        }
    }
}
