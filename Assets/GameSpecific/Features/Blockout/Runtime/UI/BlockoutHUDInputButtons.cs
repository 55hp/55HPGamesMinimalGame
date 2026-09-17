using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.InputSystem;

namespace hp55games.Blockout.UI
{
    // Second, button-based way to trigger hard drop and the two rotation axes - additive to
    // BlockoutInputHandler's swipe-to-rotate and tap-outside-piece-to-move gestures, not a
    // replacement (hard drop's old double-tap gesture WAS removed - see BlockoutInputHandler).
    // Publishes the exact same IEventBus events BlockoutInputHandler already publishes, so
    // gameplay never sees a separate input path for button vs. gesture.
    //
    // Attached at runtime by BlockoutGameplayState.EnterAsync onto UIGameplayHUD's GameObject
    // (never scene/prefab-authored - see BlockoutWellCamera for the same AddComponent pattern),
    // so its 3 buttons are resolved via transform.Find at fixed child paths (Bezi's
    // BottomActionBar/BTN_RotateLeft, BTN_HardDrop, BTN_RotateRight) rather than
    // [SerializeField] references nobody can wire in the Inspector.
    public sealed class BlockoutHUDInputButtons : MonoBehaviour
    {
        private const string RotateLeftButtonPath = "BottomActionBar/BTN_RotateLeft";
        private const string HardDropButtonPath = "BottomActionBar/BTN_HardDrop";
        private const string RotateRightButtonPath = "BottomActionBar/BTN_RotateRight";

        private IEventBus _eventBus;
        private Button _rotateLeftButton;
        private Button _hardDropButton;
        private Button _rotateRightButton;

        private void Awake()
        {
            if (!ServiceRegistry.TryResolve(out _eventBus))
            {
                Debug.LogError("[BlockoutHUDInputButtons] IEventBus is not registered.", this);
                enabled = false;
                return;
            }

            _rotateLeftButton = FindButton(RotateLeftButtonPath);
            _hardDropButton = FindButton(HardDropButtonPath);
            _rotateRightButton = FindButton(RotateRightButtonPath);

            if (_rotateLeftButton != null) _rotateLeftButton.onClick.AddListener(OnRotateLeftClicked);
            if (_hardDropButton != null) _hardDropButton.onClick.AddListener(OnHardDropClicked);
            if (_rotateRightButton != null) _rotateRightButton.onClick.AddListener(OnRotateRightClicked);
        }

        private void OnDestroy()
        {
            if (_rotateLeftButton != null) _rotateLeftButton.onClick.RemoveListener(OnRotateLeftClicked);
            if (_hardDropButton != null) _hardDropButton.onClick.RemoveListener(OnHardDropClicked);
            if (_rotateRightButton != null) _rotateRightButton.onClick.RemoveListener(OnRotateRightClicked);
        }

        private Button FindButton(string path)
        {
            var found = transform.Find(path);
            if (found == null)
            {
                Debug.LogError($"[BlockoutHUDInputButtons] Button not found at '{path}' relative to {name}.", this);
                return null;
            }

            var button = found.GetComponent<Button>();
            if (button == null)
                Debug.LogError($"[BlockoutHUDInputButtons] '{path}' has no Button component.", this);

            return button;
        }

        // Fixed 90-degree steps, not direction-sensitive like the swipe gesture (which derives
        // its sign from swipe delta) - Steps90 = 1 for both by default, same physical step size
        // either way. Flip the sign here if Franci wants the opposite rotation direction after
        // playtesting.
        private void OnRotateLeftClicked() =>
            _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisA, Steps90 = 1 });

        private void OnRotateRightClicked() =>
            _eventBus.Publish(new PieceRotateRequestedEvent { Axis = RotateAxis.AxisB, Steps90 = 1 });

        private void OnHardDropClicked() =>
            _eventBus.Publish(new HardDropRequestedEvent());
    }
}
