namespace hp55games.Mobile.Core.Architecture.States
{
    /// <summary>
    /// Empty marker: identifies whichever IGameState currently occupies the FSM's "gameplay"
    /// slot, regardless of which concrete game owns it. Lets SceneFlowService's pause/resume
    /// logic recognize "the gameplay state" without depending on any specific game's state type.
    /// </summary>
    public interface IGameplayState : IGameState
    {
    }
}
