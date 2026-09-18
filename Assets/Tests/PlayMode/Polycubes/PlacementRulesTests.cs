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
        public void CanPlaceAt_DefaultOverload_StillRejectsAboveHeight()
        {
            // The 3-arg overload must keep its original (allowAboveTop: false) behavior - every
            // existing caller (fall step, move, hard drop, lock validation) relies on this.
            var grid = new VoxelGrid(3, 1, 3);
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, 1, 0)));
        }

        [Test]
        public void CanPlaceAt_AllowAboveTop_ReturnsTrue_WhenOnlyYExceedsHeight()
        {
            var grid = new VoxelGrid(3, 1, 3);
            Assert.IsTrue(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, 1, 0), allowAboveTop: true));
        }

        [Test]
        public void CanPlaceAt_AllowAboveTop_StillReturnsFalse_WhenXOrZExitBounds()
        {
            var grid = new VoxelGrid(2, 1, 3);
            // X=2 is out of bounds for Width=2 regardless of Y - the open-top exemption only ever
            // relaxes the upper Y bound, never the side walls.
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(1, 1, 0), allowAboveTop: true));
        }

        [Test]
        public void CanPlaceAt_AllowAboveTop_StillReturnsFalse_WhenYIsBelowTheFloor()
        {
            var grid = new VoxelGrid(3, 3, 3);
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, -1, 0), allowAboveTop: true));
        }

        [Test]
        public void LockInto_MarksAllShapeCellsAsOccupied()
        {
            var grid = new VoxelGrid(3, 3, 3);
            PlacementRules.LockInto(grid, TwoCellShape(), new Vector3Int(0, 0, 0));

            Assert.IsTrue(grid.IsOccupied(0, 0, 0));
            Assert.IsTrue(grid.IsOccupied(1, 0, 0));
        }

        [Test]
        public void LockInto_DoesNotThrow_WhenACellIsAboveTheCeiling()
        {
            // Regression: a rotation committed via CanPlaceAt(allowAboveTop: true) can leave a
            // cell at Y >= Height. If the piece then has no room left to fall before it locks,
            // LockInto used to call VoxelGrid.SetOccupied out of bounds and throw
            // ArgumentOutOfRangeException on every subsequent lock attempt - looked like the game
            // freezing (no piece ever locks, nothing new ever spawns).
            var grid = new VoxelGrid(3, 1, 3);
            var shape = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0) }); // top cell at Y=1, Height=1

            Assert.DoesNotThrow(() => PlacementRules.LockInto(grid, shape, new Vector3Int(0, 0, 0)));
            Assert.IsTrue(grid.IsOccupied(0, 0, 0));   // the in-bounds cell still locks normally
        }

        [Test]
        public void ClearFullLayersTouchedBy_DoesNotThrow_WhenATouchedLayerIsAboveTheCeiling()
        {
            var grid = new VoxelGrid(1, 1, 1); // width/depth=1 so the one in-bounds layer is trivially full
            var shape = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0) }); // top cell at Y=1, Height=1
            var origin = new Vector3Int(0, 0, 0);
            PlacementRules.LockInto(grid, shape, origin);

            int cleared = 0;
            Assert.DoesNotThrow(() => cleared = PlacementRules.ClearFullLayersTouchedBy(grid, shape, origin));
            Assert.AreEqual(1, cleared); // only the real layer (Y=0) counted - the above-ceiling one is skipped
        }

        [Test]
        public void ClearFullLayersTouchedBy_ClearsLayer_AndCollapsesAbove()
        {
            var grid = new VoxelGrid(2, 3, 2);
            // Layer 0: fill everything except (1,0,0), which the piece will occupy.
            grid.SetOccupied(0, 0, 0, true);
            grid.SetOccupied(0, 0, 1, true);
            grid.SetOccupied(1, 0, 1, true);
            // Layer 1: a single marker cell, to verify it shifts down to layer 0 after the clear.
            grid.SetOccupied(0, 1, 0, true);

            var shape = new PolycubeShape(new[] { Vector3Int.zero });
            var origin = new Vector3Int(1, 0, 0);
            PlacementRules.LockInto(grid, shape, origin);

            int cleared = PlacementRules.ClearFullLayersTouchedBy(grid, shape, origin);

            Assert.AreEqual(1, cleared);
            Assert.IsTrue(grid.IsOccupied(0, 0, 0));  // old layer 1's marker collapsed down to layer 0
            Assert.IsFalse(grid.IsOccupied(1, 0, 0)); // rest of old layer 1 (empty) also collapsed down
            Assert.IsFalse(grid.IsOccupied(0, 2, 0)); // top layer now empty after everything shifted down
        }

        [Test]
        public void ClearFullLayersTouchedBy_ClearsSimultaneousLayers_HighestFirst()
        {
            // width=2 so each layer needs 2 cells to be full (a width/depth of 1 would make every
            // single-cell placement trivially "complete" a layer, which wouldn't exercise ordering).
            var grid = new VoxelGrid(2, 3, 1);
            grid.SetOccupied(1, 0, 0, true); // layer 0 needs just the piece's cell to be full
            grid.SetOccupied(1, 1, 0, true); // layer 1 needs just the piece's cell to be full
            grid.SetOccupied(1, 2, 0, true); // layer 2 marker - not full, only used to verify collapse

            // Vertical 2-cell piece: completes layers 0 and 1 simultaneously.
            var shape = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0) });
            var origin = new Vector3Int(0, 0, 0);
            PlacementRules.LockInto(grid, shape, origin);

            int cleared = PlacementRules.ClearFullLayersTouchedBy(grid, shape, origin);

            // If layers were cleared lowest-first instead of highest-first, clearing layer 0 would
            // shift layer 1's (already-identified-as-full) content down before it's processed,
            // leaving it no longer full and skipped - cleared would be 1, and the marker would end
            // up fully intact at layer 0 instead of alone in its own cell.
            Assert.AreEqual(2, cleared);
            Assert.IsTrue(grid.IsOccupied(1, 0, 0));  // layer 2's marker, now at layer 0
            Assert.IsFalse(grid.IsOccupied(0, 0, 0));
            Assert.IsFalse(grid.IsOccupied(1, 1, 0));
            Assert.IsFalse(grid.IsOccupied(1, 2, 0));
        }

        [Test]
        public void ClearFullLayersTouchedBy_ReturnsZero_WhenTouchedLayerIsNotFull()
        {
            var grid = new VoxelGrid(2, 2, 2);
            var shape = new PolycubeShape(new[] { Vector3Int.zero });
            var origin = new Vector3Int(0, 0, 0);
            PlacementRules.LockInto(grid, shape, origin);

            int cleared = PlacementRules.ClearFullLayersTouchedBy(grid, shape, origin);

            Assert.AreEqual(0, cleared);
            Assert.IsTrue(grid.IsOccupied(0, 0, 0)); // nothing cleared away
        }
    }
}
