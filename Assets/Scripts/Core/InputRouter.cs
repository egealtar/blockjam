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
        [SerializeField] private float rayDistance = 1000f;

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
            if (_board == null) { Debug.Log("BOARD NULL - board baglanmamis"); return; }
            if (!_enabled) return;

            Camera cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null) { Debug.Log("KAMERA NULL"); return; }

            if (TryGetPressPosition(out Vector2 screenPos))
            {
                Debug.Log("TIKLAMA: " + screenPos + " | ekran: " + Screen.width + "x" + Screen.height);
                Ray ray = cam.ScreenPointToRay(screenPos);
                Debug.DrawRay(ray.origin, ray.direction * rayDistance, Color.red, 3f);

                if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
                {
                    Debug.Log("RAYCAST DEGDI: " + hit.collider.name);
                    Unit unit = hit.collider.GetComponentInParent<Unit>();
                    if (unit != null) { Debug.Log("UNIT BULUNDU, board'a gonderiliyor"); _board.OnUnitTapped(unit); }
                    else Debug.Log("Collidera degdi ama Unit yok: " + hit.collider.name);
                }
                else Debug.Log("RAYCAST HICBIR SEYE DEGMEDI");
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