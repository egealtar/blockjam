using System.Collections.Generic;
using BlockJam.Data;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Stateless helper for finding matches in the (grouped) slot list.
    ///
    /// SlotController keeps same-colored units adjacent, so a match is simply a run of >= matchSize
    /// consecutive units of one color. We return exactly matchSize units for a run (e.g. 4 reds ->
    /// one group of 3, leaving 1), mirroring Block Jam where only a triple clears at a time.
    /// </summary>
    public static class MatchResolver
    {
        /// <summary>
        /// Returns the first completed group (exactly matchSize units) of the given color, or null
        /// if that color has no full run. Used to pop a group the instant its last unit lands.
        /// </summary>
        public static List<Unit> FindGroupOfColor(IReadOnlyList<Unit> units, UnitColor color, int matchSize)
        {
            int i = 0;
            while (i < units.Count)
            {
                if (units[i].Color != color) { i++; continue; }
                int runStart = i;
                while (i < units.Count && units[i].Color == color) i++;
                if (i - runStart >= matchSize)
                {
                    var g = new List<Unit>(matchSize);
                    for (int k = runStart; k < runStart + matchSize; k++) g.Add(units[k]);
                    return g;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns every completed group currently in the list (one group of matchSize per run of
        /// >= matchSize same-colored units). Used after a compaction to detect cascade matches.
        /// </summary>
        public static List<List<Unit>> FindAllGroups(IReadOnlyList<Unit> units, int matchSize)
        {
            var groups = new List<List<Unit>>();
            int i = 0;
            while (i < units.Count)
            {
                int runStart = i;
                var color = units[i].Color;
                while (i < units.Count && units[i].Color == color) i++;
                if (i - runStart >= matchSize)
                {
                    var g = new List<Unit>(matchSize);
                    for (int k = runStart; k < runStart + matchSize; k++) g.Add(units[k]);
                    groups.Add(g);
                }
            }
            return groups;
        }
    }
}