using hp55games.Mobile.Core.Architecture;

// Namespace is "InputSystem", not "Input" (matching hp55games.Mobile.Core.InputSystem's own
// convention), to avoid colliding with UnityEngine.Input in any file that imports both.
namespace hp55games.Blockout.InputSystem
{
    public enum MoveDirection { Left, Right, Forward, Back } // maps to well X/Z edges

    public sealed class PieceMoveRequestedEvent : IEvent
    {
        public MoveDirection Direction;
    }
}
