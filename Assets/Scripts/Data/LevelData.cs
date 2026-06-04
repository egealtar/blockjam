using UnityEngine;

namespace BlockJam.Data
{
    /// <summary>
    /// A single playable level, stored as a ScriptableObject asset on disk.
    /// The grid is stored as a FLAT array (width * height) instead of a 2D array because
    /// Unity's serializer does not serialize multidimensional/jagged arrays reliably.
    /// Helper accessors below convert (x, y) <-> flat index so callers can think in 2D.
    ///
    /// IMPORTANT design rule for this case:
    /// A level is only solvable if, for every color used, the total count is a multiple of 3.
    /// The level editor enforces/validates this so we never ship an impossible level.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "BlockJam/Level Data", order = 0)]
    public class LevelData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("1-based level number shown on the HUD and used for ordering / save progress.")]
        [Min(1)] public int LevelNumber = 1;

        [Header("Grid Size")]
        [Min(1)] public int Width = 5;
        [Min(1)] public int Height = 5;

        [Header("Cells (flattened, row-major: index = y * Width + x)")]
        [SerializeField] private CellData[] cells = new CellData[25];

        /// <summary>Total number of cells (Width * Height).</summary>
        public int CellCount => Width * Height;

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public int ToIndex(int x, int y) => y * Width + x;

        public CellData GetCell(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y)) return CellData.Empty;
            return cells[ToIndex(x, y)];
        }

        public void SetCell(int x, int y, CellData data)
        {
            EnsureSize();
            if (!InBounds(x, y)) return;
            cells[ToIndex(x, y)] = data;
        }

        /// <summary>
        /// Resizes the backing array to match Width*Height, preserving existing cells where possible.
        /// Called by the editor whenever the dimensions change, and defensively on access.
        /// </summary>
        public void EnsureSize()
        {
            int required = Mathf.Max(1, Width * Height);
            if (cells != null && cells.Length == required) return;

            var resized = new CellData[required];
            if (cells != null)
            {
                int copy = Mathf.Min(cells.Length, required);
                for (int i = 0; i < copy; i++) resized[i] = cells[i];
            }
            cells = resized;
        }

        /// <summary>Counts occupied (non-empty) cells - i.e. how many units this level spawns.</summary>
        public int CountUnits()
        {
            EnsureSize();
            int n = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].Occupied) n++;
            return n;
        }

        /// <summary>
        /// Validation used by the editor: every color's count must be divisible by 3,
        /// otherwise the board can never be fully cleared. Returns true if solvable-by-counts.
        /// </summary>
        public bool ValidateColorCounts(out string error)
        {
            EnsureSize();
            var counts = new int[System.Enum.GetValues(typeof(UnitColor)).Length];
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].Occupied) continue;
                counts[(int)cells[i].Color]++;
            }

            for (int c = 0; c < counts.Length; c++)
            {
                if (counts[c] % 3 != 0)
                {
                    error = $"Color {(UnitColor)c} has {counts[c]} units, which is not a multiple of 3. " +
                            "This level cannot be fully cleared.";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureSize();
        }
#endif
    }
}
