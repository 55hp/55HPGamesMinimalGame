using UnityEngine;
using UnityEngine.UI;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;
using hp55games.Ui;

namespace hp55games.Blockout.Gameplay
{
    // Replaces the static Inspector field-of-view baked into 02_Gameplay.unity with a value
    // recomputed at runtime from BlockoutWellConfig's real bounds and the device's actual aspect
    // ratio, safe area, and gameplay-HUD footprint -
    // the fixed 40 degree FOV was only ever eyeballed against the Editor Game View's default
    // aspect ratio and clips the well's top opening on unusually tall/narrow screens (e.g. the
    // Samsung S25 Edge). Position and rotation (looking straight down at the well) are left
    // exactly as authored in the scene - this component only ever touches fieldOfView, the
    // camera's single source of truth for position stays the Transform (README §0 rule 4).
    //
    // Symmetric-frustum limitation: with no lens shift, an inset on only one edge (e.g. a HUD bar
    // at the top but nothing at the bottom, or the camera simply not sitting exactly centered over
    // the well's X/Z in the scene) still costs frustum on BOTH edges - the fit uses whichever of
    // the two opposing insets/offsets is larger. Good enough to guarantee full, unoccluded
    // visibility; the well won't be perfectly re-centered in frame for a non-zero horizontal
    // offset. A future asymmetric fit would need physical-camera lens shift instead.
    [RequireComponent(typeof(Camera))]
    public sealed class BlockoutWellCamera : MonoBehaviour
    {
        // Same +/- extent convention as BlockoutWellWireframe/BlockoutInputHandler, so this
        // camera's fit uses the well's real physical bounds, not just its grid cell counts.
        private const float CellHalfExtent = 0.5f;

        [Tooltip("World-space origin of the well's (0,0,0) grid cell. Defaults to world origin, matching BlockoutSpawner's convention, if left empty.")]
        [SerializeField] private Transform _wellOrigin;

        [Tooltip("UIGameHUD.prefab's Header RectTransform sizeDelta.y, in CanvasScaler reference-resolution units - currently the only screen space the gameplay HUD reserves (score/lives/pause all live inside that one top bar). Keep this in sync if Header's height ever changes.")]
        [SerializeField] private float _hudTopReservedCanvasUnits = 100f;

        private Camera _camera;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start() => Recompute();

        private void Update()
        {
            // Cheap guard - only redo the trig when something that actually affects framing has
            // changed (device rotation, resolution change, or a safe-area change e.g. a foldable
            // unfolding), not every frame.
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight || Screen.safeArea != _lastSafeArea)
            {
                Recompute();
            }
        }

        private void Recompute()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastSafeArea = Screen.safeArea;

            if (_camera == null) return;

            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalogService))
            {
                Debug.LogError("[BlockoutWellCamera] IConfigCatalogService is not registered - add a ConfigCatalogInstaller (with a populated ConfigCatalog) to the scene.", this);
                return;
            }

            var wellConfig = catalogService.Get<BlockoutWellConfig>();
            if (wellConfig == null)
            {
                Debug.LogError("[BlockoutWellCamera] No BlockoutWellConfig found in the catalog.", this);
                return;
            }

            var origin = _wellOrigin != null ? _wellOrigin.position : Vector3.zero;

            // A naive fit would assume the camera sits exactly above the well's X/Z center (true
            // for the intended scene-authored position, e.g. (2, 18, 2) over a 5x5 well centered
            // at (2, 2)) - if Bezi's scene Transform ever drifts from that, the frustum widens by
            // however far the camera actually is from that center, on each axis, to keep both of
            // the well's edges in frame from its real position. This keeps the well fully
            // visible, not perfectly re-centered in the frame - a symmetric FOV/aspect fit can't
            // re-center for an off-axis camera without lens shift (see the class's own
            // "Symmetric-frustum limitation" remarks - same constraint, different axis).
            float wellCenterX = origin.x + (wellConfig.Width - 1) / 2f;
            float wellCenterZ = origin.z + (wellConfig.Depth - 1) / 2f;
            float cameraOffsetX = transform.position.x - wellCenterX;
            float cameraOffsetZ = transform.position.z - wellCenterZ;

            // Horizontal (screen-width) padding is percentage-based, not world-space: a fixed
            // world-unit margin can't give a constant on-screen fraction on its own, since the
            // world-units-to-screen-fraction relationship depends on the FOV that the padding
            // itself feeds into - circular in world-space. Scale the offset-compensated
            // half-extent up so it sits at (1 - 2*fraction) of the way to the frame edge: the
            // well's own width (2x that half-extent) then occupies exactly (1 - 2*fraction) of
            // the frame width by construction, as a ratio - independent of FOV/aspect/distance.
            float rawHalfExtentX = wellConfig.Width / 2f + Mathf.Abs(cameraOffsetX);
            float horizontalPaddingFraction = Mathf.Clamp(wellConfig.HorizontalPaddingScreenFraction, 0f, 0.45f);
            float halfExtentX = rawHalfExtentX / (1f - 2f * horizontalPaddingFraction);

            // Vertical/depth axis has no padding at all - only the horizontal axis gets a margin
            // (HorizontalPaddingScreenFraction, above). The well's depth edge sits exactly at the
            // frame boundary when the camera is centered on Z.
            float halfExtentZ = wellConfig.Depth / 2f + Mathf.Abs(cameraOffsetZ);
            float wellTopY = origin.y + (wellConfig.Height - 1 + CellHalfExtent);

            // The well's top opening is nearest the camera (which looks straight down), so it
            // needs the largest angle to stay framed - that's the binding constraint.
            float distance = transform.position.y - wellTopY;
            if (distance <= 0f)
            {
                Debug.LogError("[BlockoutWellCamera] Camera is not above the well's top opening - cannot compute a fit.", this);
                return;
            }

            float widthHalfAngle = Mathf.Atan(halfExtentX / distance);
            float depthHalfAngle = Mathf.Atan(halfExtentZ / distance);

            float effectiveAspect = ComputeEffectiveAspect();

            // FOVAxisMode on this camera is Vertical, and by construction the vertical screen
            // axis maps to world Z (well depth) while horizontal maps to world X (well width) and
            // is only ever derived from the vertical FOV via aspect. So solve for whichever
            // vertical half-angle is large enough to cover BOTH axes once that derivation is
            // accounted for.
            float requiredVerticalHalfAngle = Mathf.Max(depthHalfAngle, Mathf.Atan(Mathf.Tan(widthHalfAngle) / effectiveAspect));

            _camera.fieldOfView = Mathf.Clamp(requiredVerticalHalfAngle * 2f * Mathf.Rad2Deg, 1f, 170f);
        }

        // The full-screen aspect ratio, narrowed to only the region that's both within the
        // device's safe area and not covered by the gameplay HUD - so the well is guaranteed
        // fully visible AND unoccluded, not just on-screen.
        private float ComputeEffectiveAspect()
        {
            var safe = _lastSafeArea;
            float safeLeftFrac = safe.xMin / Screen.width;
            float safeRightFrac = 1f - safe.xMax / Screen.width;
            float safeBottomFrac = safe.yMin / Screen.height;
            float safeTopFrac = 1f - safe.yMax / Screen.height;

            float hudTopFrac = 0f;
            if (TryGetCanvasScaleFactor(out float scaleFactor))
            {
                hudTopFrac = (_hudTopReservedCanvasUnits * scaleFactor) / Screen.height;
            }

            // Symmetric frustum: an inset on only one edge still costs both, so the binding
            // constraint per axis is whichever opposing inset is larger - see class remarks.
            float usableHeightFrac = Mathf.Clamp01(1f - 2f * Mathf.Max(safeTopFrac + hudTopFrac, safeBottomFrac));
            float usableWidthFrac = Mathf.Clamp01(1f - 2f * Mathf.Max(safeLeftFrac, safeRightFrac));

            usableHeightFrac = Mathf.Max(usableHeightFrac, 0.05f);
            usableWidthFrac = Mathf.Max(usableWidthFrac, 0.05f);

            return (Screen.width * usableWidthFrac) / (Screen.height * usableHeightFrac);
        }

        private static bool TryGetCanvasScaleFactor(out float scaleFactor)
        {
            var uiRoot = UIRoot.FindOrCache();
            var scaler = uiRoot != null ? uiRoot.GetComponentInParent<CanvasScaler>() : null;
            if (scaler != null)
            {
                scaleFactor = scaler.scaleFactor;
                return true;
            }

            scaleFactor = 1f;
            return false;
        }
    }
}
