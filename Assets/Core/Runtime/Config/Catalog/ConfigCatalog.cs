using System.Collections.Generic;
using UnityEngine;

namespace hp55games.Mobile.Core.Config
{
    /// <summary>
    /// Aggregating container for all gameplay configs (IConfigAsset) in the project.
    /// Populated in the editor by the scan button (ConfigCatalogEditor), which collects
    /// every IConfigAsset found under "Content" folders and subfolders. Read-only at
    /// runtime: the installer registers it and consumers query it via IConfigCatalogService.
    ///
    /// Get&lt;T&gt;() assumes a single asset per type (singleton configs: MapGenerationConfig,
    /// SurvivalConfig, ...). For multi-instance types (e.g. one LevelConfig per mission) use
    /// GetAll&lt;T&gt;(): Get&lt;T&gt;() logs a warning and returns the first if more than one is found.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Config Catalog", fileName = "ConfigCatalog")]
    public sealed class ConfigCatalog : ScriptableObject
    {
        [Tooltip("Populated by the 'Scan Content folders' button. Don't edit by hand except for targeted removals.")]
        [SerializeField] private List<ScriptableObject> _configs = new();

        /// <summary>
        /// Returns the single config of type T. Logs an error and returns null if none is
        /// present; logs a warning and returns the first if more than one is present (avoid
        /// for singleton types — see GetAll for multi-instance types).
        /// </summary>
        public T Get<T>() where T : ScriptableObject, IConfigAsset
        {
            T found = null;
            int count = 0;

            foreach (var config in _configs)
            {
                if (config is T match)
                {
                    if (found == null) found = match;
                    count++;
                }
            }

            if (found == null)
                Debug.LogError($"[ConfigCatalog] No config of type {typeof(T).Name} in the catalog. Run the scan and verify the asset lives under a Content folder.", this);
            else if (count > 1)
                Debug.LogWarning($"[ConfigCatalog] {count} configs of type {typeof(T).Name} in the catalog: Get<{typeof(T).Name}>() returns the first. Use GetAll if the type is multi-instance.", this);

            return found;
        }

        /// <summary>
        /// All configs of type T. Empty list (never null) if none are present.
        /// </summary>
        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IConfigAsset
        {
            var result = new List<T>();
            foreach (var config in _configs)
            {
                if (config is T match)
                    result.Add(match);
            }
            return result;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Replaces the catalog's content. Editor scan only (ConfigCatalogEditor).
        /// </summary>
        public void EditorSetConfigs(List<ScriptableObject> configs)
        {
            _configs = configs;
        }
#endif
    }
}
