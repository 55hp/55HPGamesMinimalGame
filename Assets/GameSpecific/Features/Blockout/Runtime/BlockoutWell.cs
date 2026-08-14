using hp55games.Polycubes.Grid;

namespace hp55games.Blockout
{
    public sealed class BlockoutWell
    {
        public const int Width = 5;
        public const int Height = 10;
        public const int Depth = 5;

        public VoxelGrid Grid { get; }

        public BlockoutWell()
        {
            Grid = new VoxelGrid(Width, Height, Depth);
        }
    }
}
