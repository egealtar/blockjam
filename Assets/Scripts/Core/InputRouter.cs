using UnityEngine;
using BlockJam.Gameplay;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlockJam.Core
{
    public class InputRouter : MonoBehaviour
    {
        [SerializeField] private Camera gameCamera;

        private BoardController _board;
        private bool _enabled = true;

        public void Setup(BoardController board, Camera cam)
        {
            _board = board;
            if (cam != null) gameCamera = cam;
        }

        public void SetEnabled(bool enabled) => _enabled = enabled;

        private void Update()
        {
            if (_board == null || !_enabled) return;

            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null) return;

            if (TryGetPressPosition(out Vector2 screenPos))
            {
                Ray ray = cam.ScreenPointToRay(screenPos);
                // Unbounded ray length: the responsive camera can sit very far from the board on
                // wide/odd aspects, and a fixed max distance (e.g. 1000) made the ray stop short so
                // taps silently missed. Mathf.Infinity guarantees the ray always reaches the board.
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
                {
                    Unit unit = hit.collider.GetComponentInParent<Unit>();
                    if (unit != null) _board.OnUnitTapped(unit);
                }
            }
        }

        private bool TryGetPressPosition(out Vector2 pos)
        {
            pos = default;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                pos = mouse.position.ReadValue();
                return true;
            }
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                pos = touch.primaryTouch.position.ReadValue();
                return true;
            }
            return false;
#else
            if (Input.GetMouseButtonDown(0))
            {
                pos = Input.mousePosition;
                return true;
            }
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                pos = Input.GetTouch(0).position;
                return true;
            }
            return false;
#endif
        }
    }
}