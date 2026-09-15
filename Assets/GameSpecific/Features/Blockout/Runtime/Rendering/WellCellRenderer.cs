using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Rendering
{
    // Phase 5: pooled visual representation of well cells, via the template's
    // IObjectPoolService - no direct Instantiate/Destroy for per-cell show/hide. Deliberately
    // dumb about game meaning: BlockoutSpawner/PieceController decide WHICH cells are shown and
    // what a given color means (active vs locked); this class only knows how to show or hide a
    // cube at a grid position, pooled.
    public sealed class WellCellRenderer : MonoBehaviour
    {
        [Tooltip("Prefab shown per occupied cell (needs, or will get, a PooledObject component). If left empty, a plain temporary cube is created at runtime - assign a real prefab here once one exists (materials/mesh are Franci's manual job per Phase 5).")]
        [SerializeField] private PooledObject _cellPrefab;

        [Tooltip("Periodic Table GDD Phase 2 (Bezi): the 3 candy-shader material variants, swapped onto a cell's Renderer per ShowCell's materialCategory argument. Opaque also doubles as the fallback for every non-element skin (materialCategory == null) and for Opaque itself, so it should always be assigned once these exist - Metallic/Translucent only matter for element skins.")]
        [SerializeField] private Material _metallicMaterial;
        [SerializeField] private Material _opaqueMaterial;
        [SerializeField] private Material _translucentMaterial;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private IObjectPoolService _pool;
        private readonly Dictionary<Vector3Int, PooledObject> _shownCells = new();

        private void Awake()
        {
            if (!ServiceRegistry.TryResolve(out _pool))
            {
                Debug.LogError("[WellCellRenderer] IObjectPoolService is not registered.", this);
                enabled = false;
                return;
            }

            if (_cellPrefab == null)
            {
                _cellPrefab = CreateFallbackCellPrefab();
            }
        }

        // Shows (or, if already shown, just re-colors) a cube at gridPos. Position is applied
        // every call since ShowCell also doubles as "move the piece to its new position" for the
        // active piece - callers hide the old position and show the new one on every change.
        public void ShowCell(Vector3Int gridPos, Color color, PieceMaterialCategory? materialCategory = null)
        {
            if (!enabled) return;

            if (!_shownCells.TryGetValue(gridPos, out var instance))
            {
                var go = _pool.Get(_cellPrefab, transform);
                go.SetActive(true); // IObjectPoolService.Get doesn't guarantee this for a
                                     // never-before-pooled instance of this prefab
                instance = go.GetComponent<PooledObject>();
                _shownCells[gridPos] = instance;
            }

            instance.transform.position = (Vector3)gridPos;

            var renderer = instance.GetComponent<Renderer>();
            if (renderer == null) return;

            renderer.enabled = true;
            ApplyMaterialCategory(renderer, materialCategory);
            ApplyColor(renderer, color);
        }

        // Resolves to Opaque both when materialCategory is null (every non-element skin - Default,
        // Profondita, Juicy Clear - never had a category concept) and for Opaque itself, rather
        // than leaving the Renderer's material untouched: a pooled instance can be handed back by
        // IObjectPoolService after last being shown under a different skin's category (e.g. a
        // Translucent element), so "untouched" would mean stale, not "prefab default". Only skips
        // the swap if the resolved slot itself isn't assigned yet (materials not authored yet -
        // same "log nothing, just don't crash" fallback WellCellRenderer already uses for
        // _cellPrefab).
        private void ApplyMaterialCategory(Renderer renderer, PieceMaterialCategory? materialCategory)
        {
            var material = ResolveMaterial(materialCategory ?? PieceMaterialCategory.Opaque);
            if (material != null) renderer.sharedMaterial = material;
        }

        private Material ResolveMaterial(PieceMaterialCategory category) => category switch
        {
            PieceMaterialCategory.Metallic => _metallicMaterial,
            PieceMaterialCategory.Translucent => _translucentMaterial,
            _ => _opaqueMaterial
        };

        public void HideCell(Vector3Int gridPos)
        {
            if (!_shownCells.TryGetValue(gridPos, out var instance)) return;

            _pool.Release(instance);
            _shownCells.Remove(gridPos);
        }

        // Mirrors VoxelGrid.ClearLayerAndCollapse for the pooled visuals: captures the
        // position/color of every shown cell in layer y (appended to outClearedPositions/
        // outClearedColors, for a caller building a BlockoutClearContext), hides them, then
        // shifts every shown cell in the layers above down by one - without this, a cleared
        // layer's cubes would keep floating in place and everything above would drift out of
        // sync with the logical grid after the very first clear. width/depth/height are passed
        // in (this class doesn't own well dimensions); y must be processed in the same
        // highest-first order PlacementRules.ClearFullLayersTouchedBy cleared the grid in, when
        // more than one layer clears at once.
        public void CollapseLayer(int y, int width, int depth, int height, IList<Vector3Int> outClearedPositions, IList<Color> outClearedColors)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    var pos = new Vector3Int(x, y, z);
                    if (!_shownCells.ContainsKey(pos)) continue;

                    outClearedPositions?.Add(pos);
                    outClearedColors?.Add(GetCellColor(pos));
                    HideCell(pos);
                }
            }

            for (int layer = y; layer < height - 1; layer++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        MoveCellDown(new Vector3Int(x, layer + 1, z), new Vector3Int(x, layer, z));
                    }
                }
            }
        }

        // Relocates a shown cell from `from` to `to` (one layer down) if one is shown there, or
        // no-ops otherwise - `to` is guaranteed empty by the time this runs for a given layer
        // (CollapseLayer processes layers bottom-up from y, so whatever was shown at `to` was
        // already moved out, or hidden, in the previous iteration), exactly mirroring
        // VoxelGrid's own per-cell overwrite.
        private void MoveCellDown(Vector3Int from, Vector3Int to)
        {
            if (!_shownCells.TryGetValue(from, out var instance)) return;

            _shownCells.Remove(from);
            instance.transform.position = (Vector3)to;
            _shownCells[to] = instance;
        }

        // Read-only introspection for tests/debug tooling - not used by the show/hide mechanism
        // itself.
        public bool IsCellShown(Vector3Int gridPos) => _shownCells.ContainsKey(gridPos);
        public int ShownCellCount => _shownCells.Count;

        public Color GetCellColor(Vector3Int gridPos)
        {
            if (!_shownCells.TryGetValue(gridPos, out var instance)) return default;

            var renderer = instance.GetComponent<Renderer>();
            if (renderer == null) return default;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor(BaseColorId);
        }

        // Releases every currently-shown cell (active piece and locked alike) back to the pool -
        // the "leave no visual trace of the previous run" reset point a fresh
        // BlockoutSpawner.Initialize() needs.
        public void HideAll()
        {
            foreach (var instance in _shownCells.Values)
            {
                _pool.Release(instance);
            }
            _shownCells.Clear();
        }

        private static void ApplyColor(Renderer renderer, Color color)
        {
            // MaterialPropertyBlock, not renderer.material: many pooled instances share one
            // material, so setting .material.color here would instantiate a unique material per
            // cube again - exactly the leak Phase 4's cleanup fixed. Sets both property names
            // since CreatePrimitive's default material differs between URP (_BaseColor) and the
            // legacy/Standard shader (_Color).
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        // TEMPORARY fallback so this works before Franci wires a real prefab - mesh/materials are
        // explicitly Franci's manual job per Phase 5. Created once (not per cell) and only ever
        // handed to the pool afterward, never Instantiate()/Destroy()'d directly itself.
        private PooledObject CreateFallbackCellPrefab()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = "WellCellRenderer Fallback Cell (TEMP)";
            template.transform.SetParent(transform, false);

            // The template itself is never shown - only clones of it are, via the pool.
            var renderer = template.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;

            return template.AddComponent<PooledObject>();
        }
    }
}
