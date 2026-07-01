using System;
using hp55games.Mobile.Core.Architecture;

namespace hp55games.Mobile.Core.CommandSequence
{
    public sealed class SequenceContext
    {
        public IEventBus EventBus { get; }
        public Random Random { get; }
        public float CurrentTime { get; }
        public int CurrentBeatIndex { get; }

        public SequenceContext(IEventBus eventBus, Random random, float currentTime, int currentBeatIndex)
        {
            EventBus = eventBus;
            Random = random;
            CurrentTime = currentTime;
            CurrentBeatIndex = currentBeatIndex;
        }
    }
}
