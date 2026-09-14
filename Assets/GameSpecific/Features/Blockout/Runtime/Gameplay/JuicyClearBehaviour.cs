using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;

namespace hp55games.Blockout.Gameplay
{
    // Technical Doc Phase 5 / skin GDD "Juicy Clear": cleared cells become physical objects with
    // gravity switched off and an upward+scattered launch impulse, instead of just disappearing -
    // a moment of release/satisfaction. Pooled via IObjectPoolService, matching WellCellRenderer's
    // own pattern for repeated spawn/despawn objects. This class only fires the one-shot spawn +
    // impulse per cleared cell - each spawned instance returns itself to the pool afterward via
    // DespawnAfterSeconds on the prefab (already in the template), not tracked or updated here.
    [CreateAssetMenu(fileName = "JuicyClearBehaviour", menuName = "hp55games/Blockout/Clear Behaviours/Juicy Clear")]
    public sealed class JuicyClearBehaviour : BlockoutClearBehaviour
    {
        [Tooltip("Physical object spawned per cleared cell. Needs a Rigidbody, a Renderer, a PooledObject, and (strongly recommended) DespawnAfterSeconds so it returns to the pool on its own - otherwise spawned objects accumulate forever. Left unassigned, a clear logs an error and spawns nothing (Franci's manual job to build a real prefab - mesh/material are cosmetic, not blocking the logic).")]
        [SerializeField] private PooledObject _physicsObjectPrefab;

        [Tooltip("Upward speed given to each spawned object - combined with gravity switched off on its Rigidbody, this reads as \"released upward\" rather than falling.")]
        [SerializeField] private float _launchSpeed = 4f;

        [Tooltip("Random horizontal speed added on top of the upward launch, so a multi-cell clear doesn't look like every cell shooting straight up in perfect unison.")]
        [SerializeField] private float _horizontalScatterSpeed = 1.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public override void OnLayersCleared(BlockoutClearContext context)
        {
            if (_physicsObjectPrefab == null)
            {
                Debug.LogError("[JuicyClearBehaviour] _physicsObjectPrefab is not assigned - clear happened with no physics reaction. Assign a prefab (Rigidbody + Renderer + PooledObject + DespawnAfterSeconds) in the Inspector.", this);
                return;
            }

            if (!ServiceRegistry.TryResolve<IObjectPoolService>(out var pool))
            {
                Debug.LogError("[JuicyClearBehaviour] IObjectPoolService is not registered - clear happened with no physics reaction.");
                return;
            }

            for (int i = 0; i < context.ClearedCellPositions.Count; i++)
            {
                Spawn(pool, context.ClearedCellPositions[i], context.ClearedCellColors[i]);
            }
        }

        private void Spawn(IObjectPoolService pool, Vector3Int cellPosition, Color color)
        {
            var go = pool.Get(_physicsObjectPrefab, null);
            go.SetActive(true); // IObjectPoolService.Get doesn't guarantee this for a
                                 // never-before-pooled instance of this prefab
            go.transform.SetPositionAndRotation((Vector3)cellPosition, Quaternion.identity);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) ApplyColor(renderer, color);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.velocity = new Vector3(
                    Random.Range(-_horizontalScatterSpeed, _horizontalScatterSpeed),
                    _launchSpeed,
                    Random.Range(-_horizontalScatterSpeed, _horizontalScatterSpeed));
            }
        }

        // Same MaterialPropertyBlock approach as WellCellRenderer.ApplyColor: sets both property
        // names since a primitive's default material differs between URP (_BaseColor) and the
        // legacy/Standard shader (_Color); never mutate renderer.material directly here, that
        // would instantiate a unique material per spawned instance (the exact leak
        // MaterialPropertyBlock avoids).
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
