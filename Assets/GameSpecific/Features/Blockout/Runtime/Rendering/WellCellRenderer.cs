using System;
using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;

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

        // Two slots rather than one only because shader keywords can't be set per renderer via
        // MaterialPropertyBlock: _EMISSION has to be baked into a material. Everything else
        // (colour, metallic, smoothness, emission colour) is per cell via the property block.
        [Tooltip("URP Lit material for every cell whose skin has no emission. Per-skin metallic/smoothness come from CellSurface via MaterialPropertyBlock.")]
        [SerializeField] private Material _baseMaterial;
        [Tooltip("URP Lit material with Emission enabled (_EMISSION keyword), used only for skins with emission (Emission Intensity > 0). The actual glow colour is set per cell. Falls back to Base Material if unassigned.")]
        [SerializeField] private Material _emissiveMaterial;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private IObjectPoolService _pool;

        // "Well" layer, set on every cell taken from the pool so the well's dedicated lights/probe
        // (culling mask Well only) reach it regardless of the prefab's own layer. -1 if the layer
        // doesn't exist (cells then keep whatever layer they already had).
        private int _wellLayer = -1;

        private readonly Dictionary<Vector3Int, PooledObject> _shownCells = new();

        private void Awake()
        {
            _wellLayer = LayerMask.NameToLayer("Well");

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
        // surface: null for non-element skins (base material's own PBR values).
        public void ShowCell(Vector3Int gridPos, Color color, CellSurface? surface = null)
        {
            if (!enabled) return;

            if (!_shownCells.TryGetValue(gridPos, out var instance))
            {
                var go = _pool.Get(_cellPrefab, transform);
                go.SetActive(true); // IObjectPoolService.Get doesn't guarantee this for a
                                     // never-before-pooled instance of this prefab
                if (_wellLayer >= 0) go.layer = _wellLayer;
                instance = go.GetComponent<PooledObject>();
                _shownCells[gridPos] = instance;
            }

            instance.transform.position = (Vector3)gridPos;

            var renderer = instance.GetComponent<Renderer>();
            if (renderer == null) return;

            renderer.enabled = true;
            ApplyAppearance(renderer, color, surface);
        }

        // Relocates all cells of the active piece in one transaction so vertical pieces can move
        // through overlapping old/new grid coordinates without dictionary collisions. The pooled
        // instances are re-keyed at the destination, while their transforms are placed at the
        // interpolated visual positions supplied by the caller.
        public void MoveCells(IReadOnlyList<Vector3Int> from, IReadOnlyList<Vector3Int> to, IReadOnlyList<Vector3> visualPositions)
        {
            if (!enabled || from == null || to == null || visualPositions == null ||
                from.Count != to.Count || from.Count != visualPositions.Count) return;

            var instances = new PooledObject[from.Count];
            for (int i = 0; i < from.Count; i++)
            {
                if (!_shownCells.TryGetValue(from[i], out instances[i])) return;
            }

            for (int i = 0; i < from.Count; i++)
                _shownCells.Remove(from[i]);

            for (int i = 0; i < to.Count; i++)
            {
                instances[i].transform.position = visualPositions[i];
                _shownCells[to[i]] = instances[i];
            }
        }

        // Updates only the transform of an already-rekeyed active cell during interpolation.
        // Locked cells never call this method and therefore remain static.
        public void SetCellVisualPosition(Vector3Int gridPos, Vector3 visualPosition)
        {
            if (!_shownCells.TryGetValue(gridPos, out var instance)) return;
            instance.transform.position = visualPosition;
        }

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
        // more than one layer clears at once. levelColor (optional): colour for a given level -
        // every shifted cell is recoloured to its new level's colour, so locked cubes keep
        // matching their actual level after a clear.
        public void CollapseLayer(int y, int width, int depth, int height, IList<Vector3Int> outClearedPositions, IList<Color> outClearedColors, Func<int, Color> levelColor = null)
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
                        MoveCellDown(new Vector3Int(x, layer + 1, z), new Vector3Int(x, layer, z), levelColor);
                    }
                }
            }
        }

        // Relocates a shown cell from `from` to `to` (one layer down) if one is shown there, or
        // no-ops otherwise - `to` is guaranteed empty by the time this runs for a given layer
        // (CollapseLayer processes layers bottom-up from y, so whatever was shown at `to` was
        // already moved out, or hidden, in the previous iteration), exactly mirroring
        // VoxelGrid's own per-cell overwrite.
        private void MoveCellDown(Vector3Int from, Vector3Int to, Func<int, Color> levelColor)
        {
            if (!_shownCells.TryGetValue(from, out var instance)) return;

            _shownCells.Remove(from);
            instance.transform.position = (Vector3)to;
            _shownCells[to] = instance;

            if (levelColor != null)
            {
                var renderer = instance.GetComponent<Renderer>();
                if (renderer != null) RecolorKeepingSurface(renderer, levelColor(to.y));
            }
        }

        // Changes only the colour, merging into the cell's existing property block - unlike
        // ApplyAppearance, which rewrites the block from scratch - so the metallic/smoothness/
        // emission set when the cell was shown survive the recolour.
        private static void RecolorKeepingSurface(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
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

        // Always swaps the material (rather than leaving it untouched) and always writes a fresh
        // property block (rather than merging into the existing one): IObjectPoolService can
        // hand back an instance last shown under a different skin - e.g. an emissive, highly
        // metallic element - so anything not reset here would leak into this skin's cells. Only
        // skips the swap if the slot itself isn't assigned yet.
        private void ApplyAppearance(Renderer renderer, Color color, CellSurface? surface)
        {
            var material = surface is { IsEmissive: true } && _emissiveMaterial != null ? _emissiveMaterial : _baseMaterial;
            if (material != null) renderer.sharedMaterial = material;

            // MaterialPropertyBlock, not renderer.material: many pooled instances share one
            // material, so setting .material.color here would instantiate a unique material per
            // cube again - exactly the leak Phase 4's cleanup fixed. Sets both colour property
            // names since CreatePrimitive's default material differs between URP (_BaseColor)
            // and the legacy/Standard shader (_Color).
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);

            if (surface is { } s)
            {
                block.SetFloat(MetallicId, s.Metallic);
                block.SetFloat(SmoothnessId, s.Smoothness);
                // SetVector, not SetColor: Emission is already linear HDR, and SetColor would
                // gamma-convert it again in a linear-colour-space project.
                block.SetVector(EmissionColorId, s.Emission);
            }

            renderer.SetPropertyBlock(block);
        }

        // TEMPORARY fallback so this works before Franci wires a real prefab - mesh/materials are
        // explicitly Franci's manual job per Phase 5. Created once (not per cell) and only ever
        // handed to the pool afterward, never Instantiate()/Destroy()'d directly itself.
        private PooledObject CreateFallbackCellPrefab()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = "WellCellRenderer Fallback Cell";
            template.transform.SetParent(transform, false);

            // The template itself is never shown - only clones of it are, via the pool.
            var renderer = template.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;

            return template.AddComponent<PooledObject>();
        }
    }
}
