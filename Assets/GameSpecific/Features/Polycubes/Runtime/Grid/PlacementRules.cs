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
    }
}
