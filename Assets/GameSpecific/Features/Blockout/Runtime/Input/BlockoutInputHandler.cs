using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.InputSystem;

namespace hp55games.Blockout.InputSystem
{
    // Turns raw IInputService gestures into Blockout gameplay-request events. Final scheme:
    // swipe = translation; rotations and hard drop are HUD buttons only (UIBlockoutHUD). Tap and
    // long-press are deliberately unused - they do nothing anywhere. Not owned by
    // BlockoutGameplayState (input should keep working independent of gameplay-state lifecycle),
    // but relies on the same GameBootstrap -> Menu -> BlockoutGameplayState path for its
    // dependencies to already be registered by the time it runs.
    public sealed class BlockoutInputHandler : MonoBehaviour
    {
        private IInputService _input;
        private IEventBus _eventBus;

        private void Awake()
        {
            // Gameplay is only ever reached via GameBootstrap -> Menu -> BlockoutGameplayState
            // (confirmed as of Phase 4), which registers IInputService/IEventBus long before this
            // component's Awake() can run - both are hard requirements now, not a "might be
            // missing in a standalone scene" case to self-provision a fallback for.
            if (!ServiceRegistry.TryResolve(out _input))
            {
                Debug.LogError("[BlockoutInputHandler] IInputService is not registered.", this);
                enabled = false;
                return;
            }

            if (!ServiceRegistry.TryResolve(out _eventBus))
            {
                Debug.LogError("[BlockoutInputHandler] IEventBus is not registered.", this);
                enabled = false;
                return;
            }

            _input.Swipe += HandleSwipe;
        }

        private void OnDestroy()
        {
            if (_input != null)
                _input.Swipe -= HandleSwipe;
        }

        private void HandleSwipe(Vector2 start, Vector2 end)
        {
            // Direct screen-delta mapping, independent of the piece's on-screen position or the
            // well's geometry: swipe up/right/down/left moves the piece in that same screen
            // direction, always - on-piece and off-piece swipes behave identically. No raycast
            // needed for this.
            var delta = end - start;
            var direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? MoveDirection.Right : MoveDirection.Left)
                : (delta.y >= 0f ? MoveDirection.Forward : MoveDirection.Back);

            _eventBus.Publish(new PieceMoveRequestedEvent { Direction = direction });
        }
    }
}
