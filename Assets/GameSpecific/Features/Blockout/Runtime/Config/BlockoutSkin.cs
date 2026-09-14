using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Config;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Config
{
    // Cosmetic skin: pairs a per-shape-index piece-color palette with a layer-clear visual
    // behaviour (IBlockoutClearBehaviour) - the two always travel together per skin (see the
    // skin system GDD), swapped in bulk by the active-skin service (Phase 3). Purely cosmetic:
    // never read by scoring, difficulty, or placement logic. Multi-instance IConfigAsset (one
    // per skin) via IConfigCatalogService.GetAll<BlockoutSkin>(), not Get<T>().
    [CreateAssetMenu(fileName = "BlockoutSkin", menuName = "hp55games/Blockout/Skin")]
    public sealed class BlockoutSkin : ScriptableObject, IConfigAsset
    {
        [Tooltip("Stable identifier for save data (active skin, unlocked list) - independent of the asset's file name/GUID so renaming the asset in the project doesn't orphan existing saves.")]
        [SerializeField] private string _skinId;

        [SerializeField] private string _displayName;

        [Tooltip("Cost to unlock, in coins. Irrelevant when Unlocked By Default is true.")]
        [SerializeField] private int _costInCoins;

        [Tooltip("True only for the always-free, always-available default skin. Every other skin's actual unlock state lives in save data (per player), not here - this flag is the one skin-level exception to that rule.")]
        [SerializeField] private bool _unlockedByDefault;

        [Tooltip("Per-shape-index palette for the falling/locked piece cubes - same semantics as BlockoutSpawner's current fixed palette, now owned per skin instead of hardcoded in the spawner.")]
        [SerializeField] private Color[] _pieceColors;

        [Tooltip("Visual reaction when a layer clears while this skin is active. Left unassigned only until a concrete behaviour asset exists to assign (Phase 2 ships the first one).")]
        [SerializeField] private BlockoutClearBehaviour _clearBehaviour;

        public string SkinId => _skinId;
        public string DisplayName => _displayName;
        public int CostInCoins => _costInCoins;
        public bool UnlockedByDefault => _unlockedByDefault;
        public IReadOnlyList<Color> PieceColors => _pieceColors;
        public IBlockoutClearBehaviour ClearBehaviour => _clearBehaviour;
    }
}
