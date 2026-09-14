using UnityEngine;

namespace hp55games.Blockout.Gameplay
{
    // ScriptableObject base for IBlockoutClearBehaviour implementations - lets BlockoutSkin hold
    // a normal, Inspector-assignable asset reference while still getting real polymorphism: each
    // concrete behaviour is its own asset, picked per skin, with no central switch/enum over skin
    // types. First concrete implementation (neutral/no-op) ships in Phase 2.
    public abstract class BlockoutClearBehaviour : ScriptableObject, IBlockoutClearBehaviour
    {
        public abstract void OnLayersCleared(BlockoutClearContext context);
    }
}
