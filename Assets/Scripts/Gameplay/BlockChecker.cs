namespace BlockJam.Gameplay
{
    /// <summary>
    /// Implements the case's selection rule:
    /// "A unit can only be selected if it is NOT blocked, meaning it has at least one free side."
    ///
    /// "Free side" = one of the 4 orthogonal neighbours (up/down/left/right) is either
    /// off the board, a non-existent hole, or currently empty (its unit was already taken).
    /// A unit fully surrounded on all four sides by other units is blocked and cannot be tapped.
    /// Boxed units are also non-selectable regardless of free sides (handled on the Unit itself).
    ///
    /// Kept as a static, board-agnostic helper so it is trivial to unit-test and has no state.
    /// </summary>
    public static class BlockChecker
    {
        // Orthogonal neighbour offsets only - diagonals do NOT count as a free side,
        // matching the "side" wording (shared edge) in the brief.
        private static readonly int[] DX = { 1, -1, 0, 0 };
        private static readonly int[] DY = { 0, 0, 1, -1 };

        /// <summary>
        /// Returns true if the unit at (x, y) has at least one free orthogonal side.
        /// </summary>
        public static bool HasFreeSide(Cell[,] grid, int width, int height, int x, int y)
        {
            for (int i = 0; i < 4; i++)
            {
                int nx = x + DX[i];
                int ny = y + DY[i];

                // Off the board edge => that side is open.
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) return true;

                Cell n = grid[nx, ny];
                // A hole (non-existent cell) or an empty cell => that side is open.
                if (n == null || !n.Exists || n.IsEmpty) return true;
            }
            return false;
        }

        /// <summary>Convenience: a unit is selectable if it exists, isn't boxed, and has a free side.</summary>
        public static bool IsSelectable(Cell[,] grid, int width, int height, int x, int y)
        {
            Cell c = grid[x, y];
            if (c == null || c.IsEmpty) return false;
            if (c.Occupant.IsBoxed) return false;
            return HasFreeSide(grid, width, height, x, y);
        }
    }
}
