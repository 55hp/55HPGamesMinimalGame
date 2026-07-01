using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Contract for any single-shot feedback effect.
    /// Callers trigger the feedback without knowing its implementation.
    ///
    /// origin — optional world-space transform passed by the caller.
    /// Implementations that don't need positional data ignore it.
    /// Implementations like SimpleSpawnPooledObjectFeedback use it to place the spawned object.
    /// </summary>
    public interface IFeedback
    {
        void Activate(Transform origin = null);
    }
}
