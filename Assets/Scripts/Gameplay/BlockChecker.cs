using System.Collections.Generic;
using UnityEngine;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Block Jam-style selection rule. The board is walled on the back and both sides; only the
    /// FRONT edge is open (toward the camera / the player). A unit can be selected only if there
    /// is a path of empty cells from it to the front edge, moving left / right / forward (never
    /// backward, since the back is a wall). If such a path exists the unit "walks out" along it;
    /// if it is sealed in by other units it is blocked and cannot be taken.
    ///
    /// Grid convention (matches BoardController): y maps to world Z, and the FRONT edge is the
    /// highest y (y == height-1), because the camera looks from the +Z side. A unit reaches the
    /// exit when it can step past y == height-1.
    ///
    /// Stateless and pure so it is easy to reason about and test.
    /// </summary>
    public static class BlockChecker
    {
        // Allowed step directions: +Y (forward, toward the open front), +X and -X (sideways).
        // -Y (backward) is NOT allowed because the back is a wall.
        private static readonly int[] DX = { 0, 1, -1 };
        private static readonly int[] DY = { 1, 0, 0 };

        /// <summary>
        /// True if the unit at (x,y) can reach the open front edge through empty cells.
        /// The unit's own cell is treated as the start (it can move once neighbours are clear).
        /// </summary>
        public static bool CanReachExit(Cell[,] grid, int width, int height, int x, int y)
        {
            return FindPathToExit(grid, width, height, x, y) != null;
        }

        /// <summary>
        /// BFS from (x,y) to the front edge over EMPTY cells (the unit can move into a cell only
        /// if it's empty). Returns the list of grid coordinates to walk through (excluding the
        /// start, including a final virtual exit cell at y == height), or null if sealed in.
        /// </summary>
        public static List<Vector2Int> FindPathToExit(Cell[,] grid, int width, int height, int x, int y)
        {
            // If already on the front row, it can step straight out.
            // Otherwise we search forward/sideways through empty neighbours.
            var visited = new bool[width, height];
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();

            var start = new Vector2Int(x, y);
            queue.Enqueue(start);
            visited[x, y] = true;

            Vector2Int exitFrom = new Vector2Int(-1, -1);
            bool found = false;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();

                // Can we step OUT from here? Only from the front row (y == height-1) into the open edge.
                if (cur.y == height - 1)
                {
                    exitFrom = cur;
                    found = true;
                    break;
                }

                for (int i = 0; i < 3; i++)
                {
                    int nx = cur.x + DX[i];
                    int ny = cur.y + DY[i];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (visited[nx, ny]) continue;

                    Cell n = grid[nx, ny];
                    // A cell is walkable only if it is part of the board and currently empty,
                    // i.e. it holds neither a jam (unit) nor a permanent obstacle ("wall box").
                    // The whole board is filled with jams or obstacles except the open front
                    // corridor, so pathfinding naturally routes a unit out through that corridor
                    // and any space opened up as neighbouring jams are taken.
                    bool passable = (n != null) && n.Exists && n.IsEmpty;
                    if (!passable) continue;

                    visited[nx, ny] = true;
                    prev[new Vector2Int(nx, ny)] = cur;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            if (!found) return null;

            // Reconstruct path from start -> exitFrom, then append the virtual exit cell (y = height).
            var path = new List<Vector2Int>();
            var node = exitFrom;
            while (node != start)
            {
                path.Add(node);
                node = prev[node];
            }
            path.Reverse();
            // Virtual exit point just past the front edge (where the unit leaves the board).
            path.Add(new Vector2Int(exitFrom.x, height));
            return path;
        }

        /// <summary>A unit is selectable if it exists, isn't boxed, and can reach the open front edge.</summary>
        public static bool IsSelectable(Cell[,] grid, int width, int height, int x, int y)
        {
            Cell c = grid[x, y];
            if (c == null || c.IsEmpty) return false;
            if (c.Occupant.IsBoxed) return false;
            return CanReachExit(grid, width, height, x, y);
        }

        /// <summary>
        /// Legacy helper kept for the box-break check: true if an orthogonal neighbour cell that
        /// exists on the board is currently empty. (Box breaking still uses adjacency, not pathing.)
        /// </summary>
        public static bool HasFreeSide(Cell[,] grid, int width, int height, int x, int y)
        {
            int[] ndx = { 1, -1, 0, 0 };
            int[] ndy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + ndx[i];
                int ny = y + ndy[i];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) return true;
                Cell n = grid[nx, ny];
                if (n == null || !n.Exists || n.IsEmpty) return true;
            }
            return false;
        }
    }
}