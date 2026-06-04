using UnityEngine;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Runtime state of a single board position. This is the live counterpart to the
    /// authored CellData: it knows its world position and which Unit currently sits on it.
    ///
    /// The "blocked" rule from the case ("a unit can only be selected if it has at least one
    /// free side") is evaluated against neighbouring cells - see BlockChecker, which reads
    /// these cells. A Cell itself is intentionally dumb: it just holds position + occupant.
    /// </summary>
    public class Cell
    {
        public readonly int X;
        public readonly int Y;
        public readonly Vector3 WorldPosition;

        /// <summary>The unit currently on this cell, or null if cleared/empty.</summary>
        public Unit Occupant;

        /// <summary>True if this position is part of the board layout at all (not a hole).</summary>
        public bool Exists;

        public bool IsEmpty => Occupant == null;

        public Cell(int x, int y, Vector3 worldPos, bool exists)
        {
            X = x;
            Y = y;
            WorldPosition = worldPos;
            Exists = exists;
        }
    }
}
