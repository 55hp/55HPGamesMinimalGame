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
        public void CanPlaceAt_RejectsCellsAboveStorage_WhenThereIsNoHeadroom()
        {
            var grid = new VoxelGrid(3, 1, 3);
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, 1, 0)));
        }

        [Test]
        public void CanPlaceAt_AllowsCellsAboveHeight_WhenInsideHeadroom()
        {
            var grid = new VoxelGrid(3, 1, 3, headroom: 4);
            var tall = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0), new Vector3Int(0, 2, 0) });

            Assert.IsTrue(PlacementRules.CanPlaceAt(grid, tall, new Vector3Int(0, 1, 0))); // Y = 1..3
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, tall, new Vector3Int(0, 3, 0))); // Y = 3..5, past storage (5 rows: 0..4)
        }

        [Test]
        public void CanPlaceAt_InHeadroom_StillRejectsXZOutOfBounds_FloorAndOccupiedCells()
        {
            var grid = new VoxelGrid(2, 1, 3, headroom: 4);
            grid.SetOccupied(0, 2, 0, true); // a locked cell above the well's height

            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(1, 2, 0))); // X = 2
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, -1, 0))); // floor
            Assert.IsFalse(PlacementRules.CanPlaceAt(grid, TwoCellShape(), new Vector3Int(0, 2, 0))); // overlaps (0,2,0)
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
        public void LockInto_WritesCellsAboveHeight_WhenInsideHeadroom()
        {
            var grid = new VoxelGrid(3, 1, 3, headroom: 2);
            var shape = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0) }); // top cell at Y = 1 = Height

            PlacementRules.LockInto(grid, shape, new Vector3Int(0, 0, 0));

            Assert.IsTrue(grid.IsOccupied(0, 0, 0));
            Assert.IsTrue(grid.IsOccupied(0, 1, 0)); // nothing discarded
        }

        [Test]
        public void ClearFullLayersTouchedBy_ClearsLayersAboveHeight()
        {
            var grid = new VoxelGrid(1, 1, 1, headroom: 2); // width/depth=1: every occupied layer is full
            var shape = new PolycubeShape(new[] { Vector3Int.zero, new Vector3Int(0, 1, 0) });
            var origin = new Vector3Int(0, 0, 0);
            PlacementRules.LockInto(grid, shape, origin);

            int cleared = PlacementRules.ClearFullLayersTouchedBy(grid, shape, origin);

            Assert.AreEqual(2, cleared); // the Y = 1 layer (above Height) counts too
            Assert.IsFalse(grid.AnyOccupiedAtOrAbove(0));
        }

        [Test]
        public void ClearLayerAndCollapse_ShiftsHeadroomCellsDown()
        {
            var grid = new VoxelGrid(1, 2, 1, headroom: 2);
            grid.SetOccupied(0, 0, 0, true);
            grid.SetOccupied(0, 3, 0, true); // top storage row

            grid.ClearLayerAndCollapse(0);

            Assert.IsTrue(grid.IsOccupied(0, 2, 0));
            Assert.IsFalse(grid.IsOccupied(0, 3, 0));
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
