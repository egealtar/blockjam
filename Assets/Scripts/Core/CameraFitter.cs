using UnityEngine;
using BlockJam.Data;

namespace BlockJam.Core
{
    /// <summary>
    /// Frames the board so the whole grid stays visible across aspect ratios and resolutions
    /// (the case requires responsive Full HD / 1080p behaviour).
    ///
    /// Design: the camera keeps a FIXED tilt angle (the Block Jam 3D look). The fitter only
    /// changes how FAR the camera sits from the board center, along its current view direction,
    /// so the authored angle is preserved while the board is always fully framed. It recomputes
    /// on every resolution change (device rotation, different aspect) and after each level loads.
    ///
    /// Because the framing is driven by board size + screen aspect, it does NOT fight a
    /// hand-placed transform: it positions the camera deterministically from the board, which
    /// is exactly what "responsive" requires.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        [SerializeField] private Camera cam;

        [Tooltip("Tilt angle (degrees) the camera looks down at the board. ~50 = Block Jam feel.")]
        [SerializeField] private float tiltAngle = 50f;

        [Tooltip("World point the camera frames. Usually the board center (0,0,0).")]
        [SerializeField] private Vector3 boardCenter = Vector3.zero;

        [Tooltip("Extra world padding around the board so pieces aren't flush to the screen edge.")]
        [SerializeField] private float padding = 2f;

        [Tooltip("Fraction of screen height reserved at the bottom for the slot bar / HUD.")]
        [Range(0f, 0.6f)] [SerializeField] private float bottomReserve = 0.28f;

        [Tooltip("Push the framed point forward in world space so the board sits higher on screen, leaving room for the slot bar below.")]
        [SerializeField] private float lookOffsetForward = 0f;

        [Tooltip("Safety multiplier on board width when fitting. >1 pulls the camera back a bit more " +
                 "on tall/narrow screens so the left/right corner jams never fall off-screen. 1.15 = 15% margin.")]
        [Range(1f, 1.6f)] [SerializeField] private float widthSafety = 1.15f;

        private int _lastW, _lastH;
        private float _boardWidth = 10f;   // X extent (world)
        private float _boardDepth = 10f;   // Z extent (world)

        private void Reset() => cam = GetComponent<Camera>();
        private void Awake() { if (cam == null) cam = GetComponent<Camera>(); }

        /// <summary>Call after a level is built so framing matches its (padded) grid size.</summary>
        public void Fit(int gridWidth, int gridHeight, GameConfig config)
        {
            if (config == null) return;
            _boardWidth = gridWidth * config.CellSpacing + padding * 2f;
            _boardDepth = gridHeight * config.CellSpacing + padding * 2f;
            ApplyFit();
        }

        private void LateUpdate()
        {
            // Re-fit every frame: ResetAspect + reframe is cheap, and doing it in LateUpdate (after
            // any layout/resolution change this frame) guarantees the camera aspect always matches
            // the live viewport - so taps stay accurate in Free Aspect and at any window size,
            // without needing a manual resize to "wake up" the camera.
            ApplyFit();
        }

        private void ApplyFit()
        {
            if (cam == null) return;
            _lastW = Screen.width;
            _lastH = Screen.height;

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

            // The board is viewed at a tilt, so its on-screen vertical footprint is roughly
            // depth * cos(tilt) (foreshortened) plus its height contribution. We frame using a
            // bounding radius derived from width (horizontal) and depth (vertical-ish) extents.
            float tiltRad = tiltAngle * Mathf.Deg2Rad;
            float verticalExtent = _boardDepth * Mathf.Cos(tiltRad) * 0.5f + _boardDepth * Mathf.Sin(tiltRad) * 0.25f;
            // Horizontal: include a safety margin so the board's left/right edges (the corner jams)
            // never fall off-screen on tall/narrow aspects, where width is the limiting dimension.
            float horizontalExtent = _boardWidth * 0.5f * widthSafety;

            // Reserve bottom band for slot bar by inflating the vertical requirement.
            float usableVertical = Mathf.Max(0.2f, 1f - bottomReserve);
            float requiredHalfHeight = Mathf.Max(
                verticalExtent / usableVertical,
                horizontalExtent / aspect);

            float distance;
            if (cam.orthographic)
            {
                cam.orthographicSize = requiredHalfHeight;
                distance = Mathf.Max(_boardWidth, _boardDepth); // arbitrary safe pull-back
            }
            else
            {
                float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                distance = requiredHalfHeight / Mathf.Tan(halfFov);
                distance += _boardDepth * 0.5f; // clear the near edge of the tilted board
            }

            // Place the camera at the fixed tilt on the +Z side, looking toward -Z (down at the
            // board). This matches a camera positioned in front of the board on the +Z axis.
            cam.transform.rotation = Quaternion.Euler(tiltAngle, 180f, 0f);
            Vector3 framedPoint = boardCenter + Vector3.forward * lookOffsetForward;
            Vector3 back = -(cam.transform.forward); // points toward +Z
            // Add any active shake offset on top of the framed position so a fail-shake is visible
            // even though we reframe every frame (otherwise the fit would overwrite the shake).
            cam.transform.position = framedPoint + back * distance + _shakeOffset;
        }

        // ---- Shake support (used on fail). The offset is layered on top of the fit each frame. ----
        private Vector3 _shakeOffset;
        private Coroutine _shakeRoutine;

        public void Shake(float duration, float magnitude)
        {
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
        }

        private System.Collections.IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float damper = 1f - Mathf.Clamp01(t / duration);
                _shakeOffset = new Vector3(Random.value * 2f - 1f, Random.value * 2f - 1f, 0f)
                               * magnitude * damper;
                yield return null;
            }
            _shakeOffset = Vector3.zero;
            _shakeRoutine = null;
        }
    }
}