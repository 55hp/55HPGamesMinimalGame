using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Gameplay
{
    // Depth-perception scaffolding: draws a wireframe grid on the well's 4 side walls and floor,
    // one colour per level, spanning the full well height - without it a down-the-shaft camera
    // gives no readable sense of how deep a piece is.
    // LineRenderer-based so it actually renders in Play mode (not just Scene view gizmos). Not
    // wired into WellCellRenderer - that's still Phase 5, out of scope. Likely replaced by real
    // level geometry later.
    public sealed class BlockoutWellWireframe : MonoBehaviour
    {
        [Tooltip("World-space origin of the well's (0,0,0) grid cell. Defaults to world origin, matching BlockoutSpawner's convention, if left empty.")]
        [SerializeField] private Transform _wellOrigin;

        [SerializeField] private float _lineWidth = 0.03f;

        [Tooltip("Colour of the floor grid and its fill. Levels take their colours from BlockoutWellConfig's Level Colors.")]
        [SerializeField] private Color _floorColor = Color.white;

        [Tooltip("How far each level's top ring is lowered, so it doesn't overlap the next level's bottom ring and both stay visible.")]
        [SerializeField] private float _levelGap = 0.06f;

        [Tooltip("Opacity of the translucent fill in each wall/floor square, tinted with its level's colour. 0 hides the fill.")]
        [Range(0f, 1f)]
        [SerializeField] private float _cellFillAlpha = 0.2f;

        // Each cube occupies +/- this much around its integer grid coordinate - matches the
        // convention used by BlockoutSpawner's cubes and BlockoutInputHandler's bounds check, so
        // the wireframe lines up exactly with the well's physical walls.
        private const float CellHalfExtent = 0.5f;

        // "Well" layer, so the well's dedicated lights/probe (culling mask Well only) reach the
        // generated lines and fill - new GameObjects don't inherit the parent's layer. -1 if the
        // layer doesn't exist (then the objects just stay on Default).
        private int _wellLayer = -1;

        private void Start()
        {
            _wellLayer = LayerMask.NameToLayer("Well");

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

            Build(wellConfig);
        }

        // Every level is its own width x depth x 1 box wireframe - bottom ring, top ring and a
        // vertical line at every cell boundary on the 4 walls - in that level's colour
        // (BlockoutWellConfig.GetLevelColor, the same colour locked cubes on that level take).
        // Each box's top is lowered by _levelGap so it sits just under the next level's bottom
        // ring - two distinct rings, one per colour, between every pair of levels. Plus the floor
        // grid under level 0 (_floorColor). Every square on the walls and floor also gets a faint
        // fill in its colour (_cellFillAlpha), batched into a single mesh.
        private void Build(BlockoutWellConfig wellConfig)
        {
            int width = wellConfig.Width, height = wellConfig.Height, depth = wellConfig.Depth;

            var material = CreateLineMaterial();
            if (material == null) return;

            var origin = _wellOrigin != null ? _wellOrigin.position : Vector3.zero;

            float xMin = -CellHalfExtent, xMax = width - 1 + CellHalfExtent;
            float zMin = -CellHalfExtent, zMax = depth - 1 + CellHalfExtent;

            var fill = new CellFillBuilder();

            for (int level = 0; level < height; level++)
            {
                var color = wellConfig.GetLevelColor(level);
                float yBottom = level - CellHalfExtent, yTop = yBottom + 1 - _levelGap;
                var fillColor = WithAlpha(color, _cellFillAlpha);

                // Bottom and top rings.
                foreach (float y in new[] { yBottom, yTop })
                {
                    CreateLine(origin, material, new Vector3(xMin, y, zMin), new Vector3(xMax, y, zMin), color);
                    CreateLine(origin, material, new Vector3(xMax, y, zMin), new Vector3(xMax, y, zMax), color);
                    CreateLine(origin, material, new Vector3(xMax, y, zMax), new Vector3(xMin, y, zMax), color);
                    CreateLine(origin, material, new Vector3(xMin, y, zMax), new Vector3(xMin, y, zMin), color);
                }

                // Vertical lines at each cell boundary: X-facing walls (z = zMin / zMax), then
                // Z-facing walls (x = xMin / xMax). Corners are covered by the first loop.
                // Each wall square's fill is added alongside the vertical line on its left edge.
                for (int i = 0; i <= width; i++)
                {
                    float x = xMin + i;
                    CreateLine(origin, material, new Vector3(x, yBottom, zMin), new Vector3(x, yTop, zMin), color);
                    CreateLine(origin, material, new Vector3(x, yBottom, zMax), new Vector3(x, yTop, zMax), color);
                    if (i == width) continue;
                    fill.AddQuad(new Vector3(x, yBottom, zMin), new Vector3(x + 1, yBottom, zMin), new Vector3(x + 1, yTop, zMin), new Vector3(x, yTop, zMin), fillColor);
                    fill.AddQuad(new Vector3(x, yBottom, zMax), new Vector3(x + 1, yBottom, zMax), new Vector3(x + 1, yTop, zMax), new Vector3(x, yTop, zMax), fillColor);
                }

                for (int i = 0; i < depth; i++)
                {
                    float z = zMin + i;
                    if (i > 0)
                    {
                        CreateLine(origin, material, new Vector3(xMin, yBottom, z), new Vector3(xMin, yTop, z), color);
                        CreateLine(origin, material, new Vector3(xMax, yBottom, z), new Vector3(xMax, yTop, z), color);
                    }
                    fill.AddQuad(new Vector3(xMin, yBottom, z), new Vector3(xMin, yBottom, z + 1), new Vector3(xMin, yTop, z + 1), new Vector3(xMin, yTop, z), fillColor);
                    fill.AddQuad(new Vector3(xMax, yBottom, z), new Vector3(xMax, yBottom, z + 1), new Vector3(xMax, yTop, z + 1), new Vector3(xMax, yTop, z), fillColor);
                }
            }

            float yFloor = -CellHalfExtent;
            BuildFloor(origin, material, yFloor, xMin, xMax, width, zMin, zMax, depth);

            var floorFillColor = WithAlpha(_floorColor, _cellFillAlpha);
            for (int i = 0; i < width; i++)
            for (int j = 0; j < depth; j++)
            {
                float x = xMin + i, z = zMin + j;
                fill.AddQuad(new Vector3(x, yFloor, z), new Vector3(x + 1, yFloor, z), new Vector3(x + 1, yFloor, z + 1), new Vector3(x, yFloor, z + 1), floorFillColor);
            }

            if (_cellFillAlpha > 0f)
                CreateFillMesh(origin, material, fill);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // One mesh for all fill squares - a single draw call instead of one renderer per square.
        // Sprites/Default is Cull Off, so winding doesn't matter and the fill shows from inside.
        private void CreateFillMesh(Vector3 origin, Material material, CellFillBuilder fill)
        {
            var fillObject = new GameObject("BlockoutWellCellFill");
            if (_wellLayer >= 0) fillObject.layer = _wellLayer;
            fillObject.transform.SetParent(transform, false);

            // Quads are in well-local space like the lines; convert to this object's space so
            // they land where the world-space lines do.
            var vertices = new Vector3[fill.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = fillObject.transform.InverseTransformPoint(origin + fill.Vertices[i]);

            var mesh = new Mesh { name = "BlockoutWellCellFill" };
            mesh.SetVertices(vertices);
            mesh.SetColors(fill.Colors);
            mesh.SetTriangles(fill.Triangles, 0);

            fillObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = fillObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private sealed class CellFillBuilder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();

            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                int start = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                Colors.Add(color); Colors.Add(color); Colors.Add(color); Colors.Add(color);
                Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
                Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 3);
            }
        }

        // The floor grid at y: lines parallel to X at each Z row, lines parallel to Z at each X
        // column boundary, in _floorColor.
        private void BuildFloor(Vector3 origin, Material material, float y,
            float xMin, float xMax, int width, float zMin, float zMax, int depth)
        {
            for (int i = 0; i <= depth; i++)
            {
                float z = zMin + i;
                CreateLine(origin, material, new Vector3(xMin, y, z), new Vector3(xMax, y, z), _floorColor);
            }

            for (int i = 0; i <= width; i++)
            {
                float x = xMin + i;
                CreateLine(origin, material, new Vector3(x, y, zMin), new Vector3(x, y, zMax), _floorColor);
            }
        }

        private Material CreateLineMaterial()
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[BlockoutWellWireframe] No compatible unlit/sprite shader found - cannot render the wireframe.", this);
                return null;
            }

            return new Material(shader);
        }

        private void CreateLine(Vector3 origin, Material material, Vector3 localA, Vector3 localB, Color color)
        {
            var lineObject = new GameObject("BlockoutWellWireframeLine");
            if (_wellLayer >= 0) lineObject.layer = _wellLayer;
            lineObject.transform.SetParent(transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.material = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, origin + localA);
            line.SetPosition(1, origin + localB);
            line.startWidth = _lineWidth;
            line.endWidth = _lineWidth;
            line.startColor = color;
            line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
    }
}
