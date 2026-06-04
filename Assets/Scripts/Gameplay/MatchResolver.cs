using System.Collections.Generic;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Stateless helper that scans the (grouped) slot list for the first run of
    /// <paramref name="matchSize"/> or more consecutive same-colored units.
    ///
    /// Because SlotController keeps same colors grouped, a simple linear scan for a run is
    /// both correct and O(n). We pop exactly matchSize at a time (not the whole run) so that,
    /// e.g., 4 reds become "pop 3, keep 1" - matching Block Jam behaviour where only a triple clears.
    /// </summary>
    public static class MatchResolver
    {
        public static bool TryFindMatch(IReadOnlyList<Unit> units, int matchSize,
                                        out int startIndex, out int length)
        {
            startIndex = -1;
            length = 0;
            if (units == null || units.Count < matchSize) return false;

            int runStart = 0;
            for (int i = 1; i <= units.Count; i++)
            {
                bool sameAsPrev = i < units.Count && units[i].Color == units[runStart].Color;
                if (!sameAsPrev)
                {
                    int runLen = i - runStart;
                    if (runLen >= matchSize)
                    {
                        startIndex = runStart;
                        length = matchSize; // pop exactly one match group
                        return true;
                    }
                    runStart = i;
                }
            }
            return false;
        }
    }
}
