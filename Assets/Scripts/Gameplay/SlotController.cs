using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BlockJam.Data;
using BlockJam.Util;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// The bottom slot bar. Fixed capacity (7 per the case).
    ///
    /// Placement rule (faithful to Block Jam 3D): incoming units are inserted so that
    /// same-colored units sit together (grouped), rather than appended in arrival order.
    /// This makes the "3 of the same color auto-match" satisfying and keeps the bar tidy.
    /// As soon as any color reaches MatchSize (3) consecutively, that group pops and the
    /// remaining units compact left.
    ///
    /// Lose condition: a unit arrives while the bar is already full AND no match results
    /// from it. We detect this in BoardController by checking IsFull before sending a unit,
    /// and here by re-checking after insert+match resolution.
    /// </summary>
    public class SlotController : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Left-most slot anchor. Slots extend to the right by SlotSpacing.")]
        [SerializeField] private Transform slotOrigin;

        [Header("Slot Visuals")]
        [Tooltip("Optional visual placed under each of the fixed slots, so the player can see how " +
                 "many slots there are and how full the bar is. One is spawned per slot at startup.")]
        [SerializeField] private GameObject slotCellPrefab;

        private readonly List<GameObject> _slotCells = new List<GameObject>();

        private GameConfig _config;
        private VfxPool _vfx;

        // Ordered list of units currently in the bar (left to right).
        private readonly List<Unit> _units = new List<Unit>();

        // Units that have matched and are mid jump+pop animation. They no longer participate in
        // matching or layout, but still count toward capacity so incoming units don't fill the
        // slots they're vacating until the animation finishes.
        private readonly List<Unit> _poppingUnits = new List<Unit>();

        private bool _busy;            // true while an animation sequence is resolving
        private System.Action<Unit> _onUnitPopped; // callback so the board can return units to pool

        public int Count => _units.Count + _poppingUnits.Count;
        public bool IsFull => Count >= _config.SlotCount;
        public bool IsBusy => _busy;

        /// <summary>Adds the count of each color currently settled in the bar into the given dict.
        /// Units mid-pop are included so capacity checks treat their slots as still occupied.</summary>
        public void AddColorCounts(System.Collections.Generic.Dictionary<UnitColor, int> counts)
        {
            AddColorsFrom(_units, counts);
            AddColorsFrom(_poppingUnits, counts);
        }

        private static void AddColorsFrom(List<Unit> list, System.Collections.Generic.Dictionary<UnitColor, int> counts)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var col = list[i].Color;
                counts.TryGetValue(col, out int n);
                counts[col] = n + 1;
            }
        }

        public void Setup(GameConfig config, VfxPool vfx, System.Action<Unit> onUnitPopped)
        {
            _config = config;
            _vfx = vfx;
            _onUnitPopped = onUnitPopped;
        }

        /// <summary>
        /// Positions the whole slot bar in front of the board, based on the current level's
        /// depth, so it is always ahead of every unit regardless of board size (responsive).
        /// Called by GameController after each level is built.
        /// </summary>
        public void PositionForBoard(int boardHeight, float frontGap)
        {
            // Board is centered on origin and extends +/- (height/2 * cellSpacing) along Z.
            // The camera looks from the +Z side, so "front" (nearest the player) is +Z.
            float halfDepth = (boardHeight * 0.5f) * _config.CellSpacing;
            float z = halfDepth + frontGap; // +Z = toward the camera / front
            transform.position = new Vector3(0f, transform.position.y, z);

            BuildSlotCells();
        }

        /// <summary>
        /// Spawns one decorative cell under each of the fixed slots (once), so the 7 slots are
        /// visible to the player. Positions are taken from GetSlotWorldPos so they always line up
        /// with where units actually land, at any board size.
        /// </summary>
        private void BuildSlotCells()
        {
            if (slotCellPrefab == null) return;

            // Create on first call; afterwards just reposition (board size may change per level).
            while (_slotCells.Count < _config.SlotCount)
            {
                var cell = Instantiate(slotCellPrefab, transform);
                _slotCells.Add(cell);
            }
            for (int i = 0; i < _slotCells.Count; i++)
            {
                bool used = i < _config.SlotCount;
                _slotCells[i].SetActive(used);
                if (used) _slotCells[i].transform.position = GetSlotWorldPos(i);
            }
        }

        /// <summary>
        /// World position of slot index i, horizontally centered so the bar of SlotCount slots
        /// is symmetric around x = 0.
        /// </summary>
        public Vector3 GetSlotWorldPos(int index)
        {
            float spacing = _config.SlotSpacing;
            float totalWidth = (_config.SlotCount - 1) * spacing;
            float startX = -totalWidth * 0.5f;
            float y = slotOrigin != null ? slotOrigin.position.y : transform.position.y;
            float z = transform.position.z;
            return new Vector3(startX + spacing * index, y, z);
        }

        /// <summary>
        /// Computes the slot index a freshly tapped unit of the given color should fly to,
        /// using grouped insertion: it goes immediately after the last unit of the same color,
        /// or at the end if none exist. Does NOT mutate state - BoardController calls this to
        /// know where to animate the unit, then calls Accept() when it lands.
        /// </summary>
        public int GetInsertIndexFor(UnitColor color)
        {
            int lastSame = -1;
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Color == color) lastSame = i;

            return lastSame >= 0 ? lastSame + 1 : _units.Count;
        }

        /// <summary>
        /// Inserts a unit that has just landed, computing its grouped position AT LANDING TIME
        /// (right next to the last unit of the same color). Computing it here - rather than when
        /// the unit was tapped - is what keeps colors correctly grouped when several units fly in
        /// concurrently during rapid tapping; each one slots in against the bar's current state.
        /// </summary>
        public void Accept(Unit unit, int insertIndexIgnored, MonoBehaviour coroutineHost,
                           System.Action onResolved, System.Action onFullNoMatch)
        {
            int insertIndex = GetInsertIndexFor(unit.Color);
            insertIndex = Mathf.Clamp(insertIndex, 0, _units.Count);
            _units.Insert(insertIndex, unit);
            coroutineHost.StartCoroutine(ResolveRoutine(onResolved, onFullNoMatch));
        }

        private IEnumerator ResolveRoutine(System.Action onResolved, System.Action onFullNoMatch)
        {
            _busy = true;

            // Snap everyone to their grouped positions first.
            yield return RepositionAll(_config.SlotCompactDuration);

            // Resolve all matches that may cascade.
            bool matchedAny = true;
            while (matchedAny)
            {
                matchedAny = false;

                if (MatchResolver.TryFindMatch(_units, _config.MatchSize, out int start, out int length))
                {
                    yield return new WaitForSeconds(_config.MatchPopDelay);
                    yield return PopRange(start, length);
                    yield return RepositionAll(_config.SlotCompactDuration);
                    matchedAny = true;
                }
            }

            _busy = false;

            // Lose check: bar full and nothing matched away.
            if (IsFull) onFullNoMatch?.Invoke();
            else onResolved?.Invoke();
        }

        private IEnumerator PopRange(int start, int length)
        {
            var popping = new List<Unit>();
            for (int i = start; i < start + length; i++) popping.Add(_units[i]);

            // Remove from the logical match list up front so match-finding and repositioning are
            // correct, BUT track them in _poppingUnits so capacity still counts them while their
            // jump+pop plays. This keeps rapid tapping responsive (we never block input) while
            // preventing a freshly tapped unit from landing on a slot that is visually still
            // occupied by a popping unit.
            _units.RemoveRange(start, length);
            _poppingUnits.AddRange(popping);

            // Block Jam feel: the matched trio hops together first, then pops.
            int jumping = popping.Count;
            foreach (var u in popping)
                u.StartCoroutine(JumpThen(u, () => jumping--));
            while (jumping > 0) yield return null;

            // Gather to the center of the trio, so the three merge into one spot before popping
            // (instead of vanishing in place) - a more satisfying, Block-Jam-like match.
            Vector3 center = Vector3.zero;
            foreach (var u in popping) center += u.transform.position;
            center /= popping.Count;

            int gathering = popping.Count;
            foreach (var u in popping)
                u.SlideTo(center, _config.MatchGatherDuration, () => gathering--);
            while (gathering > 0) yield return null;

            int remaining = popping.Count;
            foreach (var u in popping)
            {
                if (_vfx != null) _vfx.PlayMatch(u.transform.position);
                u.PlayPop(_config.MatchPopDuration, () =>
                {
                    _onUnitPopped?.Invoke(u); // board returns it to the pool
                    remaining--;
                });
            }

            // Wait until all popped units finished their pop animation, then free their capacity.
            while (remaining > 0) yield return null;
            foreach (var u in popping) _poppingUnits.Remove(u);
        }

        private IEnumerator JumpThen(Unit u, System.Action onDone)
        {
            yield return u.MatchJump(_config.MatchJumpHeight, _config.MatchJumpDuration);
            onDone?.Invoke();
        }

        private IEnumerator RepositionAll(float duration)
        {
            int moving = _units.Count;
            if (moving == 0) yield break;

            for (int i = 0; i < _units.Count; i++)
            {
                Vector3 target = GetSlotWorldPos(i);
                _units[i].transform.SetParent(transform, worldPositionStays: true);
                _units[i].SlideTo(target, duration, () => moving--);
            }

            while (moving > 0) yield return null;
        }

        /// <summary>Clears the bar instantly (level reload). Units are handed back via onUnitPopped.</summary>
        public void ClearImmediate()
        {
            foreach (var u in _units) _onUnitPopped?.Invoke(u);
            _units.Clear();
            _busy = false;
        }
    }
}