using UnityEngine;

namespace hp55games.Blockout.Gameplay
{
    // First concrete IBlockoutClearBehaviour (Technical Doc Phase 2): does nothing beyond the
    // base game's own clear handling (grid collapse, scoring, rendering sync) - exists to
    // validate the layer-clear hookup end-to-end (BlockoutSpawner receiving a real
    // BlockoutClearContext and dispatching it) before a skin with an actual visual reaction
    // (Juicy Clear, Phase 5) is built against the same interface. This is what
    // BlockoutSkin_Default's _clearBehaviour points to.
    [CreateAssetMenu(fileName = "NeutralClearBehaviour", menuName = "hp55games/Blockout/Clear Behaviours/Neutral")]
    public sealed class NeutralClearBehaviour : BlockoutClearBehaviour
    {
        public override void OnLayersCleared(BlockoutClearContext context)
        {
            // Intentionally does nothing - "neutro" per the skin GDD.
        }
    }
}
