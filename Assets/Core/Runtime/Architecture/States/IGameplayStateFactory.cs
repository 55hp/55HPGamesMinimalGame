namespace hp55games.Mobile.Core.Architecture.States
{
    /// <summary>
    /// Lets a specific game override which IGameplayState fills the FSM's "gameplay" slot,
    /// without Core.Runtime ever depending on a game-specific assembly (Game.Features depends on
    /// Core.Runtime, never the reverse - a direct reference the other way would be circular).
    /// Register an implementation via ServiceRegistry before SceneFlowService.GoToGameplayAsync
    /// is first called; if none is registered, SceneFlowService falls back to the template's own
    /// GameplayState.
    /// </summary>
    public interface IGameplayStateFactory
    {
        IGameplayState Create(bool isResuming);
    }
}
