using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.UI;

namespace hp55games.Blockout.UI
{
    // The horizontal layout values of a RectTransform that a left/right mirror affects.
    public struct HudRectLayout
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
    }

    public static class HudMirrorMath
    {
        // Mirrors about the vertical axis of the parent rect: anchor/pivot x flip around 0.5 (min and
        // max swap roles), anchoredPosition.x negates. Y and sizeDelta are unaffected. Applying it
        // twice returns the original layout.
        public static HudRectLayout Mirror(HudRectLayout l)
        {
            return new HudRectLayout
            {
                AnchorMin = new Vector2(1f - l.AnchorMax.x, l.AnchorMin.y),
                AnchorMax = new Vector2(1f - l.AnchorMin.x, l.AnchorMax.y),
                Pivot = new Vector2(1f - l.Pivot.x, l.Pivot.y),
                AnchoredPosition = new Vector2(-l.AnchoredPosition.x, l.AnchoredPosition.y),
            };
        }
    }

    // Mirrors the HUD action cluster for left-handed play (IUIOptionsService.LeftHanded): the
    // cluster itself (about the screen's vertical axis) and each of its direct children (about the
    // cluster's own vertical axis), so the overall arrangement is a true mirror image. The authored
    // layout is captured once and every apply derives from it, so toggling is lossless.
    public sealed class HudHandednessMirror : MonoBehaviour
    {
        [Tooltip("The bottom action cluster's RectTransform. Its direct children are mirrored within it.")]
        [SerializeField] private RectTransform _cluster;

        private IUIOptionsService _options;
        private readonly List<(RectTransform rect, HudRectLayout authored)> _captured = new();
        private bool _hasCaptured;

        private void OnEnable()
        {
            if (_cluster == null)
            {
                Debug.LogError("[HudHandednessMirror] _cluster is not assigned - assign the action cluster RectTransform in the Inspector.", this);
                enabled = false;
                return;
            }

            CaptureAuthored();

            if (!ServiceRegistry.TryResolve(out _options))
            {
                Debug.LogWarning("[HudHandednessMirror] IUIOptionsService is not registered - keeping the authored layout.", this);
                return;
            }

            _options.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_options != null)
            {
                _options.Changed -= Apply;
                _options = null;
            }
        }

        private void CaptureAuthored()
        {
            if (_hasCaptured) return;
            _hasCaptured = true;

            _captured.Add((_cluster, Read(_cluster)));
            foreach (Transform child in _cluster)
            {
                if (child is RectTransform rect)
                    _captured.Add((rect, Read(rect)));
            }
        }

        private void Apply()
        {
            if (_options == null) return;

            bool mirrored = _options.LeftHanded;
            foreach (var (rect, authored) in _captured)
            {
                if (rect == null) continue;
                Write(rect, mirrored ? HudMirrorMath.Mirror(authored) : authored);
            }
        }

        private static HudRectLayout Read(RectTransform r) => new HudRectLayout
        {
            AnchorMin = r.anchorMin,
            AnchorMax = r.anchorMax,
            Pivot = r.pivot,
            AnchoredPosition = r.anchoredPosition,
        };

        private static void Write(RectTransform r, HudRectLayout l)
        {
            r.anchorMin = l.AnchorMin;
            r.anchorMax = l.AnchorMax;
            r.pivot = l.Pivot;
            r.anchoredPosition = l.AnchoredPosition;
        }
    }
}
