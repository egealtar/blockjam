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

        /// <summary>Raycast-driven entry point: called by InputRouter with a tapped unit.</summary>
        public void OnUnitTapped(Unit unit)
        {
            if (InteractionLocked || unit == null) return;
            if (_slots.IsBusy) return;            // ignore taps while the bar is resolving
            if (unit.IsBoxed) return;

            int x = unit.CellX, y = unit.CellY;
            if (!BlockChecker.IsSelectable(_grid, _width, _height, x, y))
            {
                // Blocked (no free side): give negative feedback so the player understands.
                unit.ShakeBlocked();
                return;
            }

            // Pre-flight lose check: if the bar is already full, a new unit can only be
            // accepted if it will immediately complete a match. We let SlotController decide,
            // but we must still allow the unit to fly in to keep the rule honest.
            // Remove from the board grid now so neighbours become selectable.
            Cell cell = _grid[x, y];
            cell.Occupant = null;
            _aliveUnits--;
            unit.SetCell(-1, -1);

            int insertIndex = _slots.GetInsertIndexFor(unit.Color);
            Vector3 slotPos = _slots.GetSlotWorldPos(insertIndex);

            // Lock interaction during the throw to avoid double-taps racing the resolver.
            InteractionLocked = true;
            RefreshSelectable();

            unit.JumpToSlot(slotPos, _config.UnitToSlotDuration, 1.2f, () =>
            {
                _slots.Accept(unit, insertIndex, this,
                    onResolved: () =>
                    {
                        InteractionLocked = false;
                        // Breaking boxes when their neighbours clear can be handled here if desired.
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
        /// Optional polish: when the board changes, any boxed unit that now has a free side
        /// reveals (breaks). This makes boxes feel reactive rather than static.
        /// </summary>
        private void TryBreakAdjacentBoxes()
        {
            if (_grid == null) return;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                Cell c = _grid[x, y];
                if (c == null || c.IsEmpty) continue;
                if (c.Occupant.IsBoxed && BlockChecker.HasFreeSide(_grid, _width, _height, x, y))
                    c.Occupant.BreakBox();
            }
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