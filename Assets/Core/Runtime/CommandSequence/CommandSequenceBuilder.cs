using System.Collections.Generic;
using UnityEngine;

namespace hp55games.Mobile.Core.CommandSequence
{
    public static class CommandSequenceBuilder
    {
        public static CommandSequenceAsset CreateEmpty(int seed, bool loop = false)
        {
            var sequence = ScriptableObject.CreateInstance<CommandSequenceAsset>();
            sequence.Seed = seed;
            sequence.Loop = loop;
            return sequence;
        }

        public static CommandSequenceAsset CreateFromCatalogs(
            ISequenceCommandCatalog[] catalogs,
            float beatInterval,
            int seed,
            bool loop = true)
        {
            if (catalogs == null || catalogs.Length == 0)
                return null;

            var sequence = CreateEmpty(seed, loop);
            var random = new System.Random(seed);
            var context = new SequenceContext(null, random, 0f, 0);

            float currentTime = 0f;

            foreach (var catalog in catalogs)
            {
                if (catalog == null)
                    continue;

                var commands = catalog.GetCommands(context);
                if (commands == null)
                    continue;

                foreach (var command in commands)
                {
                    if (command == null)
                        continue;

                    AddBeat(sequence, currentTime, command);
                    currentTime += beatInterval;
                }
            }

            return sequence;
        }

        public static void AddBeat(CommandSequenceAsset sequence, float time, ISequenceCommand command)
        {
            if (sequence == null || command == null)
                return;

            var beat = new SequenceBeat
            {
                Time = time,
                Command = command
            };

            sequence.Beats.Add(beat);
        }

        public static void AddBeats(CommandSequenceAsset sequence, SequenceBeat[] beats)
        {
            if (sequence == null || beats == null)
                return;

            foreach (var beat in beats)
            {
                if (beat.Command != null)
                {
                    sequence.Beats.Add(beat);
                }
            }
        }

        public static void ClearBeats(CommandSequenceAsset sequence)
        {
            if (sequence == null)
                return;

            sequence.Beats.Clear();
        }

        public static void SortBeatsByTime(CommandSequenceAsset sequence)
        {
            if (sequence == null)
                return;

            sequence.Beats.Sort((a, b) => a.Time.CompareTo(b.Time));
        }
    }
}
