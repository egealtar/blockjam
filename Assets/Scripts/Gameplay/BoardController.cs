using UnityEngine;
using BlockJam.Data;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Owns the board grid at runtime: builds it from a LevelData, spawns units from the pool,
    /// enforces the "free side" selection rule, and animates a tapped unit into the slot bar.
    ///
    /// It does NOT own win/lose policy - it reports state to GameController via callbacks
    /// (board cleared, unit sent, etc.). This keeps the board reusable and the rules in one place.
    /// </summary>
    public class BoardController : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private Transform boardRoot;     // grid is centered around this
        [SerializeField] private Transform tilePrefab;    // optional decorative floor tile under each unit

        private GameConfig _config;
        private ColorPalette _palette;
        private UnitPool _unitPool;
        private SlotController _slots;

        private Cell[,] _grid;
        private int _width, _height;
        private int _aliveUnits;

        private System.Action _onBoardCleared;
        private System.Action _onLose;

        public bool InteractionLocked { get; private set; }

        public void Setup(GameConfig config, ColorPalette palette, UnitPool unitPool,
                          SlotController slots, System.Action onBoardCleared, System.Action onLose)
        {
            _config = config;
            _palette = palette;
            _unitPool = unitPool;
            _slots = slots;
            _onBoardCleared = onBoardCleared;
            _onLose = onLose;
        }

        /// <summary>Tears down any current board and builds a fresh one from the level.</summary>
        public void BuildLevel(LevelData level)
        {
            ClearBoard();

            level.EnsureSize();
            _width = level.Width;
            _height = level.Height;
            _grid = new Cell[_width, _height];
            _aliveUnits = 0;
            _reservedSlots = 0;
            InteractionLocked = false;

            // Center the grid on boardRoot.
            float spacing = _config.CellSpacing;
            float offsetX = (_width - 1) * 0.5f * spacing;
            float offsetZ = (_height - 1) * 0.5f * spacing;
            Vector3 origin = boardRoot != null ? boardRoot.position : Vector3.zero;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector3 world = origin + new Vector3(x * spacing - offsetX, 0f, y * spacing - offsetZ);
                    CellData data = level.GetCell(x, y);
                    var cell = new Cell(x, y, world, data.Occupied);
                    _grid[x, y] = cell;

                    if (!data.Occupied) continue;

                    Unit unit = _unitPool.Get();
                    unit.transform.SetParent(boardRoot, worldPositionStays: true);
                    unit.transform.position = world;
                    unit.Init(data.Color, x, y, data.InBox, _palette.Get(data.Color));
                    cell.Occupant = unit;
                    _aliveUnits++;
                }
            }

            RefreshSelectable();
        }

        // Slots reserved by units that are currently flying toward the bar but haven't landed.
        // Counting these prevents overflow when the player taps rapidly (multiple in flight).
        private int _reservedSlots;

        /// <summary>Raycast-driven entry point: called by InputRouter with a tapped unit.</summary>
        public void OnUnitTapped(Unit unit)
        {
            if (InteractionLocked || unit == null) return;     // locked only on win/lose
            if (unit.IsBoxed) { unit.ShakeBlocked(); return; }

            int x = unit.CellX, y = unit.CellY;
            if (!BlockChecker.IsSelectable(_grid, _width, _height, x, y))
            {
                // Blocked (no free side): negative feedback.
                unit.ShakeBlocked();
                return;
            }

            // Capacity check counting both settled units AND units already in flight.
            // If the bar would overflow with no possible match, refuse this tap (shake).
            if (_slots.Count + _reservedSlots >= _config.SlotCount)
            {
                unit.ShakeBlocked();
                return;
            }

            // Remove from the board grid immediately so neighbours become selectable
            // and rapid follow-up taps see the updated board.
            Cell cell = _grid[x, y];
            cell.Occupant = null;
            _aliveUnits--;
            unit.SetCell(-1, -1);
            _reservedSlots++;

            int insertIndex = _slots.GetInsertIndexFor(unit.Color);
            Vector3 slotPos = _slots.GetSlotWorldPos(insertIndex);

            RefreshSelectable();

            unit.JumpToSlot(slotPos, _config.UnitToSlotDuration, 1.2f, () =>
            {
                _reservedSlots = Mathf.Max(0, _reservedSlots - 1);
                _slots.Accept(unit, insertIndex, this,
                    onResolved: () =>
                    {
                        TryBreakAdjacentBoxes();
                        RefreshSelectable();
                        CheckWin();
                    },
                    onFullNoMatch: () =>
                    {
                        InteractionLocked = true;
                        _onLose?.Invoke();
                    });
            });
        }

        /// <summary>Win = every unit cleared from the board AND the slot bar empty of leftovers.</summary>
        private void CheckWin()
        {
            if (_aliveUnits <= 0 && _slots.Count == 0)
                _onBoardCleared?.Invoke();
        }

        /// <summary>
        /// When the board changes, a boxed unit breaks open ONLY if one of its orthogonal
        /// neighbours that EXISTS on the board is now empty (i.e. a real adjacent unit was taken).
        /// Board edges do NOT count, so a boxed unit on the rim doesn't break for free at start.
        /// </summary>
        private void TryBreakAdjacentBoxes()
        {
            if (_grid == null) return;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                Cell c = _grid[x, y];
                if (c == null || c.IsEmpty || !c.Occupant.IsBoxed) continue;
                if (HasEmptyExistingNeighbour(x, y))
                    c.Occupant.BreakBox();
            }
        }

        /// <summary>
        /// True if at least one orthogonal neighbour cell EXISTS on the board (was part of the
        /// layout) but is currently empty - meaning a real adjacent unit has been cleared.
        /// </summary>
        private bool HasEmptyExistingNeighbour(int x, int y)
        {
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                if (nx < 0 || nx >= _width || ny < 0 || ny >= _height) continue; // edge: ignore
                Cell n = _grid[nx, ny];
                if (n == null) continue;
                if (n.Exists && n.IsEmpty) return true; // a real neighbour was cleared
            }
            return false;
        }

        /// <summary>
        /// Keeps colliders enabled on all live units (even blocked ones) so a tap on a blocked
        /// unit can trigger the shake feedback. During global interaction lock (a unit is flying
        /// or the bar is resolving) we disable all colliders so nothing can be tapped.
        /// The actual "is this selectable" decision is made in OnUnitTapped via BlockChecker.
        /// </summary>
        private void RefreshSelectable()
        {
            if (_grid == null) return;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                Cell c = _grid[x, y];
                if (c == null || c.IsEmpty) continue;
                // Tappable unless globally locked or boxed; blocked units stay tappable (for shake).
                bool tappable = !InteractionLocked && !c.Occupant.IsBoxed;
                c.Occupant.SetTappable(tappable);
            }
        }

        public void ClearBoard()
        {
            if (_grid == null) return;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                Cell c = _grid[x, y];
                if (c != null && !c.IsEmpty) _unitPool.Release(c.Occupant);
            }
            _grid = null;
            _aliveUnits = 0;
        }

        /// <summary>Returns a unit to the pool. Wired into SlotController's pop callback.</summary>
        public void ReturnUnitToPool(Unit unit)
        {
            if (unit != null) _unitPool.Release(unit);
        }
    }
}