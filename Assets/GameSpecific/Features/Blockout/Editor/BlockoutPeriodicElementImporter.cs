using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using hp55games.Blockout.Config;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Config.EditorTools
{
    // Periodic Table Technical Doc Phase 1: generates/refreshes the 118 BlockoutSkin-element
    // assets from blockout_periodic_elements.json - 118 assets created by hand isn't reasonable
    // at that order of magnitude (see the Technical Doc). Safe to re-run: an existing asset for a
    // given atomic number is updated in place rather than duplicated (found by its deterministic
    // path - see AssetPathFor), so re-running after the JSON changes (e.g. once unlock costs are
    // decided, Phase 5) never orphans a SkinId already present in someone's save data.
    public static class BlockoutPeriodicElementImporter
    {
        private const string JsonAssetPath = "Assets/GameSpecific/Content/Skins/blockout_periodic_elements.json";
        private const string OutputFolder = "Assets/GameSpecific/Content/Skins/Elements";

        [MenuItem("hp55games/Blockout/Import Periodic Table Elements")]
        public static void Import()
        {
            var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonAssetPath);
            if (jsonAsset == null)
            {
                Debug.LogError($"[BlockoutPeriodicElementImporter] No JSON found at \"{JsonAssetPath}\". Aborting.");
                return;
            }

            var file = JsonUtility.FromJson<ElementsFile>(jsonAsset.text);
            if (file?.elements == null || file.elements.Length == 0)
            {
                Debug.LogError("[BlockoutPeriodicElementImporter] JSON parsed but contains no elements. Aborting.");
                return;
            }

            EnsureOutputFolderExists();

            var meltBehaviour = FindMeltBehaviour();
            if (meltBehaviour == null)
            {
                Debug.LogWarning("[BlockoutPeriodicElementImporter] No PeriodicMeltClearBehaviour asset found in the project - imported skins will have no clear behaviour assigned. Create one (hp55games/Blockout/Clear Behaviours/Periodic Melt) and re-run the import once it exists.");
            }

            int created = 0, updated = 0, skipped = 0;

            foreach (var element in file.elements)
            {
                if (!TryParseColor(element, out var color))
                {
                    skipped++;
                    continue;
                }

                string path = AssetPathFor(element);
                var skin = AssetDatabase.LoadAssetAtPath<BlockoutSkin>(path);
                bool isNew = skin == null;
                if (isNew) skin = ScriptableObject.CreateInstance<BlockoutSkin>();

                var unlockMethod = ParseUnlockMethod(element);
                skin.EditorConfigureElement(
                    skinId: SkinIdFor(element),
                    displayName: element.nameIt,
                    // unlockCostCoins is only meaningful (and only reliably non-null in the JSON)
                    // for the "coins" method - Default (Carbon) is always free, Achievement is
                    // never coin-purchasable, so both stay at CostNotSetValue regardless of
                    // whatever JsonUtility left in element.unlockCostCoins for a JSON null there.
                    costInCoins: unlockMethod == BlockoutSkinUnlockMethod.Coins ? element.unlockCostCoins : BlockoutSkin.CostNotSetValue,
                    unlockedByDefault: unlockMethod == BlockoutSkinUnlockMethod.Default,
                    pieceColor: color,
                    clearBehaviour: meltBehaviour,
                    elementSymbol: element.symbol,
                    atomicNumber: element.atomicNumber,
                    materialCategory: ParseCategory(element.shaderCategory),
                    densityNormalized: element.densityNormalized,
                    isRadioactive: element.isRadioactive,
                    unlockMethod: unlockMethod,
                    unlockAchievementId: unlockMethod == BlockoutSkinUnlockMethod.Achievement ? element.unlockAchievementId : string.Empty);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(skin, path);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(skin);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BlockoutPeriodicElementImporter] Import complete: {created} created, {updated} updated, {skipped} skipped (unparsable color) - {file.elements.Length} total elements in the JSON. Re-run the ConfigCatalog scan (Scan Content folders) so the shop query picks up any newly created assets.");
        }

        private static void EnsureOutputFolderExists()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder)) return;

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.ImportAsset(OutputFolder);
        }

        private static PeriodicMeltClearBehaviour FindMeltBehaviour()
        {
            var guids = AssetDatabase.FindAssets("t:PeriodicMeltClearBehaviour");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<PeriodicMeltClearBehaviour>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static bool TryParseColor(ElementDto element, out Color color)
        {
            if (ColorUtility.TryParseHtmlString(element.colorHex, out color)) return true;

            Debug.LogWarning($"[BlockoutPeriodicElementImporter] Element {element.atomicNumber} ({element.symbol}): unparsable colorHex \"{element.colorHex}\" - skipped.");
            return false;
        }

        private static string SkinIdFor(ElementDto element) => $"element-{element.atomicNumber}";

        private static string AssetPathFor(ElementDto element) =>
            $"{OutputFolder}/BlockoutSkin_Element_{element.atomicNumber:000}_{element.symbol}.asset";

        private static PieceMaterialCategory ParseCategory(string shaderCategory)
        {
            switch (shaderCategory)
            {
                case "metallic": return PieceMaterialCategory.Metallic;
                case "translucent": return PieceMaterialCategory.Translucent;
                case "opaque": return PieceMaterialCategory.Opaque;
                default:
                    Debug.LogWarning($"[BlockoutPeriodicElementImporter] Unrecognized shaderCategory \"{shaderCategory}\" - defaulting to Opaque.");
                    return PieceMaterialCategory.Opaque;
            }
        }

        // Shop Technical Doc Phase 1: unlockCostCoins is only trustworthy (and only ever
        // non-null in the JSON) when unlockMethod is "coins" - Default (Carbon) is explicitly 0
        // there, Achievement entries are explicitly null (JsonUtility's behavior for a JSON null
        // landing on ElementDto's non-nullable int is irrelevant precisely because the caller
        // never reads unlockCostCoins for those two methods). A malformed/unrecognized
        // unlockMethod falls back to Achievement (with no achievement id) rather than Coins -
        // that leaves the element simply unobtainable until the data is fixed, instead of
        // accidentally free or accidentally priced from whatever garbage ended up in the field.
        private static BlockoutSkinUnlockMethod ParseUnlockMethod(ElementDto element)
        {
            switch (element.unlockMethod)
            {
                case "default": return BlockoutSkinUnlockMethod.Default;
                case "coins": return BlockoutSkinUnlockMethod.Coins;
                case "achievement": return BlockoutSkinUnlockMethod.Achievement;
                default:
                    Debug.LogWarning($"[BlockoutPeriodicElementImporter] Element {element.atomicNumber} ({element.symbol}): unrecognized unlockMethod \"{element.unlockMethod}\" - treated as Achievement with no achievement id (unobtainable until fixed) rather than risking an accidental free/priced unlock.");
                    return BlockoutSkinUnlockMethod.Achievement;
            }
        }

        [Serializable]
        private class ElementsFile
        {
            public ElementDto[] elements;
        }

        // Only the fields the importer actually consumes - JsonUtility silently ignores JSON
        // keys with no matching field here (densityGCm3, densityIsPredicted, stateAtRoomTemp,
        // sourceNote, schemaVersion, description, discoveryYear, knownSinceAntiquity), so the DTO
        // doesn't need to mirror the full schema. discoveryYear/knownSinceAntiquity aren't needed
        // separately: for a "coins" element unlockCostCoins already equals discoveryYear, and
        // knownSinceAntiquity is implied by unlockMethod == "achievement".
        [Serializable]
        private class ElementDto
        {
            public int atomicNumber;
            public string symbol;
            public string nameIt;
            public float densityNormalized;
            public bool isRadioactive;
            public string shaderCategory;
            public string colorHex;
            public int unlockCostCoins;
            public string unlockMethod;
            public string unlockAchievementId;
        }
    }
}
