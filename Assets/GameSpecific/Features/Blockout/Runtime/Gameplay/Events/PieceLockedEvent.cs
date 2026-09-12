using hp55games.Mobile.Core.Architecture;
using UnityEngine;

namespace hp55games.Blockout.Gameplay.Events
{
    public sealed class PieceLockedEvent : IEvent
    {
        public Vector3Int GridPosition;
    }
}
