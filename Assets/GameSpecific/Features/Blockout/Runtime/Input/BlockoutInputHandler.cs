using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.InputSystem;
using hp55games.Blockout.Gameplay;

namespace hp55games.Blockout.InputSystem
{
    // Turns raw IInputService gestures into Blockout gameplay-request events: tap -> move
    // (by touched quadrant relative to the piece), swipe -> rotate, long-press on the piece -> hard drop. Not owned by
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

        private IInputService _input;
        private IEventBus _eventBus;
        private BlockoutSpawner _spawner;

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
            _input.LongPress += HandleLongPress;
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.Tap -= HandleTap;
                _input.Swipe -= HandleSwipe;
                _input.LongPress -= HandleLongPress;
            }
        }

        private void HandleTap(Vector2 screenPosition)
        {
            // A tap moves the piece toward the side of it that was touched (raycast-resolved, see
            // TryResolveTapAgainstPiece). A tap landing on the piece's own footprint is simply
            // consumed with no effect.
            if (!TryResolveTapAgainstPiece(screenPosition, out bool hitPiece, out var direction) || hitPiece)
                return;

            _eventBus.Publish(new PieceMoveRequestedEvent { Direction = direction });
        }

        private void HandleSwipe(Vector2 start, Vector2 end)
        {
            // Direct screen-delta mapping, independent of the piece's on-screen position or the
            // well's geometry: the swipe's dominant screen direction rotates the piece -
            // Right/Left -> AxisA +1/-1, Forward/Back -> AxisB +1/-1 (same sign convention as the
            // buttons: positive screen direction -> positive Steps90). No raycast needed.
            var delta = end - start;
            var direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? MoveDirection.Right : MoveDirection.Left)
                : (delta.y >= 0f ? MoveDirection.Forward : MoveDirection.Back);

            var axis = direction == MoveDirection.Right || direction == MoveDirection.Left
                ? RotateAxis.AxisA
                : RotateAxis.AxisB;
            int steps = direction == MoveDirection.Right || direction == MoveDirection.Forward ? 1 : -1;

            _eventBus.Publish(new PieceRotateRequestedEvent { Axis = axis, Steps90 = steps });
        }

        private void HandleLongPress(Vector2 screenPosition)
        {
            // Hard drop only when the press is on the piece's footprint; same event as BTN_HardDrop.
            // InputService already suppresses Tap/Swipe on release once a long-press has fired.
            if (!TryResolveTapAgainstPiece(screenPosition, out bool hitPiece, out _) || !hitPiece)
                return;

            _eventBus.Publish(new HardDropRequestedEvent());
        }

        // Raycasts a screen point (camera-relative so it's correct regardless of camera
        // position/angle) against the active piece's occupied cells, intersected at the piece's
        // own height - not the well floor, since the camera isn't orthographic: the same screen
        // ray hits different world X/Z depending on which height it's projected onto, so a
        // floor-only intersection was systematically off for any piece not already on the floor
        // (i.e. most of the time, since pieces start near the well's top and fall). A hit means
        // the point landed on the piece's own X/Z footprint; a miss returns the direction from the
        // piece's pivot (GridPosition - the shape's local-space anchor, per
        // PolycubeShape/PieceController) to the point instead - no dead zone, works from anywhere
        // on screen including edges. HandleTap (a tap has no delta of its own to resolve a
        // direction from) and HandleLongPress (on-piece hit test only) use this - HandleSwipe
        // resolves its direction straight from the swipe's screen delta instead. Returns false only when there's nothing to resolve against (no
        // camera, or no active piece - e.g. between spawns).
        private bool TryResolveTapAgainstPiece(Vector2 screenPosition, out bool hitPiece, out MoveDirection direction)
        {
            hitPiece = false;
            direction = default;

            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null) return false;

            // Resolved lazily via ServiceRegistry (BlockoutSpawner.Awake registers itself), not
            // FindObjectOfType (README §0 rule 2) - kept lazy (first raycast, not this
            // component's own Awake()) since Awake() order between two scene-authored objects in
            // the same scene load isn't guaranteed; by the time a gesture actually happens, both
            // are long since initialized.
            if (_spawner == null) ServiceRegistry.TryResolve(out _spawner);
            var piece = _spawner != null ? _spawner.CurrentPiece : null;
            if (piece == null) return false;

            var gridPosition = piece.GridPosition;
            var origin = _wellOrigin != null ? _wellOrigin.position : Vector3.zero;
            var pieceWorldPosition = new Vector3(origin.x, origin.y + gridPosition.y, origin.z);
            var plane = new Plane(Vector3.up, pieceWorldPosition);
            var ray = camera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(ray, out float distance)) return false;

            var local = ray.GetPoint(distance) - origin;
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
