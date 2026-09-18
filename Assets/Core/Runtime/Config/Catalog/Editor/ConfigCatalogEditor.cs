using System;
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
        // Groups above this size collapse behind a foldout by default (e.g. the 118+ per-element
        // BlockoutSkin assets) so a handful of singleton configs aren't buried under them. Generic
        // by type, not by name, so Core has no dependency on any GameSpecific config type.
        private const int CollapseThreshold = 5;

        private readonly Dictionary<Type, bool> _expanded = new();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var configsProp = serializedObject.FindProperty("_configs");

            var byType = new Dictionary<Type, List<int>>();
            for (int i = 0; i < configsProp.arraySize; i++)
            {
                var element = configsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                var type = element != null ? element.GetType() : typeof(ScriptableObject);
                if (!byType.TryGetValue(type, out var indices))
                    byType[type] = indices = new List<int>();
                indices.Add(i);
            }

            var types = new List<Type>(byType.Keys);
            types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            EditorGUILayout.LabelField($"{configsProp.arraySize} configs total", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            foreach (var type in types)
            {
                var indices = byType[type];

                if (indices.Count > CollapseThreshold)
                {
                    _expanded.TryGetValue(type, out bool isExpanded);
                    isExpanded = EditorGUILayout.Foldout(isExpanded, $"{type.Name} ({indices.Count}) - collapsed", true);
                    _expanded[type] = isExpanded;
                    if (!isExpanded) continue;

                    EditorGUI.indentLevel++;
                    foreach (int i in indices)
                        EditorGUILayout.PropertyField(configsProp.GetArrayElementAtIndex(i), GUIContent.none);
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField(type.Name, EditorStyles.miniBoldLabel);
                    foreach (int i in indices)
                        EditorGUILayout.PropertyField(configsProp.GetArrayElementAtIndex(i), GUIContent.none);
                }
            }

            serializedObject.ApplyModifiedProperties();

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
