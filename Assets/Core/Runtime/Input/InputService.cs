using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace hp55games.Mobile.Core.InputSystem
{
    /// <summary>
    /// Default mobile/desktop implementation for IInputService.
    /// It supports single-pointer gestures: tap, swipe, hold.
    /// This class is ticked from a MonoBehaviour driver (InputServiceDriver).
    ///
    /// UI ownership: a press that BEGINS over a UI element (EventSystem.IsPointerOverGameObject
    /// — including a popup's full-screen scrim, since it's a raycastTarget Image) belongs to UI
    /// for its whole lifetime, not gameplay: no PointerDown/PointerUp/Tap/Swipe/Hold fires for it.
    /// Checked once at press-begin, not per-frame: if the gesture started on gameplay and dragged
    /// onto a button before release, it's still a gameplay gesture (and vice versa) — matches how
    /// uGUI itself decides pointer target on press, not release.
    /// </summary>
    public sealed class InputService : IInputService
    {
        public event Action<Vector2> PointerDown;
        public event Action<Vector2> PointerUp;
        public event Action<Vector2> Tap;
        public event Action<Vector2, Vector2> Swipe;
        public event Action<Vector2> Hold;

        public bool IsReady { get; private set; }

        // Tunable thresholds (increased for better tap detection)
        const float TapMaxDuration      = 0.35f; // seconds (increased from 0.25f)
        const float TapMaxDistanceSqr  = 100f;   // pixels^2 (10 px, increased from 5 px)
        const float HoldMinDuration    = 0.5f;   // seconds
        const float SwipeMinDistanceSqr = 1600f; // pixels^2 (40 px)

        bool   _isDown;
        bool   _holdFired;
        bool   _suppressedByUI;
        float  _downTime;
        Vector2 _downPos;
        Vector2 _lastPos;

        public void Tick(float deltaTime)
        {
            IsReady = true;

            // Single-pointer abstraction:
            // - If touch is supported -> use first touch
            // - Else -> use mouse left button as pointer
            bool isPressed;
            Vector2 currentPos;
            int touchId = -1;

            if (UnityEngine.Input.touchSupported && UnityEngine.Input.touchCount > 0)
            {
                var t = UnityEngine.Input.GetTouch(0);
                currentPos = t.position;
                touchId    = t.fingerId;
                isPressed  = t.phase == TouchPhase.Began ||
                             t.phase == TouchPhase.Moved ||
                             t.phase == TouchPhase.Stationary;
            }
            else
            {
                isPressed  = UnityEngine.Input.GetMouseButton(0);
                currentPos = UnityEngine.Input.mousePosition;
            }

            if (isPressed)
            {
                if (!_isDown)
                {
                    // Pointer just pressed
                    _isDown    = true;
                    _holdFired = false;
                    _downTime  = Time.unscaledTime;
                    _downPos   = currentPos;
                    _lastPos   = currentPos;

                    // Decided once, at press-begin: see class doc for why not per-frame.
                    _suppressedByUI = IsPointerOverUI(touchId);
                    if (_suppressedByUI)
                        return;

                    PointerDown?.Invoke(currentPos);
                }
                else
                {
                    if (_suppressedByUI)
                        return;

                    // Pointer is held
                    _lastPos = currentPos;

                    var heldFor = Time.unscaledTime - _downTime;
                    var distSqr = (currentPos - _downPos).sqrMagnitude;

                    if (!_holdFired && heldFor >= HoldMinDuration && distSqr <= TapMaxDistanceSqr)
                    {
                        _holdFired = true;
                        Hold?.Invoke(currentPos);
                    }
                }
            }
            else
            {
                if (_isDown)
                {
                    // Pointer just released
                    _isDown = false;

                    bool wasSuppressed = _suppressedByUI;
                    _suppressedByUI = false;
                    if (wasSuppressed)
                        return;

                    PointerUp?.Invoke(_lastPos);

                    var totalTime   = Time.unscaledTime - _downTime;
                    var distanceSqr = (_lastPos - _downPos).sqrMagnitude;

                    if (distanceSqr <= TapMaxDistanceSqr && totalTime <= TapMaxDuration)
                    {
                        Tap?.Invoke(_lastPos);
                        Debug.Log($"[InputService] TAP detected at {_lastPos}, duration: {totalTime:F3}s, distance: {Mathf.Sqrt(distanceSqr):F1}px");
                    }
                    else if (distanceSqr >= SwipeMinDistanceSqr)
                    {
                        Swipe?.Invoke(_downPos, _lastPos);
                        Debug.Log($"[InputService] SWIPE detected, distance: {Mathf.Sqrt(distanceSqr):F1}px");
                    }
                    else
                    {
                        Debug.Log($"[InputService] Input IGNORED - duration: {totalTime:F3}s (max {TapMaxDuration}s), distance: {Mathf.Sqrt(distanceSqr):F1}px (max {Mathf.Sqrt(TapMaxDistanceSqr):F1}px for tap, min {Mathf.Sqrt(SwipeMinDistanceSqr):F1}px for swipe)");
                    }
                }
            }
        }

        /// <summary>
        /// True if the press starting now is over a UI element. Touch uses the per-finger
        /// overload (matches the same touch this method is reading), mouse uses the
        /// parameterless one — same pair EventSystem/StandaloneInputModule expect.
        /// </summary>
        private static bool IsPointerOverUI(int touchId)
        {
            if (EventSystem.current == null) return false;

            return touchId >= 0
                ? EventSystem.current.IsPointerOverGameObject(touchId)
                : EventSystem.current.IsPointerOverGameObject();
        }
    }
}
