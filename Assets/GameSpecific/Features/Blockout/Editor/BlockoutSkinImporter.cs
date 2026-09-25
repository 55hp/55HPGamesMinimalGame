using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.Config.EditorTools
{
    // Generates/refreshes the authored BlockoutSkin assets from blockout_skins.json (replaces
    // BlockoutPeriodicElementImporter in role). Safe to re-run: an existing asset for a given
    // skinId is updated in place (found by its deterministic path - see AssetPathFor), so its
    // GUID and any SkinId already in someone's save data survive a re-import.
    public static class BlockoutSkinImporter
    {
        private const string JsonAssetPath = "Assets/GameSpecific/Content/Skins/blockout_skins.json";
        private const string OutputFolder = "Assets/GameSpecific/Content/Skins/Skins";
        private const string ClearBehaviourPath = "Assets/GameSpecific/Content/Skins/NeutralClearBehaviour.asset";

        [MenuItem("hp55games/Blockout/Import Skins")]
        public static void Import()
        {
            var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonAssetPath);
            if (jsonAsset == null)
            {
                Debug.LogError($"[BlockoutSkinImporter] No JSON found at \"{JsonAssetPath}\". Aborting.");
                return;
            }

            var file = JsonUtility.FromJson<SkinsFile>(jsonAsset.text);
            if (file?.skins == null || file.skins.Length == 0)
            {
                Debug.LogError("[BlockoutSkinImporter] JSON parsed but contains no skins. Aborting.");
                return;
            }

            var clearBehaviour = AssetDatabase.LoadAssetAtPath<NeutralClearBehaviour>(ClearBehaviourPath);
            if (clearBehaviour == null)
            {
                Debug.LogWarning($"[BlockoutSkinImporter] No NeutralClearBehaviour at \"{ClearBehaviourPath}\" - imported skins will have no clear behaviour assigned.");
            }

            EnsureOutputFolderExists();

            int created = 0, updated = 0, skipped = 0;

            foreach (var entry in file.skins)
            {
                if (string.IsNullOrEmpty(entry.skinId))
                {
                    Debug.LogWarning("[BlockoutSkinImporter] Skin entry with an empty skinId - skipped.");
                    skipped++;
                    continue;
                }

                if (!ColorUtility.TryParseHtmlString(entry.baseColorHex, out var baseColor))
                {
                    Debug.LogWarning($"[BlockoutSkinImporter] Skin \"{entry.skinId}\": unparsable baseColorHex \"{entry.baseColorHex}\" - skipped.");
                    skipped++;
                    continue;
                }

                string path = AssetPathFor(entry);
                var skin = AssetDatabase.LoadAssetAtPath<BlockoutSkin>(path);
                bool isNew = skin == null;
                if (isNew) skin = ScriptableObject.CreateInstance<BlockoutSkin>();

                var unlockMethod = ParseUnlockMethod(entry);
                skin.EditorConfigureSkin(
                    skinId: entry.skinId,
                    displayName: entry.displayName,
                    // Only a "coins" skin has a meaningful price - the default one is free.
                    costInCoins: unlockMethod == BlockoutSkinUnlockMethod.Coins ? entry.unlockCostCoins : 0,
                    unlockedByDefault: entry.unlockedByDefault,
                    unlockMethod: unlockMethod,
                    baseColor: baseColor,
                    metallic: entry.metallic,
                    smoothness: entry.smoothness,
                    emissionIntensity: entry.emissionIntensity,
                    clearBehaviour: clearBehaviour);

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

            Debug.Log($"[BlockoutSkinImporter] Import complete: {created} created, {updated} updated, {skipped} skipped - {file.skins.Length} total skins in the JSON. Re-run the ConfigCatalog scan (Scan Content folders) so the catalog picks up any newly created assets.");
        }

        private static void EnsureOutputFolderExists()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder)) return;

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.ImportAsset(OutputFolder);
        }

        private static string AssetPathFor(SkinDto entry) => $"{OutputFolder}/BlockoutSkin_{entry.skinId}.asset";

        // Unrecognized values fall back to Coins (a priced, purchasable skin) and are logged -
        // the JSON only ever uses "default" (terra) and "coins".
        private static BlockoutSkinUnlockMethod ParseUnlockMethod(SkinDto entry)
        {
            switch (entry.unlockMethod)
            {
                case "default": return BlockoutSkinUnlockMethod.Default;
                case "coins": return BlockoutSkinUnlockMethod.Coins;
                default:
                    Debug.LogWarning($"[BlockoutSkinImporter] Skin \"{entry.skinId}\": unrecognized unlockMethod \"{entry.unlockMethod}\" - treated as coins.");
                    return BlockoutSkinUnlockMethod.Coins;
            }
        }

        [Serializable]
        private class SkinsFile
        {
            public SkinDto[] skins;
        }

        [Serializable]
        private class SkinDto
        {
            public string skinId;
            public string displayName;
            public string baseColorHex;
            public float metallic;
            public float smoothness;
            public float emissionIntensity;
            public int unlockCostCoins;
            public string unlockMethod;
            public bool unlockedByDefault;
        }
    }
}
