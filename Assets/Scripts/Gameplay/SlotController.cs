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

        private System.Action<Unit> _onUnitPopped; // callback so the board can return units to pool

        // Total slots visually occupied: settled units + units mid-pop. Used for capacity checks
        // (so a freshly tapped jam never lands on a slot still clearing).
        public int Count => _units.Count + _poppingUnits.Count;

        // Fail is judged ONLY on settled units: units mid-pop are leaving the bar, so they must not
        // count toward "bar full, no match" - otherwise a match that just cleared space could still
        // trigger a false game-over while its pop animation is playing.
        public bool IsFull => _units.Count >= _config.SlotCount;

        /// <summary>Adds the per-color counts of settled units (excluding mid-pop) into the dict.
        /// Used by the board's instant capacity check to cancel guaranteed matches.</summary>
        public void AddSettledColorCounts(System.Collections.Generic.Dictionary<UnitColor, int> counts)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                var col = _units[i].Color;
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
        /// Inserts a unit that has just landed (grouped next to its same-colored neighbours) and, if
        /// that completes a match, pops THAT group immediately and independently - it does not wait
        /// for any other group's animation. So if a red trio and a blue trio complete at nearly the
        /// same moment, each pops in place the instant its own third unit arrives, in parallel.
        /// </summary>
        public void Accept(Unit unit, int insertIndexIgnored, MonoBehaviour coroutineHost,
                           System.Action onResolved, System.Action onFullNoMatch)
        {
            _host = coroutineHost;
            _onResolved = onResolved;
            _onFullNoMatch = onFullNoMatch;

            int insertIndex = GetInsertIndexFor(unit.Color);
            insertIndex = Mathf.Clamp(insertIndex, 0, _units.Count);
            _units.Insert(insertIndex, unit);

            // Snap settled units to their grouped slots (quick), then check if THIS color now forms
            // a match and, if so, pop that one group right away on its own coroutine.
            RepositionImmediate();

            var group = MatchResolver.FindGroupOfColor(_units, unit.Color, _config.MatchSize);
            if (group != null)
            {
                foreach (var u in group) { _units.Remove(u); _poppingUnits.Add(u); }
                _activePops++;
                coroutineHost.StartCoroutine(PopGroup(group, OnGroupPopped));
            }
            else
            {
                // No match from this landing - if the bar is now full with no pending pops, lose.
                EvaluateOutcome();
            }
        }

        private MonoBehaviour _host;
        private System.Action _onResolved;
        private System.Action _onFullNoMatch;
        private int _activePops;   // groups currently mid jump+gather+pop

        /// <summary>Called when one group finishes its pop animation. We only compact the bar once
        /// ALL active pops have finished - so groups that are still mid-match pop in their own place
        /// first, and the leftover (non-matching) units slide in to fill gaps only afterwards. This
        /// avoids units that are themselves about to match being dragged sideways prematurely.</summary>
        private void OnGroupPopped()
        {
            _activePops--;
            if (_activePops > 0) return;           // other groups still popping - don't shift yet
            if (_host != null) _host.StartCoroutine(CompactThenEvaluate());
        }

        private IEnumerator CompactThenEvaluate()
        {
            yield return RepositionAll(_config.SlotCompactDuration);

            // Cascade: compacting may have brought same-colored units together into a new match.
            // Pop every such group (in parallel, like the initial matches), then re-evaluate.
            var groups = MatchResolver.FindAllGroups(_units, _config.MatchSize);
            if (groups.Count > 0)
            {
                foreach (var group in groups)
                    foreach (var u in group) { _units.Remove(u); _poppingUnits.Add(u); }
                foreach (var group in groups)
                {
                    _activePops++;
                    if (_host != null) _host.StartCoroutine(PopGroup(group, OnGroupPopped));
                }
                yield break; // OnGroupPopped will compact + evaluate again once these finish
            }

            EvaluateOutcome();
        }

        /// <summary>Win/lose evaluation once nothing is mid-pop: full with no match left = lose.</summary>
        private void EvaluateOutcome()
        {
            if (_activePops > 0) return;            // wait until all groups have popped
            if (IsFull) _onFullNoMatch?.Invoke();
            else _onResolved?.Invoke();
        }

        /// <summary>Instantly place settled units at their grouped slot positions (no tween), used
        /// right before evaluating a fresh landing so positions/indices are consistent.</summary>
        private void RepositionImmediate()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                _units[i].transform.SetParent(transform, worldPositionStays: true);
                _units[i].transform.position = GetSlotWorldPos(i);
            }
        }

        /// <summary>
        /// Plays one matched group's full sequence: the units leap up together, gather in mid-air,
        /// then pop. The units are already removed from _units and added to _poppingUnits by the
        /// caller (so capacity stays correct); here we only animate and, when done, free their
        /// popping slots. Multiple groups run this in parallel for simultaneous matching.
        /// </summary>
        private IEnumerator PopGroup(List<Unit> popping, System.Action onDone)
        {
            // Block Jam feel: the matched trio leaps up, gathers together in mid-air, and pops at
            // the peak (they never come back down). First, everyone jumps straight up and holds.
            int jumping = popping.Count;
            foreach (var u in popping)
                u.StartCoroutine(JumpThen(u, () => jumping--));
            while (jumping > 0) yield return null;

            // Center point, raised to the jump apex so they merge in the air.
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

            onDone?.Invoke();
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
            _poppingUnits.Clear();
            _activePops = 0;
        }
    }
}