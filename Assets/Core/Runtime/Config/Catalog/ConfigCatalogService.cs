using System.Collections.Generic;
using hp55games.Mobile.Core.Config;
using UnityEngine;

namespace hp55games.Mobile.Core.Architecture
{
    /// <summary>
    /// Dispatches the gameplay configs collected in a ConfigCatalog, for typed access by
    /// consumers without per-component wiring. Distinct from IConfigService (app-level
    /// GameConfig via Addressables): this covers gameplay ScriptableObjects that implement
    /// IConfigAsset.
    /// </summary>
    public interface IConfigCatalogService
    {
        /// <summary>The single config of type T, or null (with a logged error) if absent.</summary>
        T Get<T>() where T : ScriptableObject, IConfigAsset;

        /// <summary>All configs of type T. Empty list if none.</summary>
        IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IConfigAsset;
    }

    public sealed class ConfigCatalogService : IConfigCatalogService
    {
        private readonly ConfigCatalog _catalog;

        public ConfigCatalogService(ConfigCatalog catalog)
        {
            _catalog = catalog;
            if (_catalog == null)
                Debug.LogError("[ConfigCatalogService] Catalog is null on construction. Assign a ConfigCatalog to ConfigCatalogInstaller.");
        }

        public T Get<T>() where T : ScriptableObject, IConfigAsset
            => _catalog != null ? _catalog.Get<T>() : null;

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IConfigAsset
            => _catalog != null ? _catalog.GetAll<T>() : new List<T>();
    }
}
