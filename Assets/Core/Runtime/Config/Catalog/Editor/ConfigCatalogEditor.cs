using System.Collections.Generic;
using hp55games.Mobile.Core.Config;
using UnityEditor;
using UnityEngine;

namespace hp55games.Mobile.Core.Config.EditorTools
{
    /// <summary>
    /// Inspector for ConfigCatalog with an automatic scan button. Collects every
    /// ScriptableObject implementing IConfigAsset found under a "Content" folder (any
    /// depth: the filter is on the path containing "/Content/") and replaces the catalog's
    /// content. The ConfigCatalog itself is excluded.
    /// </summary>
    [CustomEditor(typeof(ConfigCatalog))]
    public sealed class ConfigCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "The scan looks for every IConfigAsset under 'Content' folders and subfolders, " +
                "and replaces the list above. Re-run it after adding or moving a config.",
                MessageType.Info);

            if (GUILayout.Button("Scan Content folders"))
                Scan((ConfigCatalog)target);
        }

        private static void Scan(ConfigCatalog catalog)
        {
            var found = new List<ScriptableObject>();
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Content/")) continue;

                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;
                if (so is ConfigCatalog) continue;      // don't aggregate itself
                if (so is IConfigAsset)
                    found.Add(so);
            }

            found.Sort((a, b) => string.CompareOrdinal(a.GetType().Name, b.GetType().Name));

            Undo.RecordObject(catalog, "Scan Config Catalog");
            catalog.EditorSetConfigs(found);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ConfigCatalog] Scan complete: {found.Count} configs found under Content folders.", catalog);
        }
    }
}
