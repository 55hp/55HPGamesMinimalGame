namespace hp55games.Blockout.Gameplay
{
    public static class ScoreCalculator
    {
        // Confirmed formula: 100 * N^2, where N = layers cleared simultaneously by one piece.
        public static int PointsForSimultaneousClears(int layerCount) => 100 * layerCount * layerCount;
    }
}
