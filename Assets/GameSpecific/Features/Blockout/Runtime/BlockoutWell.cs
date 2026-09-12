using hp55games.Polycubes.Grid;
using hp55games.Blockout.Config;

namespace hp55games.Blockout
{
    public sealed class BlockoutWell
    {
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        public VoxelGrid Grid { get; }

        public BlockoutWell(BlockoutWellConfig config)
        {
            Width = config.Width;
            Height = config.Height;
            Depth = config.Depth;
            Grid = new VoxelGrid(Width, Height, Depth);
        }
    }
}
