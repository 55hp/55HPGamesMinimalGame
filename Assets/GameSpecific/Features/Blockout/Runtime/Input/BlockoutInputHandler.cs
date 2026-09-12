using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.InputSystem;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.InputSystem
{
    // Standalone wiring, same spirit as BlockoutSpawner: resolves its own dependencies and turns
    // raw IInputService gestures into Blockout gameplay-request events. Not driven by
    // BlockoutGameplayState or the FSM - delete/fold in once the real game state exists.
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
        private int _wellWidth;
        private int _wellDepth;
        private bool _ownsInputTick;

        private bool _tapPending;
        private float _tapPendingSince;
        private Vector2 _tapPendingScreenPosition;

        private void Awake()
        {
            // Standalone test scene (no Bootstrap flow): provide and tick our own IInputService /
            // IEventBus. In the real app flow GameBootstrap + InputServiceDriver already do both,
            // so this is a no-op there (existing instances are left alone, not replaced).
            if (!ServiceRegistry.TryResolve(out _input))
            {
                _input = new InputService();
                ServiceRegistry.Register<IInputService>(_input);
                _ownsInputTick = true;
                Debug.Log("[BlockoutInputHandler] No IInputService registered - created and driving a standalone instance for this scene.", this);
            }

            if (!ServiceRegistry.TryResolve(out _eventBus))
            {
                _eventBus = new EventBus();
                ServiceRegistry.Register<IEventBus>(_eventBus);
                Debug.Log("[BlockoutInputHandler] No IEventBus registered - created a standalone instance for this scene.", this);
            }

            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalogService))
            {
                Debug.LogError("[BlockoutInputHandler] IConfigCatalogService is not registered - add a ConfigCatalogInstaller (with a populated ConfigCatalog) to the scene.", this);
                enabled = false;
                return;
            }

            var wellConfig = catalogService.Get<BlockoutWellConfig>();
            if (wellConfig == null)
            {
                Debug.LogError("[BlockoutInputHandler] No BlockoutWellConfig found in the catalog.", this);
                enabled = false;
                return;
            }

            _wellWidth = wellConfig.Width;
            _wellDepth = wellConfig.Depth;

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
            if (_ownsInputTick) _input.Tick(Time.unscaledDeltaTime);

            // A pending tap resolves into a move only once the double-tap window has passed
            // without a second tap - see HandleTap.
            if (_tapPending && Time.unscaledTime - _tapPendingSince > DoubleTapWindowSeconds)
            {
                _tapPending = false;
                if (TryResolveMoveDirection(_tapPendingScreenPosition, out var direction))
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

        // Camera-relative by construction: raycasts the tap through the actual camera into world
        // space and compares against the well's real world bounds, so the result is correct
        // regardless of camera angle - never inferred from raw screen-space halves/quadrants.
        private bool TryResolveMoveDirection(Vector2 screenPosition, out MoveDirection direction)
        {
            direction = default;

            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null) return false;

            var origin = _wellOrigin != null ? _wellOrigin.position : Vector3.zero;
            var plane = new Plane(Vector3.up, origin);
            var ray = camera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(ray, out float distance)) return false;

            var local = ray.GetPoint(distance) - origin;

            // The well's cubes sit at integer grid coordinates [0, width) x [0, depth), each
            // physically occupying +/- CellHalfExtent - see BlockoutSpawner.CenteredTopStart.
            float minX = -CellHalfExtent;
            float maxX = _wellWidth - 1 + CellHalfExtent;
            float minZ = -CellHalfExtent;
            float maxZ = _wellDepth - 1 + CellHalfExtent;

            float xOvershoot = local.x < minX ? minX - local.x : local.x > maxX ? local.x - maxX : 0f;
            float zOvershoot = local.z < minZ ? minZ - local.z : local.z > maxZ ? local.z - maxZ : 0f;

            if (xOvershoot <= 0f && zOvershoot <= 0f) return false; // tap landed inside the well - no move

            if (xOvershoot >= zOvershoot)
                direction = local.x < minX ? MoveDirection.Left : MoveDirection.Right;
            else
                direction = local.z < minZ ? MoveDirection.Back : MoveDirection.Forward;

            return true;
        }
    }
}
