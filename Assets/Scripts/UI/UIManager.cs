using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlockJam.UI
{
    /// <summary>
    /// Owns the gameplay HUD and the win/fail overlays.
    /// Pure view layer: raises C# events when its buttons are pressed and exposes Show/Hide.
    /// GameController subscribes and decides what happens (next level, retry), keeping UI logic-free.
    ///
    /// Win/fail first flash a comic-style splash image with a punchy entrance (a quick scale-up
    /// "pop" plus a short shake) over an optional dimmed backdrop, then reveal the action buttons.
    /// This is purely presentational juice to make the result feel impactful.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private TMP_Text levelLabel;

        [Header("Win Screen")]
        [SerializeField] private GameObject winPanelRoot;
        [SerializeField] private GameObject winSplash;
        [SerializeField] private GameObject winButtons;
        [SerializeField] private Button continueButton;

        [Header("Fail Screen")]
        [SerializeField] private GameObject failPanelRoot;
        [SerializeField] private GameObject failSplash;
        [SerializeField] private GameObject failButtons;
        [SerializeField] private Button retryButton;
        [Tooltip("Optional dim backdrop (a full-screen semi-transparent Image) faded in behind the fail splash.")]
        [SerializeField] private CanvasGroup failDim;

        [Header("Timing / Juice")]
        [SerializeField] private float splashDuration = 1f;
        [Tooltip("Duration of the splash 'pop' scale-in.")]
        [SerializeField] private float popDuration = 0.35f;
        [Tooltip("How far past full size the pop overshoots (1.2 = 20% overshoot).")]
        [SerializeField] private float popOvershoot = 1.25f;
        [Tooltip("Shake strength (local pixels) applied to the fail splash right after it pops in.")]
        [SerializeField] private float shakeMagnitude = 18f;
        [SerializeField] private float shakeDuration = 0.3f;

        public event System.Action OnContinuePressed;
        public event System.Action OnRetryPressed;

        /// <summary>Raised the instant a fail is shown, so GameController can shake the camera / play VFX.</summary>
        public event System.Action OnFailPresented;

        private Coroutine _routine;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => OnContinuePressed?.Invoke());
            if (retryButton != null) retryButton.onClick.AddListener(() => OnRetryPressed?.Invoke());
            HideAll();
        }

        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveAllListeners();
            if (retryButton != null) retryButton.onClick.RemoveAllListeners();
        }

        public void SetLevelNumber(int levelNumber)
        {
            if (levelLabel != null) levelLabel.text = $"Level {levelNumber}";
        }

        public void ShowWin()
        {
            HideAll();
            if (winPanelRoot != null) winPanelRoot.SetActive(true);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(SplashRoutine(winSplash, winButtons, dim: null, shake: false));
        }

        public void ShowFail()
        {
            HideAll();
            if (failPanelRoot != null) failPanelRoot.SetActive(true);
            OnFailPresented?.Invoke();   // camera shake + VFX, handled by GameController
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(SplashRoutine(failSplash, failButtons, failDim, shake: true));
        }

        private IEnumerator SplashRoutine(GameObject splash, GameObject buttons, CanvasGroup dim, bool shake)
        {
            if (buttons != null) buttons.SetActive(false);

            // Fade in the dim backdrop quickly.
            if (dim != null)
            {
                dim.gameObject.SetActive(true);
                yield return Fade(dim, 0f, 1f, 0.18f);
            }

            if (splash != null)
            {
                splash.SetActive(true);
                var rt = splash.transform as RectTransform;
                if (rt != null)
                {
                    // Pop: scale 0 -> overshoot -> 1 (a springy entrance).
                    yield return Pop(rt);
                    if (shake) yield return Shake(rt);
                }
            }

            yield return new WaitForSeconds(splashDuration);

            if (splash != null) splash.SetActive(false);
            if (buttons != null) buttons.SetActive(true);
            _routine = null;
        }

        private IEnumerator Pop(RectTransform rt)
        {
            float t = 0f;
            Vector3 baseScale = Vector3.one;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / popDuration);
                // Ease-out overshoot then settle to 1.
                float s = Mathf.LerpUnclamped(0f, 1f, 1f - Mathf.Pow(1f - p, 3f));
                float scale = Mathf.Lerp(popOvershoot, 1f, p) * s;
                rt.localScale = baseScale * scale;
                yield return null;
            }
            rt.localScale = baseScale;
        }

        private IEnumerator Shake(RectTransform rt)
        {
            Vector2 origin = rt.anchoredPosition;
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float damper = 1f - Mathf.Clamp01(t / shakeDuration);
                Vector2 offset = new Vector2(
                    (Random.value * 2f - 1f),
                    (Random.value * 2f - 1f)) * shakeMagnitude * damper;
                rt.anchoredPosition = origin + offset;
                yield return null;
            }
            rt.anchoredPosition = origin;
        }

        private IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
        {
            float t = 0f;
            g.alpha = from;
            while (t < dur)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            g.alpha = to;
        }

        public void HideAll()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (winPanelRoot != null) winPanelRoot.SetActive(false);
            if (failPanelRoot != null) failPanelRoot.SetActive(false);
            if (failDim != null) { failDim.alpha = 0f; failDim.gameObject.SetActive(false); }
        }
    }
}