namespace hp55games.Mobile.Core.SceneFlow
{
    public interface ISceneFlowConfig
    {
        string BootstrapScene { get; }
        string MenuScene      { get; }
        string GameplayScene  { get; }
        string ResultsScene   { get; }
    }
}
