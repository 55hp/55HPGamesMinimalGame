// Assets/Editor/HP55_Mesh3DToCollider2DTool.cs
// Extracts the 2D silhouette from the central Z-plane of a 3D polygonal mesh
// and generates a prefab with a Rigidbody2D and matching PolygonCollider2D.
//
// Algorithm:
//   1. For each triangle: compute intersection with the Z = sliceZ plane.
//   2. Collect intersection segments.
//   3. Chain segments into one or more closed loops.
//   4. Select the longest loop (ignores minor holes).
//   5. Ramer-Douglas-Peucker simplification.
//   6. Create and save the prefab.
//
// Open via: hp55games Tools/Mesh 3D → Collider 2D

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace hp55games.Editor.Tools
{
    public class HP55_Mesh3DToCollider2DTool : EditorWindow
    {
        // ── fields ────────────────────────────────────────────────────────────
        [SerializeField] private GameObject _sourceObject;     // accetta scene GO o prefab
        [SerializeField] private Mesh       _sourceMesh;       // alternativa diretta

        [SerializeField] private bool  _useCenter       = true;
        [SerializeField] private float _manualSliceZ    = 0f;

        [SerializeField] private float _mergeTolerance  = 0.001f;
        [SerializeField] private float _simplifyEpsilon = 0.01f;

        [SerializeField] private string _outputFolder = "Assets";

        // runtime state
        private List<Vector2> _lastPolygon;
        private string        _lastStatus;
        private bool          _lastSuccess;
        private Vector2       _scroll;

        // ── menu ─────────────────────────────────────────────────────────────
        [MenuItem("hp55games Tools/Mesh 3D → Collider 2D")]
        private static void Open()
        {
            var w = GetWindow<HP55_Mesh3DToCollider2DTool>("Mesh → Collider 2D");
            w.minSize = new Vector2(360, 500);
        }

        // ── GUI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Mesh 3D → PolygonCollider2D", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Slicia la mesh sul piano Z centrale e ricava la silhouette 2D.\n" +
                "Output: prefab con Rigidbody2D + PolygonCollider2D.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            // ── Source ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Sorgente", EditorStyles.boldLabel);

            var newGO = (GameObject)EditorGUILayout.ObjectField(
                "GameObject (MeshFilter)", _sourceObject, typeof(GameObject), true);
            if (newGO != _sourceObject) { _sourceObject = newGO; _sourceMesh = null; }

            var newMesh = (Mesh)EditorGUILayout.ObjectField(
                "Mesh (asset diretto)", _sourceMesh, typeof(Mesh), false);
            if (newMesh != _sourceMesh) { _sourceMesh = newMesh; _sourceObject = null; }

            EditorGUILayout.Space(6);

            // ── Slice plane ───────────────────────────────────────────────────
            EditorGUILayout.LabelField("Piano di sezione", EditorStyles.boldLabel);
            _useCenter = EditorGUILayout.Toggle("Usa centro bounds (Z)", _useCenter);
            EditorGUI.BeginDisabledGroup(_useCenter);
            _manualSliceZ = EditorGUILayout.FloatField("Z manuale", _manualSliceZ);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(6);

            // ── Processing ────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Elaborazione", EditorStyles.boldLabel);
            _mergeTolerance  = EditorGUILayout.Slider("Merge tolerance",  _mergeTolerance,  0.0001f, 0.1f);
            _simplifyEpsilon = EditorGUILayout.Slider("Simplify epsilon", _simplifyEpsilon, 0f,      0.2f);
            EditorGUILayout.HelpBox(
                "Merge tolerance: distanza massima per considerare due endpoint coincidenti.\n" +
                "Simplify epsilon: deviazione massima tollerata nella riduzione vertici (0 = nessuna).",
                MessageType.None);

            EditorGUILayout.Space(6);

            // ── Output ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _outputFolder = EditorGUILayout.TextField("Cartella", _outputFolder);
            if (GUILayout.Button("…", GUILayout.Width(28)))
            {
                string chosen = EditorUtility.OpenFolderPanel("Cartella output", _outputFolder, "");
                if (!string.IsNullOrEmpty(chosen) && chosen.Contains("Assets"))
                    _outputFolder = "Assets" + chosen.Substring(chosen.IndexOf("Assets") + 6);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ── Generate ──────────────────────────────────────────────────────
            Mesh resolvedMesh = ResolveMesh(out string meshName);
            bool canGenerate  = resolvedMesh != null;

            GUI.backgroundColor = canGenerate ? new Color(0.4f, 0.9f, 0.4f) : Color.gray;
            EditorGUI.BeginDisabledGroup(!canGenerate);
            if (GUILayout.Button("▶  Genera Prefab", GUILayout.Height(36)))
                GeneratePrefab(resolvedMesh, meshName);
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            // ── Status ────────────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(_lastStatus))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_lastStatus,
                    _lastSuccess ? MessageType.Info : MessageType.Error);

                if (_lastSuccess && _lastPolygon != null)
                    EditorGUILayout.HelpBox(
                        $"Vertici finali nel PolygonCollider2D: {_lastPolygon.Count}",
                        MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Core ─────────────────────────────────────────────────────────────
        private void GeneratePrefab(Mesh mesh, string meshName)
        {
            _lastPolygon = null;
            _lastStatus  = null;
            _lastSuccess = false;

            // Determine slice Z
            float sliceZ = _useCenter ? mesh.bounds.center.z : _manualSliceZ;

            // Step 1: extract intersection segments
            var segments = ExtractSegments(mesh, sliceZ);
            if (segments.Count == 0)
            {
                _lastStatus = $"Nessun segmento di sezione trovato a Z={sliceZ:F4}.\n" +
                              "Prova a disabilitare 'Usa centro bounds' e impostare Z manualmente.";
                return;
            }

            // Step 2: chain segments into closed loops
            var loops = ChainSegments(segments, _mergeTolerance);
            if (loops.Count == 0)
            {
                _lastStatus = "Impossibile ricostruire il poligono: i segmenti non formano loop chiusi.\n" +
                              "Prova ad aumentare Merge tolerance.";
                return;
            }

            // Step 3: pick the largest loop (main silhouette, ignores internal holes)
            List<Vector2> polygon = loops.OrderByDescending(l => l.Count).First();

            // Step 4: RDP simplification
            if (_simplifyEpsilon > 0f)
                polygon = SimplifyRDP(polygon, _simplifyEpsilon);

            // Step 5: enforce PolygonCollider2D vertex cap (255)
            const int maxVerts = 255;
            float     eps      = _simplifyEpsilon;
            while (polygon.Count > maxVerts)
            {
                eps    *= 1.5f;
                polygon = SimplifyRDP(polygon, eps);
            }

            if (polygon.Count < 3)
            {
                _lastStatus = "Poligono degenere dopo la semplificazione (< 3 vertici). Riduci Simplify epsilon.";
                return;
            }

            _lastPolygon = polygon;

            // Step 6: validate output folder
            if (!AssetDatabase.IsValidFolder(_outputFolder))
            {
                _lastStatus = $"Cartella output non valida: {_outputFolder}";
                return;
            }

            // Step 7: build prefab
            string prefabName = meshName + "_Col2D";
            string prefabPath = $"{_outputFolder}/{prefabName}.prefab";

            var go = new GameObject(prefabName);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;

            var pc = go.AddComponent<PolygonCollider2D>();
            pc.pathCount = 1;
            pc.SetPath(0, polygon.ToArray());

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            DestroyImmediate(go);
            AssetDatabase.Refresh();

            if (asset != null)
            {
                _lastStatus  = $"Prefab salvato in: {prefabPath}";
                _lastSuccess = true;
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                _lastStatus = "Errore durante il salvataggio del prefab.";
            }
        }

        // ── Mesh resolution ───────────────────────────────────────────────────
        private Mesh ResolveMesh(out string name)
        {
            if (_sourceMesh != null)
            {
                name = _sourceMesh.name;
                return _sourceMesh;
            }
            if (_sourceObject != null)
            {
                // Works for both scene GameObjects and prefab assets
                var mf = _sourceObject.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    name = _sourceObject.name;
                    return mf.sharedMesh;
                }
                // Try SkinnedMeshRenderer
                var smr = _sourceObject.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                {
                    name = _sourceObject.name;
                    return smr.sharedMesh;
                }
            }
            name = "Unknown";
            return null;
        }

        // ── Step 1: triangle–plane intersection ───────────────────────────────
        // For each triangle, finds the 2 points where the triangle edges cross
        // the plane Z = sliceZ, yielding a line segment in XY.
        private static List<(Vector2 a, Vector2 b)> ExtractSegments(Mesh mesh, float sliceZ)
        {
            Vector3[] verts = mesh.vertices;
            int[]     tris  = mesh.triangles;
            var       result= new List<(Vector2, Vector2)>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 v0 = verts[tris[i]];
                Vector3 v1 = verts[tris[i + 1]];
                Vector3 v2 = verts[tris[i + 2]];

                var seg = TrianglePlaneSegment(v0, v1, v2, sliceZ);
                if (seg.HasValue)
                    result.Add(seg.Value);
            }
            return result;
        }

        private static (Vector2, Vector2)? TrianglePlaneSegment(
            Vector3 v0, Vector3 v1, Vector3 v2, float sliceZ)
        {
            float d0 = v0.z - sliceZ;
            float d1 = v1.z - sliceZ;
            float d2 = v2.z - sliceZ;

            var pts = new List<Vector2>(3);
            EdgeIntersect(v0, v1, d0, d1, pts);
            EdgeIntersect(v1, v2, d1, d2, pts);
            EdgeIntersect(v2, v0, d2, d0, pts);

            // Deduplicate (vertices exactly on the plane appear on two edges)
            for (int i = pts.Count - 1; i >= 1; i--)
                for (int j = 0; j < i; j++)
                    if (Vector2.Distance(pts[i], pts[j]) < 1e-5f) { pts.RemoveAt(i); break; }

            if (pts.Count < 2) return null;
            return (pts[0], pts[1]);
        }

        // Adds the XY projection of the edge-plane intersection to pts.
        private static void EdgeIntersect(
            Vector3 a, Vector3 b, float da, float db, List<Vector2> pts)
        {
            if (da * db < 0f)
            {
                // Edge straddles the plane: interpolate
                float t = da / (da - db);
                Vector3 p = Vector3.Lerp(a, b, t);
                pts.Add(new Vector2(p.x, p.y));
            }
            else if (Mathf.Abs(da) < 1e-6f)
            {
                // Vertex exactly on the plane
                pts.Add(new Vector2(a.x, a.y));
            }
        }

        // ── Step 2: segment chaining into closed loops ────────────────────────
        // Greedy nearest-neighbor chain. O(n²) — acceptable for editor use.
        private static List<List<Vector2>> ChainSegments(
            List<(Vector2 a, Vector2 b)> segments, float tol)
        {
            var remaining = new List<(Vector2 a, Vector2 b)>(segments);
            var loops     = new List<List<Vector2>>();

            while (remaining.Count > 0)
            {
                var loop = new List<Vector2>();
                var cur  = remaining[0];
                remaining.RemoveAt(0);

                loop.Add(cur.a);
                Vector2 head = cur.b;

                bool grew = true;
                while (grew && remaining.Count > 0)
                {
                    grew = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var seg = remaining[i];

                        if (Vector2.Distance(head, seg.a) <= tol)
                        {
                            loop.Add(head);
                            head = seg.b;
                            remaining.RemoveAt(i);
                            grew = true;
                            break;
                        }
                        if (Vector2.Distance(head, seg.b) <= tol)
                        {
                            loop.Add(head);
                            head = seg.a;
                            remaining.RemoveAt(i);
                            grew = true;
                            break;
                        }
                    }
                }

                if (loop.Count >= 3)
                    loops.Add(loop);
            }
            return loops;
        }

        // ── Step 3: Ramer–Douglas–Peucker simplification ──────────────────────
        private static List<Vector2> SimplifyRDP(List<Vector2> pts, float epsilon)
        {
            if (pts.Count < 3) return pts;
            return RDPRecurse(pts, 0, pts.Count - 1, epsilon);
        }

        private static List<Vector2> RDPRecurse(List<Vector2> pts, int start, int end, float eps)
        {
            float maxDist  = 0f;
            int   maxIndex = start;

            for (int i = start + 1; i < end; i++)
            {
                float d = PerpendicularDistance(pts[i], pts[start], pts[end]);
                if (d > maxDist) { maxDist = d; maxIndex = i; }
            }

            if (maxDist > eps)
            {
                var left  = RDPRecurse(pts, start, maxIndex, eps);
                var right = RDPRecurse(pts, maxIndex, end, eps);
                left.RemoveAt(left.Count - 1);
                left.AddRange(right);
                return left;
            }

            return new List<Vector2> { pts[start], pts[end] };
        }

        private static float PerpendicularDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            float dx  = b.x - a.x;
            float dy  = b.y - a.y;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 1e-8f) return Vector2.Distance(p, a);
            // Signed area formula: |cross(ab, ap)| / |ab|
            return Mathf.Abs(dy * p.x - dx * p.y + b.x * a.y - b.y * a.x) / len;
        }
    }
}
