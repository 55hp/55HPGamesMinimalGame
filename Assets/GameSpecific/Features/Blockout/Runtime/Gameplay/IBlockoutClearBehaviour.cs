using System.Collections.Generic;
using UnityEngine;

namespace hp55games.Blockout.Gameplay
{
    // What a single layer-clear needs to hand to a skin's visual reaction: the world-space cell
    // positions that were cleared (as they were immediately before the grid's clear-and-collapse)
    // and the color each cell was locked with - parallel lists, same index = same cell - plus how
    // many layers this one clear covered (LayersClearedEvent.LayerCount, for reference/effect
    // scaling, not for deciding whether to fire at all - that's already handled by the caller).
    public readonly struct BlockoutClearContext
    {
        public readonly IReadOnlyList<Vector3Int> ClearedCellPositions;
        public readonly IReadOnlyList<Color> ClearedCellColors;
        public readonly int LayerCount;

        public BlockoutClearContext(IReadOnlyList<Vector3Int> clearedCellPositions, IReadOnlyList<Color> clearedCellColors, int layerCount)
        {
            ClearedCellPositions = clearedCellPositions;
            ClearedCellColors = clearedCellColors;
            LayerCount = layerCount;
        }
    }

    // Polymorphic per-skin visual reaction to a layer clear - a skin's own implementation decides
    // what happens (nothing extra, physics objects, etc.), so adding a new skin never touches the
    // shared clear-handling code that invokes this. See BlockoutClearBehaviour for the
    // ScriptableObject base a BlockoutSkin actually serializes a reference to (Unity can't
    // serialize a bare interface field without custom tooling).
    public interface IBlockoutClearBehaviour
    {
        void OnLayersCleared(BlockoutClearContext context);
    }
}
