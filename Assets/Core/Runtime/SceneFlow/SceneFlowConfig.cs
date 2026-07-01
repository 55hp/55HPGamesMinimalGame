using UnityEngine;

namespace hp55games.Mobile.Core.SceneFlow
{
    [CreateAssetMenu(
        fileName = "SceneFlowConfig",
        menuName  = "hp55games/Core/Scene Flow Config")]
    public sealed class SceneFlowConfig : ScriptableObject, ISceneFlowConfig
    {
        [SerializeField] private string _bootstrapScene = "00_Bootstrap";
        [SerializeField] private string _menuScene      = "01_Menu";
        [SerializeField] private string _gameplayScene  = "02_Gameplay";
        [SerializeField] private string _resultsScene   = "03_Results";

        public string BootstrapScene => _bootstrapScene;
        public string MenuScene      => _menuScene;
        public string GameplayScene  => _gameplayScene;
        public string ResultsScene   => _resultsScene;
    }
}
