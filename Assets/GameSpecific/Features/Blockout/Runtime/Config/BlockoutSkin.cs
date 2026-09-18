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
        // Unity's serializer doesn't support Nullable<int> (see the Periodic Table Technical
        // Doc's economy note) - this sentinel plays that role instead: TryUnlockSkin refuses to
        // unlock a skin whose cost is still this value, rather than silently treating it as free.
        // Every one of the 118 imported element skins starts at this value (see
        // BlockoutPeriodicElementImporter) until the unlock-cost formula is decided.
        public const int CostNotSetValue = -1;

        [Header("Identity")]
        [Tooltip("Stable identifier for save data (active skin, unlocked list) - independent of the asset's file name/GUID so renaming the asset in the project doesn't orphan existing saves.")]
        [SerializeField] private string _skinId;
        [Tooltip("Name shown to the player in the skin/shop UI.")]
        [SerializeField] private string _displayName;

        [Header("Visuals")]
        [Tooltip("Per-shape-index palette for the falling/locked piece cubes - same semantics as BlockoutSpawner's current fixed palette, now owned per skin instead of hardcoded in the spawner. A periodic-table element skin uses a single-entry palette (one color for every shape - see the GDD's 'single material per element', not per shape).")]
        [SerializeField] private Color[] _pieceColors;
        [Tooltip("Visual reaction when a layer clears while this skin is active. Left unassigned only until a concrete behaviour asset exists to assign (Phase 2 ships the first one).")]
        [SerializeField] private BlockoutClearBehaviour _clearBehaviour;

        [Header("Unlock Economy")]
        [Tooltip("Cost to unlock, in coins. -1 (CostNotSetValue) means not decided yet - TryUnlockSkin refuses to unlock such a skin until a real cost is assigned. Irrelevant when Unlocked By Default is true.")]
        [SerializeField] private int _costInCoins = CostNotSetValue;
        [Tooltip("True only for the always-free, always-available default skin. Every other skin's actual unlock state lives in save data (per player), not here - this flag is the one skin-level exception to that rule.")]
        [SerializeField] private bool _unlockedByDefault;
        [Tooltip("Which of the 3 unlock mechanisms applies. Default for non-element skins too (Default/Profondita/Juicy Clear use the older coins-only TryUnlockSkin path regardless of this field's value).")]
        [SerializeField] private BlockoutSkinUnlockMethod _unlockMethod;
        [Tooltip("Achievement id (see IBlockoutAchievementService) that grants this skin when Unlock Method is Achievement. Empty otherwise.")]
        [SerializeField] private string _unlockAchievementId;

        [Header("Periodic Table Element Data")]
        [Tooltip("Chemical symbol (e.g. \"C\"). Empty for non-element skins (Default, Profondita, Juicy Clear).")]
        [SerializeField] private string _elementSymbol;
        [Tooltip("Atomic number. 0 for non-element skins - also doubles as \"is this an element skin\" for shop queries (see BlockoutSkinService.GetElementShopEntries).")]
        [SerializeField] private int _atomicNumber;
        [Tooltip("Which material variant (Bezi's Phase 2 shaders) this element's pieces should render with. Unused by non-element skins.")]
        [SerializeField] private PieceMaterialCategory _materialCategory;
        [Tooltip("Log-normalized real density (0 = lightest, 1 = densest) driving the melt clear behaviour's spherule impulse (see PeriodicMeltClearBehaviour). Unused by non-element skins.")]
        [SerializeField] private float _densityNormalized;

        public string SkinId => _skinId;
        public string DisplayName => _displayName;
        public int CostInCoins => _costInCoins;
        public bool HasCostSet => _costInCoins >= 0;
        public bool UnlockedByDefault => _unlockedByDefault;
        public IReadOnlyList<Color> PieceColors => _pieceColors;
        public IBlockoutClearBehaviour ClearBehaviour => _clearBehaviour;
        public string ElementSymbol => _elementSymbol;
        public int AtomicNumber => _atomicNumber;
        public PieceMaterialCategory MaterialCategory => _materialCategory;
        public float DensityNormalized => _densityNormalized;
        public BlockoutSkinUnlockMethod UnlockMethod => _unlockMethod;
        public string UnlockAchievementId => _unlockAchievementId;

#if UNITY_EDITOR
        // Editor-only bulk setter for BlockoutPeriodicElementImporter - mirrors
        // ConfigCatalog.EditorSetConfigs's "editor tooling writes private fields directly, not
        // exposed to runtime code" pattern rather than adding public setters real gameplay code
        // could also call. Re-running the importer calls this again on the same asset (found by
        // path), so it fully overwrites rather than merging.
        public void EditorConfigureElement(string skinId, string displayName, int costInCoins, bool unlockedByDefault,
            Color pieceColor, BlockoutClearBehaviour clearBehaviour, string elementSymbol, int atomicNumber,
            PieceMaterialCategory materialCategory, float densityNormalized,
            BlockoutSkinUnlockMethod unlockMethod, string unlockAchievementId)
        {
            _skinId = skinId;
            _displayName = displayName;
            _costInCoins = costInCoins;
            _unlockedByDefault = unlockedByDefault;
            _pieceColors = new[] { pieceColor };
            _clearBehaviour = clearBehaviour;
            _elementSymbol = elementSymbol;
            _atomicNumber = atomicNumber;
            _materialCategory = materialCategory;
            _densityNormalized = densityNormalized;
            _unlockMethod = unlockMethod;
            _unlockAchievementId = unlockAchievementId;
        }
#endif
    }
}
