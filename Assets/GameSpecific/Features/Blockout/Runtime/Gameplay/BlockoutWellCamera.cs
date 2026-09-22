using UnityEngine;

namespace hp55games.Blockout.Gameplay
{
    // Scales the well camera's fieldOfView across devices the same way CanvasScaler scales UI:
    // Franci authors the camera's Transform AND fieldOfView by eye in the Editor/on-device at one
    // reference aspect ratio (Samsung Galaxy S25 Edge, this project's actual test device -
    // 1440x3120 portrait, ~9:19.5), and this component scales that authored FOV for every other
    // device aspect with a single standard formula - no per-frame trig fit against the well's
    // world-space bounds, HUD insets, or safe area.
    //
    // This intentionally replaces an earlier version that re-derived FOV every resolution change
    // from BlockoutWellConfig's bounds + HUD reserved space + safe area via exact frustum-fit
    // trigonometry. That was over-engineered for this project's scope and its horizontal-padding
    // and vertical-HUD-clearance math ended up silently coupled through one shared "effective
    // aspect" term, canceling part of the intended margin. This version is "good enough" framing
    // across the phone-range aspect ratios this game targets, not a mathematically exact fit for
    // every possible device - an unusually square aspect (e.g. a tablet) may not be perfectly
    // framed, and that trade-off is accepted (out of scope).
    //
    // Formula: Unity's Camera.fieldOfView (with FOVAxisMode = Vertical, as authored on this
    // camera) is always the VERTICAL half-angle; the horizontal half-angle is whatever the
    // renderer derives from it via the current aspect (tan(hHalf) = tan(vHalf) * aspect). So the
    // authored reference FOV, at the reference aspect, already fixes the horizontal (well-width)
    // framing Franci tuned by eye. To keep that same horizontal framing on every other aspect
    // ratio, scale the vertical half-angle inversely with aspect:
    //
    //     tan(currentHalfV) = tan(referenceHalfV) * (referenceAspect / currentAspect)
    //
    // On a narrower/taller screen than the reference (smaller aspect), this widens the vertical
    // FOV, showing MORE well depth than the reference frame while keeping the same well width -
    // never clips. On a wider/shorter screen (larger aspect), it narrows the vertical FOV,
    // showing less depth - the accepted "unusual aspect ratio" trade-off above.
    [RequireComponent(typeof(Camera))]
    public sealed class BlockoutWellCamera : MonoBehaviour
    {
        [Tooltip("Reference resolution the camera's Field Of View (on the Camera component) was authored/eyeballed at. Currently Franci's test device, Samsung Galaxy S25 Edge, portrait: 1440x3120. Only the aspect ratio (x/y) matters, not the absolute pixel values.")]
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1440f, 3120f);

        private Camera _camera;
        private float _referenceFieldOfView;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private Rect _lastSafeArea;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            // Capture whatever FOV Franci authored on the Camera component in the Editor/scene
            // BEFORE this component ever overwrites it - that authored value is the reference FOV
            // the scale formula scales from.
            _referenceFieldOfView = _camera.fieldOfView;
        }

        private void Start() => Recompute();

        private void Update()
        {
            // Cheap guard - only rescale when something that actually affects aspect has changed
            // (device rotation, resolution change, or a safe-area change e.g. a foldable
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

            if (_camera == null || Screen.height <= 0 || _referenceResolution.y <= 0f) return;

            float referenceAspect = _referenceResolution.x / _referenceResolution.y;
            float currentAspect = (float)Screen.width / Screen.height;

            float referenceHalfV = _referenceFieldOfView * 0.5f * Mathf.Deg2Rad;
            float currentHalfV = Mathf.Atan(Mathf.Tan(referenceHalfV) * referenceAspect / currentAspect);

            _camera.fieldOfView = Mathf.Clamp(currentHalfV * 2f * Mathf.Rad2Deg, 1f, 170f);
        }
    }
}
