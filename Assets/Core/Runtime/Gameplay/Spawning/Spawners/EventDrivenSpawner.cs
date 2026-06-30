using System;
using System.Collections.Generic;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;
using UnityEngine;

namespace hp55games.Mobile.Core.Gameplay.Spawning
{
    /// <summary>
    /// Generic spawner that reacts to an event and spawns a pooled object
    /// at the world-space position provided by the event itself.
    ///
    /// The spawner has no knowledge of lanes, cameras, or scene transforms.
    /// Spawn position authority belongs entirely to the command that raises the event.
    /// </summary>
    public abstract class EventDrivenSpawner<TEvent> : MonoBehaviour where TEvent : struct, IEvent
    {
        [Header("Pool Settings")]
        [SerializeField] private int _initialPoolSize = 10;
        [SerializeField] private Transform _parentForInstances;

        private IObjectPoolService _pool;
        private IEventBus _eventBus;
        private IDisposable _eventSub;

        private readonly HashSet<int> _warmedUpPrefabs = new HashSet<int>();

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _pool);
            ServiceRegistry.TryResolve(out _eventBus);

            if (_parentForInstances == null)
                _parentForInstances = transform;
        }

        private void OnEnable()
        {
            // Dispose before re-subscribing – guards against duplicate handlers
            // if OnEnable fires without a matching prior OnDisable.
            _eventSub?.Dispose();
            _eventSub = null;

            if (_eventBus != null)
                _eventSub = _eventBus.Subscribe<TEvent>(OnEventRaised);
        }

        private void OnDisable()
        {
            _eventSub?.Dispose();
            _eventSub = null;
        }

        private void OnEventRaised(TEvent evt)
        {
            if (_pool == null)
                return;

            GameObject prefab = ResolvePrefab(evt);
            if (prefab == null)
                return;

            PooledObject pooled = prefab.GetComponent<PooledObject>();
            if (pooled == null)
            {
                Debug.LogWarning($"[{GetType().Name}] Prefab '{prefab.name}' is missing a " +
                                 $"PooledObject component. Spawn skipped.");
                return;
            }

            WarmUpIfNeeded(pooled);

            int amount = Mathf.Max(1, GetAmount(evt));
            Vector3 spawnPos = GetSpawnPosition(evt);

            for (int i = 0; i < amount; i++)
            {
                GameObject go = _pool.Get(pooled, _parentForInstances);
                go.transform.position = spawnPos;
                go.SetActive(true);
                ConfigureSpawned(go, evt);
            }
        }

        private void WarmUpIfNeeded(PooledObject pooled)
        {
            int id = pooled.GetInstanceID();
            if (_warmedUpPrefabs.Contains(id))
                return;

            if (_initialPoolSize > 0)
                _pool.WarmUp(pooled, _initialPoolSize, _parentForInstances);

            _warmedUpPrefabs.Add(id);
        }

        protected abstract GameObject ResolvePrefab(TEvent evt);
        protected abstract Vector3 GetSpawnPosition(TEvent evt);
        protected abstract int GetAmount(TEvent evt);

        protected virtual void ConfigureSpawned(GameObject go, TEvent evt) { }
    }
}
