using hp55games.Mobile.Core.Architecture;

namespace hp55games.Blockout.Gameplay.Events
{
    public sealed class LayersClearedEvent : IEvent
    {
        public int LayerCount;
        public int PointsAwarded;
    }
}
