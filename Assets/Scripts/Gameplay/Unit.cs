using System.Collections;
using UnityEngine;
using BlockJam.Data;
using BlockJam.Pooling;
using BlockJam.Util;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// A single tappable piece. Pooled (never Instantiate/Destroy at runtime).
    /// Holds its color identity, the visual mesh, optional Box blocker, and the small
    /// set of animations the game needs (idle, jump-to-slot, pop on match).
    ///
    /// The Unit knows nothing about the board or slots - it only animates itself and
    /// reports completion via callbacks. All decisions live in BoardController/SlotController.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Unit : MonoBehaviour, IPoolable
    {
        [Header("Visual")]
        [Tooltip("Renderer whose material color is tinted to represent the unit color.")]
        [SerializeField] private Renderer bodyRenderer;

        [Tooltip("Optional Animator with Idle/Jump/Die states. Leave null to skip animation.")]
        [SerializeField] private Animator animator;

        [Header("Box Blocker (nice-to-have)")]
        [Tooltip("Child GameObject representing the box shell. Shown when the unit starts boxed.")]
        [SerializeField] private GameObject boxVisual;

        [Header("Anim Params (must match the Animator controller)")]
        [SerializeField] private string idleState = "Idle";
        [SerializeField] private string jumpState = "Jump";
        [SerializeField] private string dieState = "Die";
        [SerializeField] private string runState = "Run";

        [Header("Slot Pose")]
        [Tooltip("X rotation (degrees) the unit settles at once it lands in a slot, tilting it " +
                 "back slightly so its face is more visible to the camera. -25 = a gentle lean.")]
        [SerializeField] private float slotTiltX = -25f;

        // --- runtime state ---
        public UnitColor Color { get; private set; }
        public int CellX { get; private set; }
        public int CellY { get; private set; }
        public bool IsBoxed { get; private set; }

        /// <summary>The actual RGB tint shown for this unit's color (from the palette). Reused for
        /// matching color effects like the running dust puff.</summary>
        public Color DisplayColor { get; private set; }

        private MaterialPropertyBlock _mpb;
        private Collider _collider;
        private Coroutine _moveRoutine;

        // We tint via MaterialPropertyBlock. Different shaders expose the color under different
        // property names: URP/Lit uses "_BaseColor", the SimpleToony shader uses "_Color". We set
        // both so the unit tints correctly no matter which material the body renderer uses.
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _mpb = new MaterialPropertyBlock();
        }

        public void OnSpawned()
        {
            transform.localScale = Vector3.one;
            // Reset orientation: a unit returning from the pool may have been left tilted from its
            // time in the slot bar (slotTiltX). On the board it must stand upright; the slot tilt is
            // re-applied only when it lands in a slot again.
            transform.rotation = Quaternion.identity;
            SetTappable(true);
        }

        public void OnDespawned()
        {
            if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
        }

        /// <summary>Configures the unit's identity and look. Call right after Get() from the pool.</summary>
        public void Init(UnitColor color, int cellX, int cellY, bool boxed, Color displayColor)
        {
            Color = color;
            CellX = cellX;
            CellY = cellY;
            IsBoxed = boxed;
            DisplayColor = displayColor;

            // Stand upright on the board (slot tilt is applied only on landing in a slot).
            transform.rotation = Quaternion.identity;

            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorProp, displayColor);
                _mpb.SetColor(ColorProp, displayColor);
                bodyRenderer.SetPropertyBlock(_mpb);
            }

            if (boxVisual != null) boxVisual.SetActive(boxed);

            // A boxed unit cannot be tapped until the box breaks.
            SetTappable(!boxed);
            PlayIdle();
        }

        public void SetCell(int x, int y)
        {
            CellX = x;
            CellY = y;
        }

        /// <summary>Enables/disables the collider so rapid taps on invalid units are ignored cheaply.</summary>
        /// <summary>
        /// False only while the unit is flying/walking to a slot (so it can't be tapped twice).
        /// The collider stays always enabled, so a freshly-opened neighbour is instantly tappable
        /// with no physics-sync delay; whether a tap actually moves the unit is decided by
        /// pathfinding in BoardController.OnUnitTapped (which also drives the shake feedback).
        /// </summary>
        public bool IsTappable { get; private set; } = true;

        public void SetTappable(bool tappable)
        {
            IsTappable = tappable;
            if (_collider != null && !_collider.enabled) _collider.enabled = true;
        }

        /// <summary>Breaks the box (e.g. an adjacent match cleared next to it), making the unit usable.</summary>
        public void BreakBox()
        {
            if (!IsBoxed) return;
            IsBoxed = false;
            if (boxVisual != null) boxVisual.SetActive(false);
            SetTappable(true);
        }

        // ---- animation helpers ----
        public void PlayIdle() => PlayState(idleState);
        public void PlayJump() => PlayState(jumpState);
        public void PlayDie()  => PlayState(dieState);
        public void PlayRun()  => PlayState(runState);

        private void PlayState(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            // Only play if the controller actually has this state, so a missing clip (e.g. Run not
            // yet added) is a silent no-op rather than a warning every frame.
            if (animator.HasState(0, Animator.StringToHash(stateName)))
                animator.Play(stateName);
        }

        /// <summary>
        /// Walks the unit along a path of world positions (cell centers leading to the open front
        /// edge), facing its movement direction, then continues to the final slot position.
        /// This is the Block Jam "walk out through empty cells" behaviour.
        /// </summary>
        public void WalkPathThenSlot(System.Collections.Generic.List<Vector3> pathWorld, Vector3 slotTarget,
                                     float stepDuration, float slotDuration, System.Action onArrive)
        {
            SetTappable(false);
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(WalkRoutine(pathWorld, slotTarget, stepDuration, slotDuration, onArrive));
        }

        /// <summary>
        /// Raised at each footstep while the unit runs along the board, passing the foot world
        /// position. BoardController subscribes to spawn a little dust puff (movement juice).
        /// </summary>
        public System.Action<Vector3> OnFootstep;

        private IEnumerator WalkRoutine(System.Collections.Generic.List<Vector3> pathWorld, Vector3 slotTarget,
                                        float stepDuration, float slotDuration, System.Action onArrive)
        {
            PlayRun();

            // Walk cell by cell along the board to the exit, facing the direction of travel.
            if (pathWorld != null)
            {
                foreach (var p in pathWorld)
                {
                    Vector3 flatTarget = new Vector3(p.x, transform.position.y, p.z);
                    FaceTowards(flatTarget);
                    // Dust puff at the foot as each step begins.
                    OnFootstep?.Invoke(transform.position);
                    yield return Tweener.Move(transform, flatTarget, stepDuration, Easing.Linear);
                }
            }

            // Then hop into the slot (short arc) and settle.
            PlayJump();
            FaceTowards(slotTarget);
            yield return Tweener.JumpTo(transform, slotTarget, 1.0f, slotDuration, Easing.InOutQuad);

            // Settle facing the camera with a slight backward tilt so the face is more visible.
            transform.rotation = Quaternion.Euler(slotTiltX, 0f, 0f);
            PlayIdle();
            _moveRoutine = null;
            onArrive?.Invoke();
        }

        private void FaceTowards(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>
        /// Flies the unit to a target position with a jump arc (the "throw into slot" feel),
        /// then invokes onArrive. Disables tapping for the duration so it can't be re-selected.
        /// </summary>
        public void JumpToSlot(Vector3 target, float duration, float arcHeight, System.Action onArrive)
        {
            SetTappable(false);
            PlayJump();
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(JumpRoutine(target, duration, arcHeight, onArrive));
        }

        private IEnumerator JumpRoutine(Vector3 target, float duration, float arcHeight, System.Action onArrive)
        {
            // Lift straight up first so the unit clears the board, then travel across at that
            // height, then drop into the slot. This prevents flying through other units.
            float liftHeight = Mathf.Max(arcHeight, 6f);
            Vector3 start = transform.position;
            Vector3 apexStart = start + Vector3.up * liftHeight;
            Vector3 apexEnd = new Vector3(target.x, target.y + liftHeight, target.z);

            float liftT = duration * 0.30f;
            float travelT = duration * 0.40f;
            float dropT = duration * 0.30f;

            yield return Tweener.Move(transform, apexStart, liftT, Easing.OutQuad);   // up
            yield return Tweener.Move(transform, apexEnd, travelT, Easing.InOutQuad); // across (above everything)
            yield return Tweener.Move(transform, target, dropT, Easing.InQuad);       // down into slot

            PlayIdle();
            _moveRoutine = null;
            onArrive?.Invoke();
        }

        /// <summary>
        /// Quick left-right shake to signal "this unit can't be selected" (it's blocked).
        /// Used as negative feedback when the player taps a fully-surrounded unit.
        /// </summary>
        public void ShakeBlocked()
        {
            if (_moveRoutine != null) return; // don't interrupt a real move
            StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            Vector3 origin = transform.position;
            float dur = 0.28f;
            float elapsed = 0f;
            float amp = 0.25f;     // shake distance
            float freq = 30f;      // shake speed

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float damper = 1f - (elapsed / dur);          // fades out
                float offset = Mathf.Sin(elapsed * freq) * amp * damper;
                transform.position = origin + Vector3.right * offset;
                yield return null;
            }
            transform.position = origin;
        }

        /// <summary>Slides the unit to a new slot position (used when the bar compacts after a match).</summary>
        public void SlideTo(Vector3 target, float duration, System.Action onArrive = null)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(SlideRoutine(target, duration, onArrive));
        }

        private IEnumerator SlideRoutine(Vector3 target, float duration, System.Action onArrive)
        {
            yield return Tweener.Move(transform, target, duration, Easing.OutQuad);
            _moveRoutine = null;
            onArrive?.Invoke();
        }

        /// <summary>
        /// A celebratory hop played by each unit of a matched trio just before they pop, mirroring
        /// Block Jam 3D where the three same-colored units jump together at the moment of matching.
        /// Plays the Jump animation and arcs up then back down.
        /// </summary>
        public IEnumerator MatchJump(float jumpHeight, float duration)
        {
            PlayJump();
            Vector3 start = transform.position;
            Vector3 apex = start + Vector3.up * jumpHeight;
            float half = Mathf.Max(0.01f, duration * 0.5f);
            yield return Tweener.Move(transform, apex, half, Easing.OutQuad);
            yield return Tweener.Move(transform, start, half, Easing.InQuad);
        }

        /// <summary>Plays the pop (die anim + scale-out) then invokes onDone so the owner can pool it.</summary>
        public void PlayPop(float duration, System.Action onDone)
        {
            PlayDie();
            StartCoroutine(PopRoutine(duration, onDone));
        }

        private IEnumerator PopRoutine(float duration, System.Action onDone)
        {
            // tiny anticipation then scale to zero
            yield return Tweener.Scale(transform, transform.localScale * 1.15f, duration * 0.3f, Easing.OutQuad);
            yield return Tweener.Scale(transform, Vector3.zero, duration * 0.7f, Easing.InQuad);
            onDone?.Invoke();
        }
    }
}