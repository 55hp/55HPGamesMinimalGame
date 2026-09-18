using System.Collections.Generic;
using UnityEngine;
using hp55games.Polycubes.Shapes;

namespace hp55games.Polycubes.Grid
{
    public static class PlacementRules
    {
        public static bool CanPlaceAt(VoxelGrid grid, PolycubeShape shape, Vector3Int origin)
            => CanPlaceAt(grid, shape, origin, allowAboveTop: false);

        // allowAboveTop: a cell whose Y lands at or beyond the grid's ceiling (Height) is treated
        // as valid/unoccupied, as if the grid extended infinitely upward, instead of rejected -
        // X/Z bounds and the floor (Y < 0) are still enforced normally either way. Used for
        // rotation, which should only ever be blocked by the well's side walls and floor, never
        // by its open top (no wireframe there - see BlockoutWellWireframe/README §2).
        public static bool CanPlaceAt(VoxelGrid grid, PolycubeShape shape, Vector3Int origin, bool allowAboveTop)
        {
            foreach (var cell in shape.Cells)
            {
                var world = origin + cell;

                if (allowAboveTop && world.y >= grid.Height)
                {
                    if (world.x < 0 || world.x >= grid.Width || world.z < 0 || world.z >= grid.Depth) return false;
                    continue; // above the ceiling can't be occupied - nothing is ever placed there
                }

                if (!grid.IsInBounds(world.x, world.y, world.z)) return false;
                if (grid.IsOccupied(world.x, world.y, world.z)) return false;
            }
            return true;
        }

        public static void LockInto(VoxelGrid grid, PolycubeShape shape, Vector3Int origin)
        {
            foreach (var cell in shape.Cells)
            {
                var world = origin + cell;
                grid.SetOccupied(world.x, world.y, world.z, true);
            }
        }

        // Call after LockInto: clears every full layer among the ones the shape's cells touched
        // at `origin`, and returns how many were cleared. Processes touched layers from highest Y
        // to lowest - clearing a layer only shifts what's ABOVE it down by one (see
        // VoxelGrid.ClearLayerAndCollapse), so a not-yet-processed lower layer's index stays valid
        // after a higher one is cleared; clearing bottom-up would invalidate it instead.
        public static int ClearFullLayersTouchedBy(VoxelGrid grid, PolycubeShape shape, Vector3Int origin)
            => ClearFullLayersTouchedBy(grid, shape, origin, null);

        // Same as above, plus reports exactly which Y layers were cleared (in the same
        // highest-first order they were processed) via clearedLayerYs, when a caller isn't
        // satisfied with just the count - Blockout uses this to mirror the collapse in its own
        // (color-aware) rendering layer, which this game-agnostic grid knows nothing about.
        public static int ClearFullLayersTouchedBy(VoxelGrid grid, PolycubeShape shape, Vector3Int origin, List<int> clearedLayerYs)
        {
            var touchedLayers = new List<int>();
            foreach (var cell in shape.Cells)
            {
                int y = origin.y + cell.y;
                if (!touchedLayers.Contains(y)) touchedLayers.Add(y);
            }

            touchedLayers.Sort();
            touchedLayers.Reverse();

            int cleared = 0;
            foreach (var y in touchedLayers)
            {
                if (grid.IsLayerFull(y))
                {
                    grid.ClearLayerAndCollapse(y);
                    clearedLayerYs?.Add(y);
                    cleared++;
                }
            }
            return cleared;
        }
    }
}
