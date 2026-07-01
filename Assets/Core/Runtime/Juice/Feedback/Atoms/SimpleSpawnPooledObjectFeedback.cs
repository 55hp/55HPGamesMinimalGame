using System.Collections;
using System.Collections.Generic;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;
using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Spawns a pooled GameObject at the origin transform position when activated.
    /// After _lifetime seconds the instance is returned to the pool.
    /// If no origin is provided, spawns at this GameObject's world position.
    /// Requires IObjectPoolService to be registered in ServiceRegistry.
    /// </summary>
    public sealed class SimpleSpawnPooledObjectFeedback : MonoBehaviour, IFeedback
    {
        [Header("Spawn")]
        [SerializeField] private PooledObject _prefab;
        [SerializeField] private float _lifetime = 0.5f;
        [Tooltip("Number of instances to pre-allocate in the pool on Awake. " +
                 "Set this to the maximum number of concurrent activations expected " +
                 "across ALL instances of this component that share the same prefab.")]
        [SerializeField] [Min(0)] private int _warmUpCount = 1;

        [Header("Timing")]
        [Tooltip("Seconds to wait before the object is spawned.")]
        [SerializeField] private float _startDelay = 0f;

        private IObjectPoolService _pool;

        // Tracks in-flight GameObjects so OnDisable can return them to the pool
        // in case Unity cancels the ReleaseAfter coroutine before it completes.
        private readonly List<GameObject> _inFlight = new();

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _pool);

            if (_pool == null)
            {
                Debug.LogWarning("[SpawnFeedback] IObjectPoolService not found in ServiceRegistry.", this);
                return;
            }

            if (_prefab == null)
            {
                Debug.LogWarning("[SpawnFeedback] _prefab is null — WarmUp skipped.", this);
                return;
            }

            // Pre-alloca le istanze richieste. Se più componenti condividono lo stesso
            // _prefab, ognuno deve dichiarare la propria quota: il totale nel pool
            // sarà la somma dei _warmUpCount di tutti i componenti che usano quel prefab.
            if (_warmUpCount > 0)
                _pool.WarmUp(_prefab, _warmUpCount);
        }

        public void Activate(Transform origin = null)
        {
            if (_prefab == null || _pool == null)
                return;

            // Sample position immediately — origin may be destroyed before the delay elapses.
            Vector3 spawnPosition = origin != null ? origin.position : transform.position;

            if (_startDelay > 0f)
            {
                StartCoroutine(DelayedSpawn(spawnPosition));
                return;
            }

            Spawn(spawnPosition);
        }

        private IEnumerator DelayedSpawn(Vector3 spawnPosition)
        {
            yield return new WaitForSeconds(_startDelay);
            Spawn(spawnPosition);
        }

        private void Spawn(Vector3 spawnPosition)
        {
            // Disable before positioning to avoid a one-frame appearance at the
            // wrong world position on both the "new instance" and "reuse" paths.
            var go = _pool.Get(_prefab);
            if (go == null)
                return;

            go.SetActive(false);
            go.transform.position = spawnPosition;
            go.SetActive(true);

            _inFlight.Add(go);

            StartCoroutine(ReleaseAfter(go, _lifetime));
        }

        private void OnDisable()
        {
            // Coroutines are stopped by Unity when the MonoBehaviour is disabled.
            // Release any in-flight objects that were not yet returned to the pool.
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                var go = _inFlight[i];
                if (go == null || !go.activeInHierarchy) continue;

                var pooled = go.GetComponent<PooledObject>();
                if (pooled != null)
                    _pool.Release(pooled);
                else
                    go.SetActive(false);
            }
            _inFlight.Clear();
        }

        private IEnumerator ReleaseAfter(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);

            _inFlight.Remove(go);

            // Guard: the object might have already been released externally.
            if (go == null || !go.activeInHierarchy)
                yield break;

            var pooled = go.GetComponent<PooledObject>();
            if (pooled != null)
                _pool.Release(pooled);
            else
                go.SetActive(false);
        }
    }
}
