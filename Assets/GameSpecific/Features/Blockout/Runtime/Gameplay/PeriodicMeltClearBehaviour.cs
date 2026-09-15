using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Config;
using hp55games.Mobile.Core.Pooling;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Gameplay
{
    // Periodic Table GDD "melt" clear reaction: instead of just disappearing (default skin) or
    // launching upward (Juicy Clear), each cleared layer ejects 24 spherules - 6 per side of the
    // well's 4 perimeter walls, at that layer's height - see JuicyClearBehaviour for the
    // pooling/spawn pattern this mirrors. One shared asset instance is referenced by all 118
    // BlockoutSkin-element assets (see BlockoutPeriodicElementImporter): it has no per-element
    // data of its own, it reads which element is active - and that element's DensityNormalized -
    // from IBlockoutSkinService.ActiveSkin at clear time instead.
    [CreateAssetMenu(fileName = "PeriodicMeltClearBehaviour", menuName = "hp55games/Blockout/Clear Behaviours/Periodic Melt")]
    public sealed class PeriodicMeltClearBehaviour : BlockoutClearBehaviour
    {
        private const int SpherulesPerSide = 6;

        // Matches BlockoutWellWireframe's wall convention: each grid cell occupies +/- this much
        // around its integer coordinate, so the perimeter wall sits exactly at the well's
        // physical boundary rather than at the center of the outermost cell.
        private const float CellHalfExtent = 0.5f;

        [Tooltip("Physical object spawned per spherule (6 per side x 4 sides x cleared layer = 24 per layer). Needs a Rigidbody, a Renderer, a PooledObject, and (strongly recommended) DespawnAfterSeconds so it returns to the pool on its own - otherwise spawned objects accumulate forever. Left unassigned, a clear logs an error and spawns nothing (Franci/Bezi's manual job to build a real prefab - mesh/material are cosmetic, not blocking the logic).")]
        [SerializeField] private PooledObject _spherulePrefab;

        [Tooltip("Maps the active element's DensityNormalized (0 = lightest, 1 = densest) to the spherules' initial vertical speed - positive launches upward, negative launches downward, near-zero at the midpoint reads as a normal gravitational fall. Gravity stays ON (unlike Juicy Clear): this curve is only the initial kick, not a sustained force. Tarable in the Inspector without recompiling, per the Technical Doc.")]
        [SerializeField]
        private AnimationCurve _densityToVerticalImpulse = new AnimationCurve(
            new Keyframe(0f, 3f),
            new Keyframe(0.5f, 0f),
            new Keyframe(1f, -3f));

        [Tooltip("Outward speed (away from the well's center) given to every spherule, on top of the density-driven vertical impulse - reads as \"expelled from the perimeter\" rather than spawning already overlapping the wall.")]
        [SerializeField] private float _outwardEjectSpeed = 2f;

        [Tooltip("Random horizontal speed added on top of the outward ejection, so the 6 spherules on a side don't move in perfect lockstep.")]
        [SerializeField] private float _horizontalScatterSpeed = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public override void OnLayersCleared(BlockoutClearContext context)
        {
            if (_spherulePrefab == null)
            {
                Debug.LogError("[PeriodicMeltClearBehaviour] _spherulePrefab is not assigned - clear happened with no melt reaction. Assign a prefab (Rigidbody + Renderer + PooledObject + DespawnAfterSeconds) in the Inspector.", this);
                return;
            }

            if (!ServiceRegistry.TryResolve<IObjectPoolService>(out var pool))
            {
                Debug.LogError("[PeriodicMeltClearBehaviour] IObjectPoolService is not registered - clear happened with no melt reaction.");
                return;
            }

            if (!ServiceRegistry.TryResolve<IConfigCatalogService>(out var catalogService))
            {
                Debug.LogError("[PeriodicMeltClearBehaviour] IConfigCatalogService is not registered - clear happened with no melt reaction.");
                return;
            }

            var wellConfig = catalogService.Get<BlockoutWellConfig>();
            if (wellConfig == null) return; // missing config - already logged by Get<T>

            var color = context.ClearedCellColors != null && context.ClearedCellColors.Count > 0
                ? context.ClearedCellColors[0]
                : Color.white;
            float densityNormalized = ResolveActiveDensityNormalized();

            foreach (var y in DistinctLayerYs(context.ClearedCellPositions))
            {
                SpawnPerimeter(pool, wellConfig.Width, wellConfig.Depth, y, color, densityNormalized);
            }
        }

        // The active skin's own density, not anything derived from the cleared cells - a
        // multi-material future (palette-per-shape, explicitly out of scope per the GDD) would
        // need to look this up per cell instead, but today every piece under one skin shares one
        // element.
        private static float ResolveActiveDensityNormalized()
        {
            if (!ServiceRegistry.TryResolve<IBlockoutSkinService>(out var skinService)) return 0.5f;
            var skin = skinService.ActiveSkin;
            return skin != null ? skin.DensityNormalized : 0.5f;
        }

        private static IReadOnlyList<int> DistinctLayerYs(IReadOnlyList<Vector3Int> positions)
        {
            var seen = new List<int>();
            if (positions == null) return seen;

            foreach (var position in positions)
            {
                if (!seen.Contains(position.y)) seen.Add(position.y);
            }
            return seen;
        }

        private void SpawnPerimeter(IObjectPoolService pool, int width, int depth, int y, Color color, float densityNormalized)
        {
            float xMin = -CellHalfExtent, xMax = width - 1 + CellHalfExtent;
            float zMin = -CellHalfExtent, zMax = depth - 1 + CellHalfExtent;

            SpawnSide(pool, isXFixed: true, fixedValue: xMin, uMin: zMin, uMax: zMax, y: y, color: color, densityNormalized: densityNormalized, outward: new Vector3(-1f, 0f, 0f));
            SpawnSide(pool, isXFixed: true, fixedValue: xMax, uMin: zMin, uMax: zMax, y: y, color: color, densityNormalized: densityNormalized, outward: new Vector3(1f, 0f, 0f));
            SpawnSide(pool, isXFixed: false, fixedValue: zMin, uMin: xMin, uMax: xMax, y: y, color: color, densityNormalized: densityNormalized, outward: new Vector3(0f, 0f, -1f));
            SpawnSide(pool, isXFixed: false, fixedValue: zMax, uMin: xMin, uMax: xMax, y: y, color: color, densityNormalized: densityNormalized, outward: new Vector3(0f, 0f, 1f));
        }

        // One well wall: SpherulesPerSide spherules evenly spaced along [uMin, uMax] (Z for an
        // X-fixed wall, X for a Z-fixed wall), at the fixed wall coordinate and the cleared
        // layer's height y.
        private void SpawnSide(IObjectPoolService pool, bool isXFixed, float fixedValue, float uMin, float uMax, int y, Color color, float densityNormalized, Vector3 outward)
        {
            for (int i = 0; i < SpherulesPerSide; i++)
            {
                float t = (i + 0.5f) / SpherulesPerSide;
                float u = Mathf.Lerp(uMin, uMax, t);
                var position = isXFixed ? new Vector3(fixedValue, y, u) : new Vector3(u, y, fixedValue);

                Spawn(pool, position, color, densityNormalized, outward);
            }
        }

        private void Spawn(IObjectPoolService pool, Vector3 position, Color color, float densityNormalized, Vector3 outward)
        {
            var go = pool.Get(_spherulePrefab, null);
            go.SetActive(true); // IObjectPoolService.Get doesn't guarantee this for a
                                 // never-before-pooled instance of this prefab
            go.transform.SetPositionAndRotation(position, Quaternion.identity);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) ApplyColor(renderer, color);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float vertical = _densityToVerticalImpulse.Evaluate(densityNormalized);
                var horizontal = outward * _outwardEjectSpeed + new Vector3(
                    Random.Range(-_horizontalScatterSpeed, _horizontalScatterSpeed),
                    0f,
                    Random.Range(-_horizontalScatterSpeed, _horizontalScatterSpeed));

                rb.useGravity = true; // unlike Juicy Clear: this is a kick, not a sustained lift/hold
                rb.velocity = new Vector3(horizontal.x, vertical, horizontal.z);
            }
        }

        // Same MaterialPropertyBlock approach as JuicyClearBehaviour/WellCellRenderer: sets both
        // property names since a primitive's default material differs between URP (_BaseColor)
        // and the legacy/Standard shader (_Color); never mutate renderer.material directly here.
        private static void ApplyColor(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
