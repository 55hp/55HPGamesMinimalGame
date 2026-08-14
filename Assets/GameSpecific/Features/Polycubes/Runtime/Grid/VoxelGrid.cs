using System;

namespace hp55games.Polycubes.Grid
{
    public sealed class VoxelGrid
    {
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        private readonly bool[,,] _occupied;

        public VoxelGrid(int width, int height, int depth)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));

            Width = width;
            Height = height;
            Depth = depth;
            _occupied = new bool[width, height, depth];
        }

        public bool IsInBounds(int x, int y, int z) =>
            x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth;

        public bool IsOccupied(int x, int y, int z)
        {
            RequireInBounds(x, y, z);
            return _occupied[x, y, z];
        }

        public void SetOccupied(int x, int y, int z, bool occupied)
        {
            RequireInBounds(x, y, z);
            _occupied[x, y, z] = occupied;
        }

        public bool IsLayerFull(int y)
        {
            RequireInBounds(0, y, 0);
            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    if (!_occupied[x, y, z]) return false;
                }
            }
            return true;
        }

        public void ClearLayerAndCollapse(int y)
        {
            RequireInBounds(0, y, 0);
            for (int layer = y; layer < Height - 1; layer++)
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        _occupied[x, layer, z] = _occupied[x, layer + 1, z];
                    }
                }
            }

            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    _occupied[x, Height - 1, z] = false;
                }
            }
        }

        private void RequireInBounds(int x, int y, int z)
        {
            if (!IsInBounds(x, y, z))
            {
                throw new ArgumentOutOfRangeException(
                    $"({x}, {y}, {z}) is out of bounds for a {Width}x{Height}x{Depth} grid.");
            }
        }
    }
}
