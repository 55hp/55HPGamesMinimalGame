using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;

namespace hp55games.Mobile.Core.Gameplay.Spawning
{
    /// <summary>
    /// Despawns a pooled object (returns it to IObjectPoolService)
    /// when it goes outside the specified bounds.
    /// checkZ is disabled by default to preserve the original 2D behavior.
    /// </summary>
    [RequireComponent(typeof(PooledObject))]
    public sealed class DespawnWhenOutOfBounds : MonoBehaviour
    {
        [Header("World Bounds – XY")]
        public float minX = -10f;
        public float maxX = 10f;
        public float minY = -5f;
        public float maxY = 5f;

        [Header("World Bounds – Z (optional)")]
        [Tooltip("Enable to also despawn when the object exits the Z bounds below. Disabled by default.")]
        public bool checkZ = false;
        public float minZ = -10f;
        public float maxZ = 10f;

        private PooledObject _pooled;
        private IObjectPoolService _pool;

        private void Awake()
        {
            _pooled = GetComponent<PooledObject>();
            ServiceRegistry.TryResolve(out _pool);
        }

        private void Update()
        {
            if (_pool == null)
                return;

            var pos = transform.position;

            bool outOfBounds = pos.x < minX || pos.x > maxX ||
                               pos.y < minY || pos.y > maxY;

            // Z check is opt-in so existing scenes with no Z movement are unaffected.
            if (!outOfBounds && checkZ)
                outOfBounds = pos.z < minZ || pos.z > maxZ;

            if (outOfBounds)
                _pool.Release(_pooled);
        }
    }
}
