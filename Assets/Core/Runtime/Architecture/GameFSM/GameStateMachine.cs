using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace hp55games.Mobile.Core.Architecture.States
{
    public sealed class GameStateMachine : IGameStateMachine
    {
        private readonly object _lock = new();
        private IGameState _current;
        private bool _isTransitioning;
        private CancellationTokenSource _cts;

        public IGameState Current => _current;

        public async Task ChangeStateAsync(IGameState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));

            CancellationTokenSource myCts;

            // Try to acquire the right to run a transition.
            // If another transition is in progress, request its cancellation and wait.
            while (true)
            {
                lock (_lock)
                {
                    if (!_isTransitioning)
                    {
                        _isTransitioning = true;
                        // cancel any previous CTS reference (defensive)
                        _cts?.Cancel();
                        _cts = new CancellationTokenSource();
                        myCts = _cts;
                        break;
                    }
                    else
                    {
                        // ask the current transition to cancel; then yield and retry acquiring the lock
                        _cts?.Cancel();
                    }
                }

                // Give the running transition a chance to observe cancellation and finish.
                await Task.Yield();
            }

            try
            {
                var ct = myCts.Token;

                if (_current != null)
                {
                    try { await _current.ExitAsync(ct); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { Debug.LogException(ex); }
                }

                _current = next;

                try { await _current.EnterAsync(ct); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            finally
            {
                lock (_lock) { _isTransitioning = false; }
            }
        }

        public void CancelCurrent() => _cts?.Cancel();
    }
}