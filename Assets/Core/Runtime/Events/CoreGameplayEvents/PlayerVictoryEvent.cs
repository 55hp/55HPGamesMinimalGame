using hp55games.Mobile.Core.Architecture;

namespace hp55games.Mobile.Core.Gameplay.Events
{
    /// <summary>
    /// The player reached the run's win condition while still alive. Twin of
    /// PlayerDeathEvent: a game's GameplayState can subscribe to both and transition
    /// to Results, distinguishing the outcome (e.g. by Lives &gt; 0 vs Lives &lt;= 0).
    /// </summary>
    public struct PlayerVictoryEvent : IEvent { }
}
