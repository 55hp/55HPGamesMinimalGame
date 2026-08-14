using NUnit.Framework;
using System;
using hp55games.Polycubes.Grid;

namespace hp55games.Polycubes.Tests
{
    public class VoxelGridTests
    {
        [Test]
        public void SetOccupied_Then_IsOccupied_ReflectsState()
        {
            var grid = new VoxelGrid(3, 4, 2);

            Assert.IsFalse(grid.IsOccupied(1, 2, 1));

            grid.SetOccupied(1, 2, 1, true);
            Assert.IsTrue(grid.IsOccupied(1, 2, 1));

            grid.SetOccupied(1, 2, 1, false);
            Assert.IsFalse(grid.IsOccupied(1, 2, 1));
        }

        [Test]
        public void IsInBounds_RejectsCoordinatesOutsideDimensions()
        {
            var grid = new VoxelGrid(3, 4, 2);

            Assert.IsTrue(grid.IsInBounds(0, 0, 0));
            Assert.IsTrue(grid.IsInBounds(2, 3, 1));
            Assert.IsFalse(grid.IsInBounds(-1, 0, 0));
            Assert.IsFalse(grid.IsInBounds(3, 0, 0));
            Assert.IsFalse(grid.IsInBounds(0, 4, 0));
            Assert.IsFalse(grid.IsInBounds(0, 0, 2));
        }

        [Test]
        public void SetOccupied_OutOfBounds_IsRejected()
        {
            var grid = new VoxelGrid(3, 4, 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetOccupied(3, 0, 0, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetOccupied(0, -1, 0, true));
        }

        [Test]
        public void IsLayerFull_DetectsFullAndPartialLayers()
        {
            var grid = new VoxelGrid(2, 3, 2);

            Assert.IsFalse(grid.IsLayerFull(1));

            grid.SetOccupied(0, 1, 0, true);
            grid.SetOccupied(1, 1, 0, true);
            grid.SetOccupied(0, 1, 1, true);
            Assert.IsFalse(grid.IsLayerFull(1));

            grid.SetOccupied(1, 1, 1, true);
            Assert.IsTrue(grid.IsLayerFull(1));
        }

        [Test]
        public void ClearLayerAndCollapse_ShiftsLayersAboveDown()
        {
            var grid = new VoxelGrid(2, 3, 1);

            // Layer 0: empty. Layer 1: one cell occupied. Layer 2: fully occupied.
            grid.SetOccupied(0, 1, 0, true);
            grid.SetOccupied(0, 2, 0, true);
            grid.SetOccupied(1, 2, 0, true);

            grid.ClearLayerAndCollapse(0);

            // Old layer 1 becomes new layer 0.
            Assert.IsTrue(grid.IsOccupied(0, 0, 0));
            Assert.IsFalse(grid.IsOccupied(1, 0, 0));

            // Old layer 2 becomes new layer 1.
            Assert.IsTrue(grid.IsOccupied(0, 1, 0));
            Assert.IsTrue(grid.IsOccupied(1, 1, 0));

            // Top layer is cleared, nothing shifts in from above it.
            Assert.IsFalse(grid.IsOccupied(0, 2, 0));
            Assert.IsFalse(grid.IsOccupied(1, 2, 0));
        }
    }
}
