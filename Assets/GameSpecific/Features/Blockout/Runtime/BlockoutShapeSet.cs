using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Polycubes.Shapes;
using hp55games.Blockout.Config;

namespace hp55games.Blockout
{
    public static class BlockoutShapeSet
    {
        // Stable, deterministic names for the 12 canonical pieces, assigned by
        // generation/selection ORDER (both PolycubeGenerator.GenerateAllConnected and
        // SelectPentacubes are deterministic for fixed inputs - no randomness) - not hand-picked
        // letter shapes (I/L/T/S), no such curation exists yet. Safe to rename later:
        // BlockoutShapeSelectionConfig keys off these strings, so a rename just needs a matching
        // update there.
        public static IReadOnlyList<string> AllPieceNames { get; } =
            BuildAllNamedShapes().Select(s => s.Name).ToList();

        // The default set, filtered down to only the pieces BlockoutShapeSelectionConfig (if
        // present in the catalog) marks enabled - a piece not listed there defaults to enabled.
        // Falls back to all 12 if the filter would otherwise empty the set (a config mistake
        // shouldn't be able to leave the game with zero pieces to spawn).
        public static List<PolycubeShape> BuildDefault()
        {
            var allShapes = BuildAllNamedShapes();
            var enabledByName = ResolveEnabledByName();

            var filtered = allShapes.Where(s => IsEnabled(s.Name, enabledByName)).ToList();
            if (filtered.Count == 0)
            {
                Debug.LogError("[BlockoutShapeSet] BlockoutShapeSelectionConfig disables every piece - falling back to all 12 rather than spawning with none.");
                return allShapes;
            }

            return filtered;
        }

        // The queryable (name, enabled) list Bezi's config drives - GetPieceEntries() and
        // BuildDefault() read the exact same source, so they can never disagree.
        public static IReadOnlyList<(string Name, bool Enabled)> GetPieceEntries()
        {
            var enabledByName = ResolveEnabledByName();
            return BuildAllNamedShapes()
                .Select(s => (s.Name, IsEnabled(s.Name, enabledByName)))
                .ToList();
        }

        private static bool IsEnabled(string name, IReadOnlyDictionary<string, bool> enabledByName) =>
            !enabledByName.TryGetValue(name, out var enabled) || enabled; // not listed -> defaults to true

        private static Dictionary<string, bool> ResolveEnabledByName()
        {
            var map = new Dictionary<string, bool>();

            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalog)) return map;

            // GetAll (empty if none), not Get (logs an error if absent) - this config is
            // optional by design, so a project that hasn't authored one yet shouldn't get an
            // error every time BuildDefault() runs.
            var config = catalog.GetAll<BlockoutShapeSelectionConfig>().FirstOrDefault();
            if (config == null) return map;

            foreach (var toggle in config.Pieces)
            {
                if (!string.IsNullOrEmpty(toggle.pieceName)) map[toggle.pieceName] = toggle.enabled;
            }

            return map;
        }

        // All 12 canonical pieces, named, before any enabled-flag filtering.
        private static List<PolycubeShape> BuildAllNamedShapes()
        {
            var tetracubes = PolycubeGenerator.GenerateAllConnected(4);
            var pentacubes = SelectPentacubes(PolycubeGenerator.GenerateAllConnected(5), count: 4);

            var result = new List<PolycubeShape>(tetracubes.Count + pentacubes.Count);
            for (int i = 0; i < tetracubes.Count; i++)
                result.Add(tetracubes[i].WithName($"Tetracube{i + 1:00}"));
            for (int i = 0; i < pentacubes.Count; i++)
                result.Add(pentacubes[i].WithName($"Pentacube{i + 1:00}"));

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
