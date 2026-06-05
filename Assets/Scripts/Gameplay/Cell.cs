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

        /// <summary>
        /// True if a permanent obstacle ("wall box") occupies this cell. Unlike the breakable
        /// box that wraps a unit, an obstacle never disappears: it is auto-placed on every cell
        /// that has no jam and is not part of the open front corridor, so it both visually shows
        /// the closed area (like the grass border in Block Jam) and acts as a hard wall that
        /// pathfinding must route around. A cell with an obstacle can never hold a unit.
        /// </summary>
        public bool IsObstacle;

        /// <summary>The pooled visual representing the obstacle on this cell, if any.</summary>
        public PooledObstacle ObstacleView;

        /// <summary>Empty for movement purposes: no unit AND no permanent obstacle.</summary>
        public bool IsEmpty => Occupant == null && !IsObstacle;

        /// <summary>A unit cannot move into or through this cell (it holds a unit or an obstacle).</summary>
        public bool IsBlocking => Occupant != null || IsObstacle;

        public Cell(int x, int y, Vector3 worldPos, bool exists)
        {
            X = x;
            Y = y;
            WorldPosition = worldPos;
            Exists = exists;
        }
    }
}