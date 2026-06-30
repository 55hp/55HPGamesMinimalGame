using System;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Gameplay.Events;
using UnityEngine;

namespace hp55games.Mobile.Core.CommandSequence
{
    public sealed class CommandSequencer : MonoBehaviour
    {
        [Header("Sequence Data")]
        [SerializeField] private CommandSequenceAsset _sequence;

        [Header("Runtime")]
        [SerializeField] private bool _startOnGameStarted = true;

        private IEventBus _eventBus;
        private IDisposable _gameStartedSub;

        private System.Random _random;
        private bool _isRunning;
        private int _currentBeatIndex;
        private float _currentTime;

        private void Awake()
        {
            ServiceRegistry.TryResolve(out _eventBus);

            if (_eventBus != null && _startOnGameStarted)
            {
                _gameStartedSub = _eventBus.Subscribe<GameStartedEvent>(OnGameStarted);
            }
        }

        private void OnDestroy()
        {
            _gameStartedSub?.Dispose();
        }

        private void OnGameStarted(GameStartedEvent evt)
        {
            StartSequence();
        }

        private void Update()
        {
            if (!_isRunning || _sequence == null || _eventBus == null)
                return;

            _currentTime += Time.deltaTime;

            while (_currentBeatIndex < _sequence.Beats.Count)
            {
                var beat = _sequence.Beats[_currentBeatIndex];

                if (_currentTime < beat.Time)
                    break;

                ExecuteBeat(beat);
                _currentBeatIndex++;
            }

            if (_currentBeatIndex >= _sequence.Beats.Count)
            {
                if (_sequence.Loop)
                {
                    _currentBeatIndex = 0;
                    _currentTime = -_sequence.LoopDelay;
                }
                else
                {
                    _isRunning = false;
                }
            }
        }

        private void ExecuteBeat(SequenceBeat beat)
        {
            if (beat.Command == null)
                return;

            var context = new SequenceContext(_eventBus, _random, _currentTime, _currentBeatIndex);
            beat.Command.Execute(context);
        }

        public void StartSequence()
        {
            if (_sequence == null)
                return;

            _random = new System.Random(_sequence.Seed);
            _currentBeatIndex = 0;
            _currentTime = -_sequence.StartingDelay;
            _isRunning = true;
        }

        public void StopSequence()
        {
            _isRunning = false;
        }

        public void ResetSequence()
        {
            _currentBeatIndex = 0;
            _currentTime = -_sequence.StartingDelay;
        }
    }
}
