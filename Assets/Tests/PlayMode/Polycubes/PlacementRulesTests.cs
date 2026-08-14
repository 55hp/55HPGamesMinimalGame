using NUnit.Framework;
using UnityEngine;
using hp55games.Polycubes.Grid;
using hp55games.Polycubes.Shapes;

namespace hp55games.Polycubes.Tests
{
    public class PlacementRulesTests
    {
        private static PolycubeShape TwoCellShape() => new PolycubeShape(new[]
        {
            Vector3Int.zero,
            new Vector3Int(1, 0, 0),
        });

        [Test]
        public void CanPlaceAt_ReturnsTrue_WhenCellsAreFreeAndInBounds()
        {
            var grid = new VoxelGrid(3, 3, 3);
            Assert.IsTrue(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, 0, 0)));
        }

        [Test]
        public void CanPlaceAt_ReturnsFalse_WhenAnyCellExitsGridBounds()
        {
            var grid = new VoxelGrid(2, 3, 3);
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(1, 0, 0)));
        }

        [Test]
        public void CanPlaceAt_ReturnsFalse_WhenAnyCellOverlapsOccupiedSpace()
        {
            var grid = new VoxelGrid(3, 3, 3);
            grid.SetOccupied(1, 0, 0, true);

            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, 0, 0)));
        }

        [Test]
        public void LockInto_MarksAllShapeCellsAsOccupied()
        {
            var grid = new VoxelGrid(3, 3, 3);
            PlacementRules.LockInto(grid, TwoCellShape(), new Vector3Int(0, 0, 0));

            Assert.IsTrue(grid.IsOccupied(0, 0, 0));
            Assert.IsTrue(grid.IsOccupied(1, 0, 0));
        }
    }
}
