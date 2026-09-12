using hp55games.Mobile.Core.Architecture;

namespace hp55games.Blockout.Gameplay.Events
{
    public sealed class FallIntervalChangedEvent : IEvent
    {
        public float NewInterval;
    }
}
