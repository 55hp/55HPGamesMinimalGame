using System;

namespace hp55games.Polycubes.Grid
{
    public sealed class VoxelGrid
    {
        public int Width { get; }
        // The physical height of the container (a well's walls, camera framing, wireframe) -
        // NOT how high a locked cell can sit; that's StorageHeight.
        public int Height { get; }
        public int Depth { get; }

        // Extra rows stored above Height, so a locked cell can exist there until it's cleared or
        // judged by the game layer. Never part of the physical bounds.
        public int Headroom { get; }
        public int StorageHeight => Height + Headroom;

        private readonly bool[,,] _occupied;

        public VoxelGrid(int width, int height, int depth, int headroom = 0)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));
            if (headroom < 0) throw new ArgumentOutOfRangeException(nameof(headroom));

            Width = width;
            Height = height;
            Depth = depth;
            Headroom = headroom;
            _occupied = new bool[width, height + headroom, depth];
        }

        // Physical bounds: the container itself (y < Height).
        public bool IsInBounds(int x, int y, int z) =>
            x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth;

        // Storage bounds: any cell a locked block can occupy (y < StorageHeight). Equal to
        // IsInBounds when Headroom is 0. Occupancy reads/writes use this one.
        public bool IsInStorage(int x, int y, int z) =>
            x >= 0 && x < Width && y >= 0 && y < StorageHeight && z >= 0 && z < Depth;

        // True if any locked cell sits at Y >= y.
        public bool AnyOccupiedAtOrAbove(int y)
        {
            for (int layer = Math.Max(y, 0); layer < StorageHeight; layer++)
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        if (_occupied[x, layer, z]) return true;
                    }
                }
            }
            return false;
        }

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
            for (int layer = y; layer < StorageHeight - 1; layer++)
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
                    _occupied[x, StorageHeight - 1, z] = false;
                }
            }
        }

        private void RequireInBounds(int x, int y, int z)
        {
            if (!IsInStorage(x, y, z))
            {
                throw new ArgumentOutOfRangeException(
                    $"({x}, {y}, {z}) is out of bounds for a {Width}x{StorageHeight}x{Depth} grid.");
            }
        }
    }
}
