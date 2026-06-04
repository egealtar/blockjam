using System;
using UnityEngine;

namespace BlockJam.Data
{
    /// <summary>
    /// Pure data description of a single cell in a level grid.
    /// This is what the level editor authors and what gets serialized inside <see cref="LevelData"/>.
    /// It holds NO scene references and NO behaviour - it is just the "recipe" for a cell.
    /// </summary>
    [Serializable]
    public struct CellData
    {
        [Tooltip("If false, this grid position is empty (a hole in the board) and no unit spawns here.")]
        public bool Occupied;

        [Tooltip("Color/type of the unit that spawns on this cell.")]
        public UnitColor Color;

        [Tooltip("If true the unit starts hidden inside a Box blocker and cannot be tapped " +
                 "until the box is broken (the nice-to-have mechanic).")]
        public bool InBox;

        public static CellData Empty => new CellData { Occupied = false };

        public static CellData Of(UnitColor color, bool inBox = false)
        {
            return new CellData { Occupied = true, Color = color, InBox = inBox };
        }
    }
}
