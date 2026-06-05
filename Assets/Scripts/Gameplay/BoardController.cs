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
        private ObstaclePool _obstaclePool;

        private Cell[,] _grid;
        private int _width, _height;

        /// <summary>The padded grid width (level width + 2 for the left/right frame).</summary>
        public int GridWidth => _width;
        /// <summary>The padded grid height (level height + 1 for the back frame).</summary>
        public int GridHeight => _height;
        private int _aliveUnits;

        private System.Action _onBoardCleared;
        private System.Action _onLose;

        public bool InteractionLocked { get; private set; }

        public void Setup(GameConfig config, ColorPalette palette, UnitPool unitPool,
                          SlotController slots, ObstaclePool obstaclePool,
                          System.Action onBoardCleared, System.Action onLose)
        {
            _config = config;
            _palette = palette;
            _unitPool = unitPool;
            _slots = slots;
            _obstaclePool = obstaclePool;
            _onBoardCleared = onBoardCleared;
            _onLose = onLose;
        }

        /// <summary>Tears down any current board and builds a fresh one from the level.</summary>
        public void BuildLevel(LevelData level)
        {
            ClearBoard();

            level.EnsureSize();
            int levelW = level.Width;
            int levelH = level.Height;

            // The playable grid is padded with a permanent obstacle frame: one extra column on the
            // LEFT and RIGHT, and one extra row at the BACK. The FRONT is never padded (it's the
            // exit). So jams live at gridX = levelX + 1 and gridY = levelY + 1, with x=0 / x=W-1 /
            // y=0 reserved for the frame. This is what makes the board look fully enclosed with
            // only the lanes in front of jams open.
            _width = levelW + 2;
            _height = levelH + 1;
            _grid = new Cell[_width, _height];
            _aliveUnits = 0;
            _reservedSlots = 0;
            InteractionLocked = false;

            // Center the (padded) grid on boardRoot.
            float spacing = _config.CellSpacing;
            float offsetX = (_width - 1) * 0.5f * spacing;
            float offsetZ = (_height - 1) * 0.5f * spacing;
            Vector3 origin = boardRoot != null ? boardRoot.position : Vector3.zero;

            // First create every cell (positions only).
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector3 world = origin + new Vector3(x * spacing - offsetX, 0f, y * spacing - offsetZ);
                    _grid[x, y] = new Cell(x, y, world, true);
                }
            }

            // Spawn jams from the level into the padded interior (shifted by +1 in x and +1 in y).
            for (int ly = 0; ly < levelH; ly++)
            {
                for (int lx = 0; lx < levelW; lx++)
                {
                    CellData data = level.GetCell(lx, ly);
                    if (!data.Occupied) continue;

                    int gx = lx + 1;
                    int gy = ly + 1;
                    Cell cell = _grid[gx, gy];

                    Unit unit = _unitPool.Get();
                    unit.transform.SetParent(boardRoot, worldPositionStays: true);
                    unit.transform.position = cell.WorldPosition;
                    unit.Init(data.Color, gx, gy, data.InBox, _palette.Get(data.Color));
                    cell.Occupant = unit;
                    _aliveUnits++;
                }
            }

            PlaceObstacles(origin, spacing, offsetX, offsetZ);
            RefreshSelectable();
        }

        /// <summary>
        /// Fills every non-jam cell with a permanent obstacle ("wall box"), EXCEPT the open front
        /// corridor: in each column, the cells in front of (higher y than) that column's
        /// front-most jam are left empty so the jams have a lane to walk out toward the exit.
        /// Columns with no jam at all are left fully open (nothing to wall off there).
        /// </summary>
        private void PlaceObstacles(Vector3 origin, float spacing, float offsetX, float offsetZ)
        {
            if (_obstaclePool == null) return;

            // The very front row (highest y) is the exit zone and never gets obstacles, so the
            // left/right frame columns don't stick out past the front of the jam area.
            int frontRow = _height - 1;

            for (int x = 0; x < _width; x++)
            {
                // Find the front-most jam in this column (highest y that holds a unit).
                int frontJamY = -1;
                for (int y = _height - 1; y >= 0; y--)
                {
                    if (_grid[x, y].Occupant != null) { frontJamY = y; break; }
                }

                // Every non-jam cell becomes an obstacle EXCEPT the open exit corridor: the cells
                // in front of (higher y than) the column's front-most jam, and the front row.
                for (int y = 0; y < _height; y++)
                {
                    if (y == frontRow) continue;             // keep the exit row fully open
                    Cell cell = _grid[x, y];
                    if (cell.Occupant != null) continue;     // a jam sits here
                    if (frontJamY >= 0 && y > frontJamY) continue; // exit corridor in front of a jam

                    cell.IsObstacle = true;
                    var view = _obstaclePool.Get();
                    view.transform.position = cell.WorldPosition;
                    view.transform.rotation = Quaternion.identity;
                    // Size each obstacle to fill its whole cell so neighbours sit flush (no gaps),
                    // measuring the prefab's actual rendered size so any mesh scale works.
                    FitObstacleToCell(view, spacing);
                    cell.ObstacleView = view;
                }
            }
        }

        /// <summary>
        /// Scales an obstacle so its rendered footprint exactly fills one cell (spacing x spacing
        /// on the X/Z plane), regardless of the prefab's mesh size or authored scale, so obstacles
        /// sit flush against each other with no visible gaps. Height is left proportional.
        /// </summary>
        private void FitObstacleToCell(PooledObstacle view, float spacing)
        {
            var rend = view.GetComponentInChildren<Renderer>();
            if (rend == null) return;

            Vector3 size = rend.bounds.size; // current world-space size at current scale
            if (size.x <= 0.0001f || size.z <= 0.0001f) return;

            float sx = spacing / size.x;
            float sz = spacing / size.z;
            Vector3 ls = view.transform.localScale;
            view.transform.localScale = new Vector3(ls.x * sx, ls.y, ls.z * sz);
        }

        /// <summary>
        /// Converts a grid coordinate to its world position, using the same centering math as
        /// BuildLevel. Accepts y == height (the virtual exit cell just past the front edge).
        /// </summary>
        public Vector3 CellToWorld(int x, int y)
        {
            float spacing = _config.CellSpacing;
            float offsetX = (_width - 1) * 0.5f * spacing;
            float offsetZ = (_height - 1) * 0.5f * spacing;
            Vector3 origin = boardRoot != null ? boardRoot.position : Vector3.zero;
            return origin + new Vector3(x * spacing - offsetX, 0f, y * spacing - offsetZ);
        }

        // Slots reserved by units that are currently flying toward the bar but haven't landed.
        // Counting these prevents overflow when the player taps rapidly (multiple in flight).
        private int _reservedSlots;

        /// <summary>Raycast-driven entry point: called by InputRouter with a tapped unit.</summary>
        public void OnUnitTapped(Unit unit)
        {
            if (unit == null || InteractionLocked) return;
            if (unit.IsBoxed) { unit.ShakeBlocked(); return; }

            int x = unit.CellX, y = unit.CellY;

            // Find a walk-out path to the open front edge (Block Jam rule). If none, it's sealed in.
            var pathCells = BlockChecker.FindPathToExit(_grid, _width, _height, x, y);
            if (pathCells == null)
            {
                unit.ShakeBlocked();
                return;
            }

            // Capacity check counting both settled units AND units already in flight.
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

            // Convert the path cells to world positions for the unit to walk along.
            var pathWorld = new System.Collections.Generic.List<Vector3>(pathCells.Count);
            foreach (var pc in pathCells)
                pathWorld.Add(CellToWorld(pc.x, pc.y));

            unit.WalkPathThenSlot(pathWorld, slotPos, _config.UnitToSlotDuration * 0.5f,
                                  _config.UnitToSlotDuration, () =>
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
                if (c == null || c.Occupant == null || !c.Occupant.IsBoxed) continue;
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
                if (c == null || c.Occupant == null) continue; // skip empties and obstacle cells
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
                if (c == null) continue;
                if (c.Occupant != null) _unitPool.Release(c.Occupant);
                if (c.ObstacleView != null && _obstaclePool != null)
                {
                    _obstaclePool.Release(c.ObstacleView);
                    c.ObstacleView = null;
                }
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