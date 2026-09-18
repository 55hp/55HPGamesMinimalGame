using UnityEngine;
using hp55games.Mobile.Core.Config;
using hp55games.Mobile.Core.SceneFlow;

namespace hp55games.Mobile.Core.Bootstrap
{
    /// <summary>
    /// Pins the project's top-level config assets to the Bootstrap scene for quick navigation
    /// in the Editor. Pure reference holder: nothing reads these fields at runtime, and how
    /// configs are loaded/consumed (Addressables, ConfigCatalog) is unchanged by this component.
    /// </summary>
    public sealed class BootstrapConfigReferences : MonoBehaviour
    {
        [SerializeField] private ConfigCatalog _configCatalog;
        [SerializeField] private GameConfig _gameConfig;
        [SerializeField] private SceneFlowConfig _sceneFlowConfig;
    }
}
