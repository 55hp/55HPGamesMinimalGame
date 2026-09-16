using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Gameplay
{
    // TEMP depth-perception scaffolding: draws a wireframe grid on the well's 4 side walls and
    // floor, spanning the full well height, matching the original Blockout's green-tunnel
    // reference look - without it a down-the-shaft camera gives no readable sense of how deep a
    // piece is.
    // LineRenderer-based so it actually renders in Play mode (not just Scene view gizmos). Not
    // wired into WellCellRenderer - that's still Phase 5, out of scope. Likely replaced by real
    // level geometry later.
    public sealed class BlockoutWellWireframe : MonoBehaviour
    {
        [Tooltip("World-space origin of the well's (0,0,0) grid cell. Defaults to world origin, matching BlockoutSpawner's convention, if left empty.")]
        [SerializeField] private Transform _wellOrigin;

        [SerializeField] private Color _lineColor = new Color(0.15f, 1f, 0.3f, 1f);
        [SerializeField] private float _lineWidth = 0.03f;

        // Each cube occupies +/- this much around its integer grid coordinate - matches the
        // convention used by BlockoutSpawner's cubes and BlockoutInputHandler's bounds check, so
        // the wireframe lines up exactly with the well's physical walls.
        private const float CellHalfExtent = 0.5f;

        private enum FixedAxis { X, Y, Z }

        private void Start()
        {
            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalogService))
            {
                Debug.LogError("[BlockoutWellWireframe] IConfigCatalogService is not registered - add a ConfigCatalogInstaller (with a populated ConfigCatalog) to the scene.", this);
                return;
            }

            var wellConfig = catalogService.Get<BlockoutWellConfig>();
            if (wellConfig == null)
            {
                Debug.LogError("[BlockoutWellWireframe] No BlockoutWellConfig found in the catalog.", this);
                return;
            }

            Build(wellConfig.Width, wellConfig.Height, wellConfig.Depth);
        }

        private void Build(int width, int height, int depth)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[BlockoutWellWireframe] No compatible unlit/sprite shader found - cannot render the wireframe.", this);
                return;
            }

            var origin = _wellOrigin != null ? _wellOrigin.position : Vector3.zero;
            var material = new Material(shader);

            float xMin = -CellHalfExtent, xMax = width - 1 + CellHalfExtent;
            float yMin = -CellHalfExtent, yMax = height - 1 + CellHalfExtent;
            float zMin = -CellHalfExtent, zMax = depth - 1 + CellHalfExtent;

            // The two walls at x = xMin / xMax: horizontal lines per Y row, vertical lines at
            // each Z column boundary.
            BuildWall(origin, material, FixedAxis.X, xMin, zMin, zMax, depth, yMin, yMax, height);
            BuildWall(origin, material, FixedAxis.X, xMax, zMin, zMax, depth, yMin, yMax, height);

            // The two walls at z = zMin / zMax: horizontal lines per Y row, vertical lines at
            // each X column boundary.
            BuildWall(origin, material, FixedAxis.Z, zMin, xMin, xMax, width, yMin, yMax, height);
            BuildWall(origin, material, FixedAxis.Z, zMax, xMin, xMax, width, yMin, yMax, height);

            // The floor at y = yMin: lines parallel to X at each Z row, lines parallel to Z at
            // each X column boundary. Same BuildWall shape as a wall - just with Y as the fixed
            // axis and both remaining axes horizontal instead of one being vertical.
            BuildWall(origin, material, FixedAxis.Y, yMin, xMin, xMax, width, zMin, zMax, depth);
        }

        // Draws one wall/floor's grid: (yCount+1) lines spanning u, and (uCount+1) lines spanning
        // y. `u` and `y` are just the plane's two free axes - for an X/Z-fixed wall, `y` is world
        // Y; for the Y-fixed floor, `y` is world Z. See PointOn for the axis mapping.
        private void BuildWall(Vector3 origin, Material material, FixedAxis fixedAxis, float fixedValue,
            float uMin, float uMax, int uCount, float yMin, float yMax, int yCount)
        {
            for (int i = 0; i <= yCount; i++)
            {
                float y = yMin + i;
                CreateLine(origin, material, PointOn(fixedAxis, fixedValue, uMin, y), PointOn(fixedAxis, fixedValue, uMax, y));
            }

            for (int i = 0; i <= uCount; i++)
            {
                float u = uMin + i;
                CreateLine(origin, material, PointOn(fixedAxis, fixedValue, u, yMin), PointOn(fixedAxis, fixedValue, u, yMax));
            }
        }

        private static Vector3 PointOn(FixedAxis fixedAxis, float fixedValue, float u, float y) => fixedAxis switch
        {
            FixedAxis.X => new Vector3(fixedValue, y, u),
            FixedAxis.Z => new Vector3(u, y, fixedValue),
            _ => new Vector3(u, fixedValue, y), // FixedAxis.Y (floor): free axes are X and Z.
        };

        private void CreateLine(Vector3 origin, Material material, Vector3 localA, Vector3 localB)
        {
            var lineObject = new GameObject("BlockoutWellWireframeLine (TEMP)");
            lineObject.transform.SetParent(transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.material = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, origin + localA);
            line.SetPosition(1, origin + localB);
            line.startWidth = _lineWidth;
            line.endWidth = _lineWidth;
            line.startColor = _lineColor;
            line.endColor = _lineColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
    }
}
