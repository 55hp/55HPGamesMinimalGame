using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.InputSystem;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.InputSystem
{
    // Turns raw IInputService gestures into Blockout gameplay-request events. Not owned by
    // BlockoutGameplayState (input should keep working independent of gameplay-state lifecycle),
    // but relies on the same GameBootstrap -> Menu -> BlockoutGameplayState path for its
    // dependencies to already be registered by the time it runs.
    public sealed class BlockoutInputHandler : MonoBehaviour
    {
        [Tooltip("Camera used to project taps into the well. Defaults to Camera.main if left empty.")]
        [SerializeField] private Camera _camera;

        [Tooltip("World-space origin of the well's (0,0,0) grid cell. Defaults to world origin, matching BlockoutSpawner's convention, if left empty.")]
        [SerializeField] private Transform _wellOrigin;

        // A tap this close to a piece cube's edge still counts as "inside" the well - matches the
        // half-unit each placeholder cube physically occupies around its integer grid position.
        private const float CellHalfExtent = 0.5f;

        // Double tap must land within this window to count as one gesture; a lone tap only
        // resolves into a move once this window passes without a second tap.
        private const float DoubleTapWindowSeconds = 0.3f;

        private IInputService _input;
        private IEventBus _eventBus;
        private BlockoutSpawner _spawner;

        private bool _tapPending;
        private float _tapPendingSince;
        private Vector2 _tapPendingScreenPosition;

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

            _input.Tap += HandleTap;
            _input.Swipe += HandleSwipe;
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.Tap -= HandleTap;
                _input.Swipe -= HandleSwipe;
            }
        }

        private void Update()
        {
            // A pending tap resolves into a move only once the double-tap window has passed
            // without a second tap - see HandleTap.
            if (_tapPending && Time.unscaledTime - _tapPendingSince > DoubleTapWindowSeconds)
            {
                _tapPending = false;

                // A tap has no directional delta to rotate by, so a tap landing on the piece's
                // own footprint is simply consumed with no effect - only a miss resolves into a
                // move. See 03_input_translation_raycast.md.
                if (TryResolveGestureAgainstPiece(_tapPendingScreenPosition, out bool hitPiece, out var direction) && !hitPiece)
                {
                    _eventBus.Publish(new PieceMoveRequestedEvent { Direction = direction });
                }
            }
        }

        private void HandleTap(Vector2 screenPosition)
        {
            if (_tapPending && Time.unscaledTime - _tapPendingSince <= DoubleTapWindowSeconds)
            {
                _tapPending = false;
                _eventBus.Publish(new HardDropRequestedEvent());
                return;
            }

            // Buffer this tap - it either becomes a move (if the window passes with no follow-up
            // tap) or gets consumed as a hard drop above.
            _tapPending = true;
            _tapPendingSince = Time.unscaledTime;
            _tapPendingScreenPosition = screenPosition;
        }

        private void HandleSwipe(Vector2 start, Vector2 end)
        {
            // Raycast-gated per 03_input_translation_raycast.md: a swipe starting on the active
            // piece rotates (existing logic below, unchanged); a swipe starting off the piece
            // translates instead. If the raycast can't resolve at all (no active piece / no
            // camera yet), fall back to the old unconditional-rotate behavior rather than
            // dropping the input.
            if (TryResolveGestureAgainstPiece(start, out bool hitPiece, out var direction) && !hitPiece)
            {
                _eventBus.Publish(new PieceMoveRequestedEvent { Direction = direction });
                return;
            }

            var delta = end - start;

            // Swipe left/right -> AxisA, swipe up/down -> AxisB (per spec). Which physical
            // PolycubeShape axis each of AxisA/AxisB actually rotates is decided in
            // PieceController (AxisAMapsTo / AxisBMapsTo) - a single swap point for Franci.
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = delta.x >= 0 ? 1 : -1 });
            }
            else
            {
                _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisB, Steps90 = delta.y >= 0 ? 1 : -1 });
            }
        }

        // Raycasts a screen point vertically (via the ground plane, camera-relative so it's
        // correct regardless of camera angle) against the active piece's occupied cells. A hit
        // means the point landed on the piece's own X/Z footprint; a miss returns the direction
        // from the piece's pivot (GridPosition - the shape's local-space anchor, per
        // PolycubeShape/PieceController) to the point instead - no dead zone, works from anywhere
        // on screen including edges. Returns false only when there's nothing to resolve against
        // (no camera, or no active piece - e.g. between spawns).
        private bool TryResolveGestureAgainstPiece(Vector2 screenPosition, out bool hitPiece, out MoveDirection direction)
        {
            hitPiece = false;
            direction = default;

            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null) return false;

            if (_spawner == null) _spawner = FindObjectOfType<BlockoutSpawner>();
            var piece = _spawner != null ? _spawner.CurrentPiece : null;
            if (piece == null) return false;

            var origin = _wellOrigin != null ? _wellOrigin.position : Vector3.zero;
            var plane = new Plane(Vector3.up, origin);
            var ray = camera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(ray, out float distance)) return false;

            var local = ray.GetPoint(distance) - origin;
            var gridPosition = piece.GridPosition;
            IReadOnlyList<Vector3Int> cells = piece.Shape.Cells;

            foreach (var cell in cells)
            {
                float cellX = gridPosition.x + cell.x;
                float cellZ = gridPosition.z + cell.z;

                // Same +/- CellHalfExtent footprint each cube physically occupies, as used
                // elsewhere in this file.
                if (local.x >= cellX - CellHalfExtent && local.x <= cellX + CellHalfExtent
                    && local.z >= cellZ - CellHalfExtent && local.z <= cellZ + CellHalfExtent)
                {
                    hitPiece = true;
                    break;
                }
            }

            if (hitPiece) return true;

            float dx = local.x - gridPosition.x;
            float dz = local.z - gridPosition.z;

            direction = Mathf.Abs(dx) >= Mathf.Abs(dz)
                ? (dx >= 0f ? MoveDirection.Right : MoveDirection.Left)
                : (dz >= 0f ? MoveDirection.Forward : MoveDirection.Back);

            return true;
        }
    }
}
