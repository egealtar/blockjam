using UnityEngine;
using BlockJam.Data;

namespace BlockJam.Core
{
    /// <summary>
    /// Frames the board so it fits on screen across aspect ratios (the case requires
    /// "responsive: adapt properly to different screen sizes and aspect ratios", Full HD 1080p).
    ///
    /// Strategy: a fixed tilted 3D camera (the Block Jam 3D look). On each level / screen change
    /// we adjust the camera distance (orthographic size or perspective pull-back) from the grid
    /// dimensions plus padding, and we re-fit on resolution changes so rotating a device or
    /// running on a different aspect keeps the whole board visible.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [Tooltip("Extra world-space padding around the board so pieces aren't flush to the edge.")]
        [SerializeField] private float padding = 1.5f;
        [Tooltip("Reserve this fraction of vertical space at the bottom for the slot bar / HUD.")]
        [Range(0f, 0.5f)] [SerializeField] private float bottomReserve = 0.22f;

        private int _lastW, _lastH;
        private float _boardWorldWidth, _boardWorldDepth;

        private void Reset() => cam = GetComponent<Camera>();
        private void Awake() { if (cam == null) cam = GetComponent<Camera>(); }

        /// <summary>Call after a level is built so the framing matches its grid size.</summary>
        public void Fit(LevelData level, GameConfig config)
        {
            _boardWorldWidth = level.Width * config.CellSpacing + padding * 2f;
            _boardWorldDepth = level.Height * config.CellSpacing + padding * 2f;
            ApplyFit();
        }

        private void Update()
        {
            // Re-fit only when the screen actually changes - cheap and avoids per-frame work.
            if (Screen.width != _lastW || Screen.height != _lastH)
                ApplyFit();
        }

        private void ApplyFit()
        {
            if (cam == null) return;
            _lastW = Screen.width;
            _lastH = Screen.height;

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            // Account for the reserved bottom band by enlarging the required vertical extent.
            float usableVertical = 1f - bottomReserve;
            float requiredHalfHeight = Mathf.Max(
                _boardWorldDepth * 0.5f / Mathf.Max(0.01f, usableVertical),
                (_boardWorldWidth * 0.5f) / aspect);

            if (cam.orthographic)
            {
                cam.orthographicSize = requiredHalfHeight;
            }
            else
            {
                // Pull the camera back along its local forward so the board fits the FOV.
                float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distance = requiredHalfHeight / Mathf.Tan(halfFov);
                // Keep current orientation; move along -forward from the board center (origin).
                Vector3 dir = -transform.forward;
                transform.position = dir * distance + Vector3.up * 0f;
            }
        }
    }
}
