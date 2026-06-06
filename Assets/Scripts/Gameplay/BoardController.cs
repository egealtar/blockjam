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
        private VfxPool _vfxPool;

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
                          SlotController slots, ObstaclePool obstaclePool, VfxPool vfxPool,
                          System.Action onBoardCleared, System.Action onLose)
        {
            _config = config;
            _palette = palette;
            _unitPool = unitPool;
            _slots = slots;
            _obstaclePool = obstaclePool;
            _vfxPool = vfxPool;
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
            _flyingColors.Clear();
            InteractionLocked = false;

            // Center the (padded) grid on boardRoot.
            float spacing = _config.CellSpacing;
            float offsetX = (_width - 1) * 0.5f * spacing;
            float offsetZ = (_height - 1) * 0.5f * spacing;
            Vector3 origin = boardRoot != null ? boardRoot.position : Vector3.zero;

            // First create every cell (positions only). The floor is a single large ground plane
            // placed in the scene (covers the whole board + screen), so no per-cell tiles here.
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

        // Colors of units currently flying toward the bar but not yet landed. Tracking the COLOR
        // (not just a count) lets us predict matches: if the bar would reach 3 of a color once the
        // in-flight ones land, those 3 will pop and free space - so we can keep sending jams now
        // instead of waiting for the physical pop. This is the "count slots as if matches already
        // happened" rule that keeps rapid play flowing.
        private readonly System.Collections.Generic.List<UnitColor> _flyingColors = new System.Collections.Generic.List<UnitColor>();

        /// <summary>
        /// Effective bar fill = settled units + in-flight units, MINUS the units that are
        /// guaranteed to pop (every group of MatchSize same-color units cancels out). If this is
        /// below SlotCount, there's room to send another jam right now.
        /// </summary>
        private int EffectiveSlotFill(UnitColor incoming)
        {
            var counts = new System.Collections.Generic.Dictionary<UnitColor, int>();
            _slots.AddColorCounts(counts);                       // settled in the bar
            for (int i = 0; i < _flyingColors.Count; i++)        // already in flight
            {
                counts.TryGetValue(_flyingColors[i], out int n);
                counts[_flyingColors[i]] = n + 1;
            }
            counts.TryGetValue(incoming, out int inc);           // the jam we're about to send
            counts[incoming] = inc + 1;

            int total = 0;
            foreach (var kv in counts)
            {
                // Whatever doesn't form a complete match-of-3 stays in the bar.
                total += kv.Value % _config.MatchSize;
            }
            return total;
        }

        /// <summary>Raycast-driven entry point: called by InputRouter with a tapped unit.</summary>
        public void OnUnitTapped(Unit unit)
        {
            if (unit == null || InteractionLocked) return;
            if (!unit.IsTappable) return;              // currently flying to a slot
            if (unit.IsBoxed) { unit.ShakeBlocked(); return; }

            int x = unit.CellX, y = unit.CellY;

            // Find a walk-out path to the open front edge (Block Jam rule). If none, it's sealed in.
            var pathCells = BlockChecker.FindPathToExit(_grid, _width, _height, x, y);
            if (pathCells == null)
            {
                unit.ShakeBlocked();
                return;
            }

            // Capacity check using EFFECTIVE fill: counts settled + in-flight units but cancels out
            // any group that will complete a match (and therefore pop). So if sending this jam only
            // pushes the bar over SlotCount with units that are about to disappear, we still allow
            // it - keeping play fluid - and only block when the bar would truly overflow.
            if (EffectiveSlotFill(unit.Color) > _config.SlotCount)
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
            _flyingColors.Add(unit.Color);

            // Break any adjacent boxes RIGHT NOW (the instant the jam leaves its cell), so a boxed
            // neighbour opens immediately and becomes tappable - not after the jam lands in a slot.
            TryBreakAdjacentBoxes();

            int insertIndex = _slots.GetInsertIndexFor(unit.Color);
            Vector3 slotPos = _slots.GetSlotWorldPos(insertIndex);

            RefreshSelectable();

            // Convert the path cells to world positions for the unit to walk along.
            var pathWorld = new System.Collections.Generic.List<Vector3>(pathCells.Count);
            foreach (var pc in pathCells)
                pathWorld.Add(CellToWorld(pc.x, pc.y));

            UnitColor flownColor = unit.Color;
            // Spawn a dust puff under the unit at each footstep while it runs (movement juice).
            unit.OnFootstep = footPos =>
            {
                if (_vfxPool != null) _vfxPool.PlayDust(footPos, unit.DisplayColor);
            };
            unit.WalkPathThenSlot(pathWorld, slotPos, _config.UnitToSlotDuration * 0.5f,
                                  _config.UnitToSlotDuration, () =>
            {
                unit.OnFootstep = null;
                _flyingColors.Remove(flownColor);
                _slots.Accept(unit, insertIndex, this,
                    onResolved: () =>
                    {
                        RefreshSelectable();
                        CheckWin();
                    },
                    onFullNoMatch: () =>
                    {
                        // The bar is physically full with no match. But if jams are still flying in,
                        // one of them may complete a match and free space - so don't lose yet. The
                        // last jam to land re-runs this check with no flyers left, giving the true
                        // game-over. This keeps the lose condition consistent with the effective-fill
                        // rule used when sending jams (no "fail screen + empty slots" contradiction).
                        if (_flyingColors.Count > 0) return;
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
                {
                    // Play a dedicated break burst at the box's position for satisfying feedback.
                    if (_vfxPool != null) _vfxPool.PlayBoxBreak(c.Occupant.transform.position);
                    c.Occupant.BreakBox();
                }
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
        /// No-op for tap-gating now: tappability is driven by flight state (set when a unit starts
        /// moving) and the real "can this move?" decision is made live in OnUnitTapped via
        /// pathfinding. Kept as a hook in case future visual state (e.g. dimming blocked jams)
        /// is desired. Re-running pathfinding for every cell on every change is unnecessary.
        /// </summary>
        private void RefreshSelectable()
        {
            // Intentionally empty: see summary. Collider stays enabled; pathfinding gates taps.
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