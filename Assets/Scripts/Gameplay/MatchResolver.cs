using System.Collections.Generic;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Stateless helper that scans the (grouped) slot list for matches.
    ///
    /// Because SlotController keeps same colors grouped, a simple linear scan for runs is both
    /// correct and O(n). Each run of >= matchSize same-colored units yields one group of exactly
    /// matchSize units (e.g. 4 reds -> one group of 3, keeping 1), matching Block Jam where only a
    /// triple clears. Returning ALL groups at once lets the caller pop multiple matches in parallel.
    /// </summary>
    public static class MatchResolver
    {
        /// <summary>
        /// Returns every match group currently present (each exactly matchSize units), as lists of
        /// unit references so the caller can remove them without index invalidation. Empty if none.
        /// </summary>
        public static List<List<Unit>> FindAllMatches(IReadOnlyList<Unit> units, int matchSize)
        {
            var groups = new List<List<Unit>>();
            if (units == null || units.Count < matchSize) return groups;

            int runStart = 0;
            for (int i = 1; i <= units.Count; i++)
            {
                bool sameAsPrev = i < units.Count && units[i].Color == units[runStart].Color;
                if (!sameAsPrev)
                {
                    int runLen = i - runStart;
                    if (runLen >= matchSize)
                    {
                        var group = new List<Unit>(matchSize);
                        for (int k = runStart; k < runStart + matchSize; k++)
                            group.Add(units[k]);
                        groups.Add(group);
                    }
                    runStart = i;
                }
            }
            return groups;
        }
    }
}