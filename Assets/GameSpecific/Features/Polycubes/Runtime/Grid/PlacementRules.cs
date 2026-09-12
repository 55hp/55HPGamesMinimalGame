using System.Collections.Generic;
using UnityEngine;
using hp55games.Polycubes.Shapes;

namespace hp55games.Polycubes.Grid
{
    public static class PlacementRules
    {
        public static bool CanPlaceAt(VoxelGrid grid, PolycubeShape shape, Vector3Int origin)
        {
            foreach (var cell in shape.Cells)
            {
                var world = origin + cell;
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
                    cleared++;
                }
            }
            return cleared;
        }
    }
}
