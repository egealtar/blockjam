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

        private GameConfig _config;
        private VfxPool _vfx;

        // Ordered list of units currently in the bar (left to right).
        private readonly List<Unit> _units = new List<Unit>();

        private bool _busy;            // true while an animation sequence is resolving
        private System.Action<Unit> _onUnitPopped; // callback so the board can return units to pool

        public int Count => _units.Count;
        public bool IsFull => _units.Count >= _config.SlotCount;
        public bool IsBusy => _busy;

        public void Setup(GameConfig config, VfxPool vfx, System.Action<Unit> onUnitPopped)
        {
            _config = config;
            _vfx = vfx;
            _onUnitPopped = onUnitPopped;
        }

        /// <summary>World position of slot index i.</summary>
        public Vector3 GetSlotWorldPos(int index)
        {
            Vector3 basePos = slotOrigin != null ? slotOrigin.position : transform.position;
            return basePos + Vector3.right * (_config.SlotSpacing * index);
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
        /// Inserts a unit that has just landed at the given index, then resolves any match.
        /// Returns via coroutine; sets _busy while animating. After resolution, if the bar is
        /// still full, signals game-over through onFull.
        /// </summary>
        public void Accept(Unit unit, int insertIndex, MonoBehaviour coroutineHost,
                           System.Action onResolved, System.Action onFullNoMatch)
        {
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

            // Remove from the logical list up front so repositioning math is correct.
            _units.RemoveRange(start, length);

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

            // Wait until all popped units finished their pop animation.
            while (remaining > 0) yield return null;
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
