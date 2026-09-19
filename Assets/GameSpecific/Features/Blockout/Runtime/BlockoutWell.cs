using hp55games.Polycubes.Grid;
using hp55games.Blockout.Config;

namespace hp55games.Blockout
{
    public sealed class BlockoutWell
    {
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        // Rows stored above Height for locked cells that haven't been cleared/judged yet.
        // A piece spawns with its reference cell at Y = Height and every polycube's cells lie
        // within 4 of that cell (largest shape has 5 cells), so it can occupy up to
        // Y = Height + 4 - 5 rows cover that.
        public const int HeadroomRows = 5;

        public VoxelGrid Grid { get; }

        public BlockoutWell(BlockoutWellConfig config)
        {
            Width = config.Width;
            Height = config.Height;
            Depth = config.Depth;
            Grid = new VoxelGrid(Width, Height, Depth, HeadroomRows);
        }
    }
}
