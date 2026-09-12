using hp55games.Mobile.Core.Architecture;

namespace hp55games.Blockout.InputSystem
{
    // Confirmed 2026-08-14 (Technical Doc): exactly 2 controllable axes - any polycube
    // orientation is reachable by composing rotations on these two, no third axis needed.
    public enum RotateAxis { AxisA, AxisB }

    public sealed class PieceRotateRequestedEvent : IEvent
    {
        public RotateAxis Axis;
        public int Steps90;
    }
}
