using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Tests
{
    public class BlockoutClearBehaviourTests
    {
        // Stand-in for a real skin's behaviour asset - exercises the interface/abstract-base
        // contract itself (BlockoutSkin Phase 1 needs this to exist and dispatch correctly, ahead
        // of Phase 2's first real implementation).
        private sealed class RecordingClearBehaviour : BlockoutClearBehaviour
        {
            public BlockoutClearContext? LastContext;
            public override void OnLayersCleared(BlockoutClearContext context) => LastContext = context;
        }

        [Test]
        public void OnLayersCleared_ReceivesTheExactContextPassedIn()
        {
            var behaviour = ScriptableObject.CreateInstance<RecordingClearBehaviour>();
            var positions = new List<Vector3Int> { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) };
            var colors = new List<Color> { Color.red, Color.blue };
            var context = new BlockoutClearContext(positions, colors, layerCount: 2);

            // Dispatched through the interface, not the concrete type - this is how BlockoutSkin
            // will call it (IBlockoutClearBehaviour ClearBehaviour => _clearBehaviour).
            ((IBlockoutClearBehaviour)behaviour).OnLayersCleared(context);

            Assert.IsTrue(behaviour.LastContext.HasValue);
            Assert.AreSame(positions, behaviour.LastContext.Value.ClearedCellPositions);
            Assert.AreSame(colors, behaviour.LastContext.Value.ClearedCellColors);
            Assert.AreEqual(2, behaviour.LastContext.Value.LayerCount);

            Object.DestroyImmediate(behaviour);
        }
    }
}
