using UnityEngine;

namespace BlockJam.Data
{
    /// <summary>
    /// Game-wide tunables and the ordered list of levels.
    /// Keeping this as a ScriptableObject (instead of hard-coded constants) means designers
    /// can tweak feel/timing without touching code, and it keeps magic numbers out of logic.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "BlockJam/Game Config", order = 1)]
    public class GameConfig : ScriptableObject
    {
        [Header("Levels (played in order; loops back to first after the last)")]
        public LevelData[] Levels;

        [Header("Slot Bar")]
        [Tooltip("Fixed number of slots. The case specifies 7.")]
        [Min(1)] public int SlotCount = 7;

        [Tooltip("How many of the same color must gather to auto-match.")]
        [Min(2)] public int MatchSize = 3;

        [Header("Board Layout (world units)")]
        [Tooltip("Distance between cell centers on the board grid.")]
        public float CellSpacing = 1.1f;

        [Tooltip("World distance between slot centers in the bottom bar.")]
        public float SlotSpacing = 1.0f;

        [Header("Animation Timing (seconds)")]
        [Tooltip("Time for a tapped unit to fly from the board into its slot.")]
        public float UnitToSlotDuration = 0.28f;

        [Tooltip("Time for slot units to slide/compact after a match clears.")]
        public float SlotCompactDuration = 0.15f;

        [Tooltip("Delay before a completed match pops, so the player sees the third unit land.")]
        public float MatchPopDelay = 0.06f;

        [Tooltip("Duration of the match pop/scale-out before units return to the pool.")]
        public float MatchPopDuration = 0.20f;

        [Header("Pooling")]
        [Tooltip("How many units to pre-warm in the pool at startup.")]
        [Min(0)] public int UnitPrewarm = 32;

        [Tooltip("How many of each VFX to pre-warm.")]
        [Min(0)] public int VfxPrewarm = 8;

        public int ClampLevelIndex(int index)
        {
            if (Levels == null || Levels.Length == 0) return 0;
            // Loop back to start after the final level.
            return ((index % Levels.Length) + Levels.Length) % Levels.Length;
        }

        public LevelData GetLevel(int index)
        {
            if (Levels == null || Levels.Length == 0) return null;
            return Levels[ClampLevelIndex(index)];
        }
    }
}
