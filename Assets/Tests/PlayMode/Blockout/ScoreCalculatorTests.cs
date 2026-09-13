using NUnit.Framework;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Tests
{
    public class ScoreCalculatorTests
    {
        [TestCase(1, 100)]
        [TestCase(2, 400)]
        [TestCase(3, 900)]
        [TestCase(4, 1600)]
        [TestCase(5, 2500)]
        public void PointsForSimultaneousClears_MatchesConfirmedFormula(int layerCount, int expectedPoints)
        {
            Assert.AreEqual(expectedPoints, ScoreCalculator.PointsForSimultaneousClears(layerCount));
        }
    }
}
