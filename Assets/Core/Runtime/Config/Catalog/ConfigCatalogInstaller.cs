using hp55games.Mobile.Core.Architecture;
using UnityEngine;

namespace hp55games.Mobile.Core.Config
{
    /// <summary>
    /// Registers IConfigCatalogService in ServiceRegistry. Must sit in a scene that loads
    /// BEFORE its consumers (e.g. Bootstrap / 01_Menu): gameplay scripts that resolve the
    /// service in their own Awake() need the catalog already registered by then.
    /// </summary>
    public sealed class ConfigCatalogInstaller : MonoBehaviour
    {
        [Tooltip("The ConfigCatalog populated via scan. Drag the asset here.")]
        [SerializeField] private ConfigCatalog _catalog;

        private void Awake()
        {
            if (_catalog == null)
            {
                Debug.LogError("[ConfigCatalogInstaller] _catalog not assigned. Assign the ConfigCatalog in the Inspector.", this);
                return;
            }

            ServiceRegistry.Register<IConfigCatalogService>(new ConfigCatalogService(_catalog));
            Debug.Log("[Config] ConfigCatalogService registered.");
        }
    }
}
