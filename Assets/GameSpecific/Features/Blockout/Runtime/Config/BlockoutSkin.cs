using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Config;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Config
{
    // Cosmetic skin: defines only the falling piece's look - one base colour plus URP Lit
    // surface params (metallic/smoothness/emission, see CellSurface). Locked cubes take their
    // well level's colour instead (BlockoutWellConfig.GetLevelColor), so a skin never controls
    // locked-cube colour. Generated from blockout_skins.json by BlockoutSkinImporter. Purely
    // cosmetic: never read by scoring, difficulty, or placement logic. Multi-instance
    // IConfigAsset (one per skin) via IConfigCatalogService.GetAll<BlockoutSkin>(), not Get<T>().
    //
    // The "Legacy" fields below belong to the retiring periodic-table skins (118 elements +
    // Default/Profondita/Juicy Clear). They stay only so the periodic shop UI, achievements,
    // PeriodicMeltClearBehaviour and the old importer keep compiling and the 118 assets keep
    // their data - all removed together in FASE 4.
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

        [Header("Falling Piece Look")]
        [Tooltip("Colour of the falling piece's cubes (URP Lit base colour).")]
        [SerializeField] private Color _baseColor = Color.white;
        [Tooltip("URP Lit Metallic-workflow metallic value.")]
        [Range(0f, 1f)]
        [SerializeField] private float _metallic;
        [Tooltip("URP Lit Metallic-workflow smoothness value.")]
        [Range(0f, 1f)]
        [SerializeField] private float _smoothness = 0.5f;
        [Tooltip("Emission = Base Color x this. 0 = no emission.")]
        [Range(0f, 2f)]
        [SerializeField] private float _emissionIntensity;

        [Header("Clear")]
        [Tooltip("Visual reaction when a layer clears while this skin is active. The 20 authored skins all use NeutralClearBehaviour.")]
        [SerializeField] private BlockoutClearBehaviour _clearBehaviour;

        [Header("Legacy (periodic table - removed in FASE 4)")]
        [Tooltip("LEGACY: per-shape-index palette for the falling/locked piece cubes - same semantics as BlockoutSpawner's current fixed palette, now owned per skin instead of hardcoded in the spawner. A periodic-table element skin uses a single-entry palette (one color for every shape - see the GDD's 'single material per element', not per shape).")]
        [SerializeField] private Color[] _pieceColors;

        [Header("Unlock Economy")]
        [Tooltip("Cost to unlock, in coins. -1 (CostNotSetValue) means not decided yet - TryUnlockSkin refuses to unlock such a skin until a real cost is assigned. Irrelevant when Unlocked By Default is true.")]
        [SerializeField] private int _costInCoins = CostNotSetValue;
        [Tooltip("True only for the always-free, always-available default skin. Every other skin's actual unlock state lives in save data (per player), not here - this flag is the one skin-level exception to that rule.")]
        [SerializeField] private bool _unlockedByDefault;
        [Tooltip("Which of the 3 unlock mechanisms applies. Default for non-element skins too (Default/Profondita/Juicy Clear use the older coins-only TryUnlockSkin path regardless of this field's value).")]
        [SerializeField] private BlockoutSkinUnlockMethod _unlockMethod;
        [Tooltip("Achievement id (see IBlockoutAchievementService) that grants this skin when Unlock Method is Achievement. Empty otherwise.")]
        [SerializeField] private string _unlockAchievementId;

        [Header("Legacy Periodic Table Element Data (removed in FASE 4)")]
        [Tooltip("Chemical symbol (e.g. \"C\"). Empty for non-element skins (Default, Profondita, Juicy Clear).")]
        [SerializeField] private string _elementSymbol;
        [Tooltip("Atomic number. 0 for non-element skins - also doubles as \"is this an element skin\" for shop queries (see BlockoutSkinService.GetElementShopEntries).")]
        [SerializeField] private int _atomicNumber;
        [Tooltip("Which material variant (Bezi's Phase 2 shaders) this element's pieces should render with. Unused by non-element skins.")]
        [SerializeField] private PieceMaterialCategory _materialCategory;
        [Tooltip("Log-normalized real density (0 = lightest, 1 = densest) driving the melt clear behaviour's spherule impulse (see PeriodicMeltClearBehaviour). Unused by non-element skins.")]
        [SerializeField] private float _densityNormalized;
        [Tooltip("True for Z >= 84 (Polonium through Oganesson). False for non-element skins.")]
        [SerializeField] private bool _isRadioactive;

        public string SkinId => _skinId;
        public string DisplayName => _displayName;
        public int CostInCoins => _costInCoins;
        public bool HasCostSet => _costInCoins >= 0;
        public bool UnlockedByDefault => _unlockedByDefault;
        public Color BaseColor => _baseColor;
        public float Metallic => _metallic;
        public float Smoothness => _smoothness;
        public float EmissionIntensity => _emissionIntensity;
        public IBlockoutClearBehaviour ClearBehaviour => _clearBehaviour;

        // LEGACY (FASE 4): periodic shop UI only. Gameplay reads BaseColor.
        public IReadOnlyList<Color> PieceColors => _pieceColors;
        public string ElementSymbol => _elementSymbol;
        public int AtomicNumber => _atomicNumber;
        public PieceMaterialCategory MaterialCategory => _materialCategory;
        public float DensityNormalized => _densityNormalized;
        public bool IsRadioactive => _isRadioactive;
        public BlockoutSkinUnlockMethod UnlockMethod => _unlockMethod;
        public string UnlockAchievementId => _unlockAchievementId;

#if UNITY_EDITOR
        // Editor-only bulk setter for BlockoutSkinImporter (same pattern as
        // EditorConfigureElement below). Re-running the importer fully overwrites the authored
        // fields; legacy element fields are left untouched (always empty on these assets).
        public void EditorConfigureSkin(string skinId, string displayName, int costInCoins, bool unlockedByDefault,
            BlockoutSkinUnlockMethod unlockMethod, Color baseColor, float metallic, float smoothness,
            float emissionIntensity, BlockoutClearBehaviour clearBehaviour)
        {
            _skinId = skinId;
            _displayName = displayName;
            _costInCoins = costInCoins;
            _unlockedByDefault = unlockedByDefault;
            _unlockMethod = unlockMethod;
            _unlockAchievementId = string.Empty;
            _baseColor = baseColor;
            _metallic = Mathf.Clamp01(metallic);
            _smoothness = Mathf.Clamp01(smoothness);
            _emissionIntensity = Mathf.Clamp(emissionIntensity, 0f, 2f);
            _clearBehaviour = clearBehaviour;
        }

        // LEGACY (FASE 4): used only by BlockoutPeriodicElementImporter.
        // Editor-only bulk setter for BlockoutPeriodicElementImporter - mirrors
        // ConfigCatalog.EditorSetConfigs's "editor tooling writes private fields directly, not
        // exposed to runtime code" pattern rather than adding public setters real gameplay code
        // could also call. Re-running the importer calls this again on the same asset (found by
        // path), so it fully overwrites rather than merging.
        public void EditorConfigureElement(string skinId, string displayName, int costInCoins, bool unlockedByDefault,
            Color pieceColor, BlockoutClearBehaviour clearBehaviour, string elementSymbol, int atomicNumber,
            PieceMaterialCategory materialCategory, float densityNormalized, bool isRadioactive,
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
            _isRadioactive = isRadioactive;
            _unlockMethod = unlockMethod;
            _unlockAchievementId = unlockAchievementId;
        }
#endif
    }
}
