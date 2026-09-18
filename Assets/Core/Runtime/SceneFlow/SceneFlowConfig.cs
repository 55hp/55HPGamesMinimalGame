using UnityEngine;

namespace hp55games.Mobile.Core.SceneFlow
{
    [CreateAssetMenu(
        fileName = "SceneFlowConfig",
        menuName  = "hp55games/Core/Scene Flow Config")]
    public sealed class SceneFlowConfig : ScriptableObject, ISceneFlowConfig
    {
        [Header("Scene Names")]
        [Tooltip("Initial scene loaded at app startup, before the menu.")]
        [SerializeField] private string _bootstrapScene = "00_Bootstrap";
        [Tooltip("Main menu scene.")]
        [SerializeField] private string _menuScene      = "01_Menu";
        [Tooltip("Core gameplay scene.")]
        [SerializeField] private string _gameplayScene  = "02_Gameplay";
        [Tooltip("Post-game results scene.")]
        [SerializeField] private string _resultsScene   = "03_Results";

        public string BootstrapScene => _bootstrapScene;
        public string MenuScene      => _menuScene;
        public string GameplayScene  => _gameplayScene;
        public string ResultsScene   => _resultsScene;
    }
}
