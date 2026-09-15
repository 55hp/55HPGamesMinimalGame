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
        private const int CarbonAtomicNumber = 6; // GDD: always-unlocked, zero-cost default element

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

                bool isCarbon = element.atomicNumber == CarbonAtomicNumber;
                skin.EditorConfigureElement(
                    skinId: SkinIdFor(element),
                    displayName: element.nameIt,
                    costInCoins: isCarbon ? 0 : BlockoutSkin.CostNotSetValue,
                    unlockedByDefault: isCarbon,
                    pieceColor: color,
                    clearBehaviour: meltBehaviour,
                    elementSymbol: element.symbol,
                    atomicNumber: element.atomicNumber,
                    materialCategory: ParseCategory(element.shaderCategory),
                    densityNormalized: element.densityNormalized);

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

        [Serializable]
        private class ElementsFile
        {
            public ElementDto[] elements;
        }

        // Only the fields the importer actually consumes - JsonUtility silently ignores JSON
        // keys with no matching field here (densityGCm3, densityIsPredicted, stateAtRoomTemp,
        // sourceNote, schemaVersion, description, unlockCostCoins), so the DTO doesn't need to
        // mirror the full schema. unlockCostCoins in particular is deliberately not read: it's
        // null for every element today (GDD: cost formula is an explicit open item), and
        // JsonUtility can't parse a JSON null into a non-nullable int anyway - every imported
        // skin's cost is set from CarbonAtomicNumber/BlockoutSkin.CostNotSetValue instead.
        [Serializable]
        private class ElementDto
        {
            public int atomicNumber;
            public string symbol;
            public string nameIt;
            public float densityNormalized;
            public string shaderCategory;
            public string colorHex;
        }
    }
}
