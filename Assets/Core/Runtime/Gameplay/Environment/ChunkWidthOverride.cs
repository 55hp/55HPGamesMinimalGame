using UnityEngine;

namespace hp55games.Mobile.Core.Gameplay.Environment
{
    /// <summary>
    /// Declares an explicit logical width for a chunk prefab.
    /// Place this on the root GameObject of any chunk whose Renderer bounds
    /// are unreliable at instantiation time (e.g. particle-only chunks whose
    /// ParticleSystemRenderer reports zero bounds before the first emission).
    /// HorizontalChunkScroller reads this value in ComputeChunkWidth and skips
    /// the Renderer aggregation entirely when the override is present.
    /// </summary>
    public sealed class ChunkWidthOverride : MonoBehaviour
    {
        [Tooltip("Logical width of this chunk in world units. " +
                 "Must be greater than zero.")]
        [Min(0.001f)]
        [SerializeField] private float _width = 1f;

        public float Width => _width;
    }
}
