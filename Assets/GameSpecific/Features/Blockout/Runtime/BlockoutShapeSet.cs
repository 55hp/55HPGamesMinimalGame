using System.Collections.Generic;
using System.Linq;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout
{
    public static class BlockoutShapeSet
    {
        public static List<PolycubeShape> BuildDefault()
        {
            var tetracubes = PolycubeGenerator.GenerateAllConnected(4);
            var pentacubes = SelectPentacubes(PolycubeGenerator.GenerateAllConnected(5), count: 4);

            var result = new List<PolycubeShape>(tetracubes);
            result.AddRange(pentacubes);
            return result;
        }

        // Open question 1 (GDD): "4 pentacubi semplici e riconoscibili (I, L, T, S)". Automatic-heuristic default
        // (per Technical Spec) rather than a hand-picked list: keep only single-layer (planar) pentacubes — those
        // read cleanly on a mobile screen the same way flat pentominoes do — and take the most compact ones first.
        // Swap this method's ordering/filter if Franci hand-picks a different four.
        private static List<PolycubeShape> SelectPentacubes(List<PolycubeShape> pentacubes, int count)
        {
            return pentacubes
                .Where(IsPlanar)
                .OrderBy(BoundingBoxArea)
                .ThenBy(PolycubeGenerator.NormalizedCellKey)
                .Take(count)
                .ToList();
        }

        private static bool IsPlanar(PolycubeShape shape)
        {
            var cells = shape.Cells;
            bool flatX = cells.Select(c => c.x).Distinct().Count() == 1;
            bool flatY = cells.Select(c => c.y).Distinct().Count() == 1;
            bool flatZ = cells.Select(c => c.z).Distinct().Count() == 1;
            return flatX || flatY || flatZ;
        }

        private static int BoundingBoxArea(PolycubeShape shape)
        {
            var cells = shape.Cells;
            int spanX = cells.Max(c => c.x) - cells.Min(c => c.x) + 1;
            int spanY = cells.Max(c => c.y) - cells.Min(c => c.y) + 1;
            int spanZ = cells.Max(c => c.z) - cells.Min(c => c.z) + 1;
            return spanX * spanY * spanZ;
        }
    }
}
