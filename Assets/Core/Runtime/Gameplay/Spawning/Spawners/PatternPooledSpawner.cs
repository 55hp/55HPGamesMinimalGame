using System;
using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Gameplay.Events;
using hp55games.Mobile.Core.Pooling;

namespace hp55games.Mobile.Core.Gameplay.Spawning
{
    public sealed class PatternPooledSpawner : MonoBehaviour
    {
        [Header("Pool / Prefabs")]
        [SerializeField] private List<PooledObject> _prefabs = new List<PooledObject>();
        [SerializeField] private int _initialPoolSizePerPrefab = 4;
        [SerializeField] private Transform _parentForInstances;

        [Header("Spawn Point")]
        [SerializeField]
        [Tooltip("REQUIRED: Transform that defines the spawn position and rotation. Create a child GameObject to use as spawn point.")]
        private Transform _spawnPoint;

        [Header("Pattern Sequence")]
        [SerializeField] private List<int> _patternSequence = new List<int>();

        [Header("Start / Stop")]
        [SerializeField] private bool _startActive = false;
        [SerializeField] private float _initialDelay = 0f;

        [Header("Timing")]
        [SerializeField] private float _spawnInterval = 2f;

        private IObjectPoolService _pool;
        private IEventBus _eventBus;
        private IDisposable _gameStartedSub;

        private bool _isSpawning;
        private float _timer;
        private int _patternSequenceIndex;

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _pool);
            ServiceRegistry.TryResolve(out _eventBus);

            if (_eventBus != null)
            {
                _gameStartedSub = _eventBus.Subscribe<GameStartedEvent>(OnGameStarted);
            }

            if (_parentForInstances == null)
                _parentForInstances = transform;

            if (_pool != null && _prefabs != null && _prefabs.Count > 0 && _initialPoolSizePerPrefab > 0)
            {
                foreach (var prefab in _prefabs)
                {
                    if (prefab != null)
                    {
                        _pool.WarmUp(prefab, _initialPoolSizePerPrefab, _parentForInstances);
                    }
                }
            }

            _timer = -_initialDelay;
            _patternSequenceIndex = 0;
            _isSpawning = _startActive;
        }

        private void OnDestroy()
        {
            _gameStartedSub?.Dispose();
        }

        private void OnGameStarted(GameStartedEvent evt)
        {
            _isSpawning = true;
        }

        private void Update()
        {
            if (_pool == null || _prefabs == null || _prefabs.Count == 0)
                return;

            if (_spawnPoint == null)
                return;

            if (!_isSpawning)
                return;

            _timer += Time.deltaTime;

            if (_timer >= _spawnInterval)
            {
                _timer = 0f;
                SpawnOne();
            }
        }

        private void SpawnOne()
        {
            int prefabIndex = GetNextPrefabIndex();

            if (prefabIndex < 0 || prefabIndex >= _prefabs.Count)
            {
                Debug.LogWarning($"[PatternPooledSpawner] Invalid prefab index: {prefabIndex}");
                return;
            }

            var prefab = _prefabs[prefabIndex];
            if (prefab == null)
            {
                Debug.LogWarning($"[PatternPooledSpawner] Prefab at index {prefabIndex} is null");
                return;
            }

            var go = _pool.Get(prefab, _parentForInstances);
            var tr = go.transform;

            tr.position = _spawnPoint.position;
            tr.rotation = _spawnPoint.rotation;

            go.SetActive(true);
        }

        private int GetNextPrefabIndex()
        {
            if (_patternSequence == null || _patternSequence.Count == 0)
            {
                return 0;
            }

            int index = Mathf.Clamp(_patternSequence[_patternSequenceIndex], 0, _prefabs.Count - 1);

            _patternSequenceIndex++;
            if (_patternSequenceIndex >= _patternSequence.Count)
            {
                _patternSequenceIndex = 0;
            }

            return index;
        }

        public void StartSpawning()
        {
            if (_isSpawning)
                return;

            _isSpawning = true;
        }

        public void StopSpawning()
        {
            _isSpawning = false;
        }
    }
}
