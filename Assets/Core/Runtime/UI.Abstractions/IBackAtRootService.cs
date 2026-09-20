using System;

namespace hp55games.Mobile.Core.UI
{
    /// <summary>
    /// Optional hook for "back pressed with nothing left to go back to" (no pause, no popup, page
    /// stack at root). Core raises it and knows nothing about who listens; game code subscribes
    /// while relevant (e.g. gameplay routes it to pause) and unsubscribes on exit. No subscriber = no-op.
    /// </summary>
    public interface IBackAtRootService
    {
        event Action BackAtRoot;
        void Raise();
    }

    public sealed class BackAtRootService : IBackAtRootService
    {
        public event Action BackAtRoot;
        public void Raise() => BackAtRoot?.Invoke();
    }
}
