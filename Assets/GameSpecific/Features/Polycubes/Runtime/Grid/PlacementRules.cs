using System.Collections.Generic;
using UnityEngine;
using hp55games.Polycubes.Shapes;

namespace hp55games.Polycubes.Grid
{
    public static class PlacementRules
    {
        // One unconditional check: every cell must be inside the grid's storage (walls, floor,
        // and the headroom's top) and unoccupied. There's no "above the ceiling" exception - the
        // ceiling only matters to the game layer, judging locked cells after the fact.
        public static bool CanPlaceAt(VoxelGrid grid, PolycubeShape shape, Vector3Int origin)
        {
            foreach (var cell in shape.Cells)
            {
                var world = origin + cell;

                if (!grid.IsInStorage(world.x, world.y, world.z)) return false;
                if (grid.IsOccupied(world.x, world.y, world.z)) return false;
            }
            return true;
        }

        // Writes every cell - nothing is discarded. Throws (via VoxelGrid) if a cell is outside
        // storage, which CanPlaceAt already rules out for any position a piece can reach.
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
