using System;
using System.Collections.Generic;
using UnityEngine;

namespace hp55games.Mobile.Core.CommandSequence
{
    [CreateAssetMenu(fileName = "CommandSequence", menuName = "Core/Command Sequence")]
    public sealed class CommandSequenceAsset : ScriptableObject
    {
        [Tooltip("Deterministic seed for Random generation.")]
        public int Seed = 123;

        [Tooltip("Delay in seconds before the first beat executes. Cannot be negative.")]
        public float StartingDelay = 3f;

        [Tooltip("If true, sequence loops when finished.")]
        public bool Loop = false;

        [Tooltip("Delay in seconds between the end of a loop and the start of the next one. Only used when Loop is true.")]
        public float LoopDelay = 3f;

        [Tooltip("Sequence of timed beats to execute.")]
        public List<SequenceBeat> Beats = new();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (StartingDelay < 0f) StartingDelay = 0f;
            if (LoopDelay < 0f) LoopDelay = 0f;
        }
#endif
    }

    [Serializable]
    public sealed class SequenceBeat
    {
        [Tooltip("Time in seconds when this beat executes.")]
        public float Time;

        [Tooltip("Command to execute at this time.")]
        [SerializeReference]
        public ISequenceCommand Command;
    }
}
