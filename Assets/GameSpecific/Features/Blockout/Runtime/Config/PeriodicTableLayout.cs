namespace hp55games.Blockout.Config
{
    // Periodic Table Shop GDD: an atomic number's real position in the periodic table (IUPAC
    // group 1-18, period 1-7), so the shop grid can reproduce the table's authentic shape - the
    // real gaps (group 2 -> 13 across periods 2-3, the lanthanide/actinide block pulled out of
    // the main grid into its own scrollable sub-row) rather than a compacted grid. Pure data/
    // math, no Unity dependency.
    public readonly struct PeriodicTablePosition
    {
        // 1-18 (IUPAC). For a lanthanide/actinide this is its series' logical group (always 3) -
        // it is never placed at (Group, Period) in the main grid; see IsInExtractedSeries. The
        // main grid instead shows one shared placeholder tile at (Group: 3, Period).
        public readonly int Group;
        public readonly int Period; // 1-7
        public readonly bool IsLanthanide;
        public readonly bool IsActinide;

        public PeriodicTablePosition(int group, int period, bool isLanthanide, bool isActinide)
        {
            Group = group;
            Period = period;
            IsLanthanide = isLanthanide;
            IsActinide = isActinide;
        }

        // True for La-Lu (57-71) or Ac-Lr (89-103): rendered only in the dedicated scrollable
        // lanthanide/actinide sub-view (GDD "Layout"), never as its own tile in the main grid.
        public bool IsInExtractedSeries => IsLanthanide || IsActinide;
    }

    public static class PeriodicTableLayout
    {
        public const int LanthanideSeriesStart = 57;
        public const int LanthanideSeriesEnd = 71;
        public const int ActinideSeriesStart = 89;
        public const int ActinideSeriesEnd = 103;

        public static PeriodicTablePosition GetPosition(int atomicNumber)
        {
            int period = GetPeriod(atomicNumber);
            bool isLanthanide = atomicNumber >= LanthanideSeriesStart && atomicNumber <= LanthanideSeriesEnd;
            bool isActinide = atomicNumber >= ActinideSeriesStart && atomicNumber <= ActinideSeriesEnd;
            int group = GetGroup(atomicNumber, period, isLanthanide, isActinide);
            return new PeriodicTablePosition(group, period, isLanthanide, isActinide);
        }

        public static int GetPeriod(int atomicNumber)
        {
            if (atomicNumber <= 2) return 1;
            if (atomicNumber <= 10) return 2;
            if (atomicNumber <= 18) return 3;
            if (atomicNumber <= 36) return 4;
            if (atomicNumber <= 54) return 5;
            if (atomicNumber <= 86) return 6;
            return 7; // up to 118
        }

        private static int GetGroup(int atomicNumber, int period, bool isLanthanide, bool isActinide)
        {
            if (isLanthanide || isActinide) return 3; // the pulled-out series' shared logical group

            switch (period)
            {
                case 1:
                    return atomicNumber == 1 ? 1 : 18; // H / He

                case 2:
                case 3:
                {
                    int periodStart = period == 2 ? 3 : 11;
                    int pos = atomicNumber - periodStart; // 0-based within the period
                    return pos < 2 ? pos + 1 : pos + 11; // groups 1-2, then the real jump to 13-18
                }

                case 4:
                case 5:
                {
                    int periodStart = period == 4 ? 19 : 37;
                    return atomicNumber - periodStart + 1; // full 1-18 sequence, no gap
                }

                case 6:
                    if (atomicNumber == 55) return 1;
                    if (atomicNumber == 56) return 2;
                    return atomicNumber - 72 + 4; // Hf(72)=4 .. Rn(86)=18 (57-71 already handled above)

                default: // 7
                    if (atomicNumber == 87) return 1;
                    if (atomicNumber == 88) return 2;
                    return atomicNumber - 104 + 4; // Rf(104)=4 .. Og(118)=18 (89-103 already handled above)
            }
        }
    }
}
