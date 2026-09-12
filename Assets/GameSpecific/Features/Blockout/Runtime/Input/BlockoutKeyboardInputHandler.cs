using UnityEngine;
using hp55games.Mobile.Core.Architecture;

namespace hp55games.Blockout.InputSystem
{
    // Editor/debug-only keyboard alternative to BlockoutInputHandler's touch gestures - publishes
    // the exact same events, so PieceController needs no changes and both can run side by side.
    // Not meant for device builds; harmless there since a touch device simply never sends key
    // events, but this is standalone temp wiring like the rest of Blockout's input, not something
    // that belongs in a shipped build - delete/gate behind an editor check once that matters.
    //
    // Move: arrow keys. Rotate: Q/E drive AxisA, Z/X drive AxisB (matches the swipe-based
    // left/right -> AxisA, up/down -> AxisB split in BlockoutInputHandler). Hard drop: Space.
    public sealed class BlockoutKeyboardInputHandler : MonoBehaviour
    {
        private IEventBus _eventBus;

        private void Awake()
        {
            // Same standalone-scene fallback as BlockoutInputHandler: create one if the real app
            // flow hasn't registered one yet. Harmless either way since Register just overwrites,
            // and this keeps the two input handlers independent of which one Awakes first.
            if (!ServiceRegistry.TryResolve(out _eventBus))
            {
                _eventBus = new EventBus();
                ServiceRegistry.Register<IEventBus>(_eventBus);
                Debug.Log("[BlockoutKeyboardInputHandler] No IEventBus registered - created a standalone instance for this scene.", this);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                _eventBus.Publish(new PieceMoveRequestedEvent { Direction = MoveDirection.Left });
            else if (Input.GetKeyDown(KeyCode.RightArrow))
                _eventBus.Publish(new PieceMoveRequestedEvent { Direction = MoveDirection.Right });
            else if (Input.GetKeyDown(KeyCode.UpArrow))
                _eventBus.Publish(new PieceMoveRequestedEvent { Direction = MoveDirection.Forward });
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                _eventBus.Publish(new PieceMoveRequestedEvent { Direction = MoveDirection.Back });

            if (Input.GetKeyDown(KeyCode.Q))
                _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = -1 });
            else if (Input.GetKeyDown(KeyCode.E))
                _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });

            if (Input.GetKeyDown(KeyCode.Z))
                _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisB, Steps90 = -1 });
            else if (Input.GetKeyDown(KeyCode.X))
                _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisB, Steps90 = 1 });

            if (Input.GetKeyDown(KeyCode.Space))
                _eventBus.Publish(new HardDropRequestedEvent());
        }
    }
}
