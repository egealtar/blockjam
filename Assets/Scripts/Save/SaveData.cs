using System;

namespace BlockJam.Save
{
    /// <summary>
    /// Plain serializable container for persisted progress.
    /// Deliberately tiny: the case only requires that level progress survives a restart.
    /// Versioned so the format can evolve without breaking old saves.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int Version = 1;

        /// <summary>Index (0-based) of the level the player should resume on.</summary>
        public int CurrentLevelIndex = 0;

        /// <summary>Highest level index the player has reached (for future menus / safety).</summary>
        public int HighestLevelReached = 0;
    }
}
