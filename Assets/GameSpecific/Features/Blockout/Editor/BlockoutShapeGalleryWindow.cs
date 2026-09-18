using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using hp55games.Blockout;
using hp55games.Blockout.Config;
using hp55games.Polycubes.Shapes;

namespace hp55games.Blockout.Config.EditorTools
{
    // Read-only isometric preview of the 12 canonical pieces (Tetracube01..08, Pentacube01..04),
    // each paired with a button to toggle it in BlockoutShapeSelectionConfig - so Franci can see
    // which shape a name refers to before deciding what to enable/disable. Reads
    // BlockoutShapeSet.AllShapes() for the preview only; never touches PolycubeGenerator,
    // BuildDefault, GetPieceEntries, or the pentacube auto-selection. Writes go straight to the
    // config asset via SerializedObject, same pattern as ConfigCatalogEditor.
    public sealed class BlockoutShapeGalleryWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/GameSpecific/Content/Configs/BlockoutShapeSelection.asset";
        private const int Columns = 4;
        private const float CellWidth = 160f;
        private const float PreviewHeight = 120f;
        private const float CellScale = 13f;

        private static readonly float CosIso = Mathf.Cos(30f * Mathf.Deg2Rad);
        private static readonly float SinIso = Mathf.Sin(30f * Mathf.Deg2Rad);

        private static readonly Vector3Int[] CornerOffsets = BuildCornerOffsets();
        private static readonly (int a, int b)[] CubeEdgePairs = BuildCubeEdgePairs();

        private Vector2 _scroll;

        [MenuItem("Tools/Blockout/Shape Gallery")]
        private static void Open() => GetWindow<BlockoutShapeGalleryWindow>("Shape Gallery");

        private void OnGUI()
        {
            var config = AssetDatabase.LoadAssetAtPath<BlockoutShapeSelectionConfig>(ConfigPath);

            if (config == null)
            {
                EditorGUILayout.HelpBox($"No BlockoutShapeSelectionConfig found at \"{ConfigPath}\". Every piece below is treated as enabled until one exists.", MessageType.Info);
                if (GUILayout.Button("Create Shape Selection Config", GUILayout.Width(220)))
                    config = CreateConfig();
                EditorGUILayout.Space(6);
            }

            var shapes = BlockoutShapeSet.AllShapes();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int row = 0; row * Columns < shapes.Count; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < Columns; col++)
                {
                    int index = row * Columns + col;
                    if (index >= shapes.Count)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }
                    DrawShapeCell(shapes[index], config);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawShapeCell(PolycubeShape shape, BlockoutShapeSelectionConfig config)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(CellWidth));

            Rect previewRect = GUILayoutUtility.GetRect(CellWidth, PreviewHeight);
            GUI.Box(previewRect, GUIContent.none);

            bool isEnabled = IsEnabled(config, shape.Name);
            DrawShapePreview(previewRect, shape, isEnabled ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f));

            EditorGUILayout.LabelField(shape.Name, EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(config == null))
            {
                if (GUILayout.Button(isEnabled ? "Disable" : "Enable"))
                    SetPieceEnabled(config, shape.Name, !isEnabled);
            }

            EditorGUILayout.EndVertical();
        }

        // Mirrors BlockoutShapeSet.IsEnabled's own convention: not listed (or no config at all)
        // defaults to enabled.
        private static bool IsEnabled(BlockoutShapeSelectionConfig config, string pieceName)
        {
            if (config == null) return true;
            foreach (var toggle in config.Pieces)
            {
                if (toggle.pieceName == pieceName) return toggle.enabled;
            }
            return true;
        }

        private static BlockoutShapeSelectionConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<BlockoutShapeSelectionConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BlockoutShapeGalleryWindow] Created {ConfigPath}.");
            return config;
        }

        private static void SetPieceEnabled(BlockoutShapeSelectionConfig config, string pieceName, bool enabled)
        {
            var serialized = new SerializedObject(config);
            var piecesProp = serialized.FindProperty("_pieces");

            int foundIndex = -1;
            for (int i = 0; i < piecesProp.arraySize; i++)
            {
                var nameProp = piecesProp.GetArrayElementAtIndex(i).FindPropertyRelative("pieceName");
                if (nameProp.stringValue == pieceName)
                {
                    foundIndex = i;
                    break;
                }
            }

            SerializedProperty target;
            if (foundIndex >= 0)
            {
                target = piecesProp.GetArrayElementAtIndex(foundIndex);
            }
            else
            {
                piecesProp.InsertArrayElementAtIndex(piecesProp.arraySize);
                target = piecesProp.GetArrayElementAtIndex(piecesProp.arraySize - 1);
                target.FindPropertyRelative("pieceName").stringValue = pieceName;
            }
            target.FindPropertyRelative("enabled").boolValue = enabled;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        // --- Isometric wireframe preview: precomputed 2D screen-space lines drawn via
        // Handles.DrawLine, no camera/Handles.matrix involved - reliable inside a plain
        // EditorWindow.OnGUI (unlike Handles.DrawWireCube, which expects a 3D scene camera). ---

        private static void DrawShapePreview(Rect rect, PolycubeShape shape, Color color)
        {
            var edges = CollectUniqueEdges(shape);
            if (edges.Count == 0) return;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (var edge in edges)
            {
                Vector2 a = IsoProject(edge.Item1);
                Vector2 b = IsoProject(edge.Item2);
                min = Vector2.Min(min, Vector2.Min(a, b));
                max = Vector2.Max(max, Vector2.Max(a, b));
            }

            Vector2 offset = rect.center - (min + max) * 0.5f;

            var previousColor = Handles.color;
            Handles.color = color;
            foreach (var edge in edges)
            {
                Vector3 a = IsoProject(edge.Item1) + offset;
                Vector3 b = IsoProject(edge.Item2) + offset;
                Handles.DrawLine(a, b);
            }
            Handles.color = previousColor;
        }

        private static Vector2 IsoProject(Vector3Int p)
        {
            float x = (p.x - p.z) * CosIso * CellScale;
            float y = (p.x + p.z) * SinIso * CellScale - p.y * CellScale;
            return new Vector2(x, y);
        }

        // Every cube contributes its 12 edges in absolute grid-corner coordinates; two adjacent
        // cubes sharing a face contribute the same 4 edges with identical endpoints, so
        // deduplicating (via the canonicalized tuple key) draws each seam once instead of
        // layering it - the difference between a clean voxel wireframe and a doubled-up mess.
        private static HashSet<(Vector3Int Item1, Vector3Int Item2)> CollectUniqueEdges(PolycubeShape shape)
        {
            var edges = new HashSet<(Vector3Int, Vector3Int)>();
            var corners = new Vector3Int[8];

            foreach (var cell in shape.Cells)
            {
                for (int i = 0; i < 8; i++) corners[i] = cell + CornerOffsets[i];

                foreach (var pair in CubeEdgePairs)
                    edges.Add(CanonicalEdge(corners[pair.a], corners[pair.b]));
            }

            return edges;
        }

        private static (Vector3Int, Vector3Int) CanonicalEdge(Vector3Int a, Vector3Int b) =>
            CompareVec(a, b) <= 0 ? (a, b) : (b, a);

        private static int CompareVec(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        // The 8 corners of a unit cube as (0/1, 0/1, 0/1) offsets, indexed 0..7 so index bits
        // (dx<<2 | dy<<1 | dz) match BuildCubeEdgePairs' bit-difference check below.
        private static Vector3Int[] BuildCornerOffsets()
        {
            var offsets = new Vector3Int[8];
            int i = 0;
            for (int dx = 0; dx <= 1; dx++)
                for (int dy = 0; dy <= 1; dy++)
                    for (int dz = 0; dz <= 1; dz++)
                        offsets[i++] = new Vector3Int(dx, dy, dz);
            return offsets;
        }

        // Two of the 8 corners are connected by a cube edge iff their indices differ in exactly
        // one bit (i.e. exactly one of dx/dy/dz differs) - 12 such pairs, matching a cube's edge
        // count.
        private static (int, int)[] BuildCubeEdgePairs()
        {
            var pairs = new List<(int, int)>();
            for (int a = 0; a < 8; a++)
                for (int b = a + 1; b < 8; b++)
                    if (DiffersInExactlyOneBit(a, b))
                        pairs.Add((a, b));
            return pairs.ToArray();
        }

        private static bool DiffersInExactlyOneBit(int a, int b)
        {
            int x = a ^ b;
            return x != 0 && (x & (x - 1)) == 0;
        }
    }
}
