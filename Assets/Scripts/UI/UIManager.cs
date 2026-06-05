using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BlockJam.UI
{
    /// <summary>
    /// Owns the gameplay HUD and the win/fail overlays.
    /// Pure view layer: it raises C# events when its buttons are pressed and exposes simple
    /// Show/Hide methods. GameController subscribes and decides what actually happens
    /// (load next level, retry, etc.), keeping UI free of game logic.
    ///
    /// On win/fail we first flash a comic-style splash image ("BOOM!" / a fail stamp) for a beat,
    /// then reveal the action buttons - a small bit of "juice" that makes the result feel punchy.
    ///
    /// TextMeshPro is used for crisp text at any resolution (part of the responsive 1080p goal).
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private TMP_Text levelLabel;

        [Header("Win Screen")]
        [SerializeField] private GameObject winPanelRoot;   // assigned in inspector
        [SerializeField] private GameObject winSplash;      // the BOOM! image, shown first
        [SerializeField] private GameObject winButtons;     // continue button group, shown after
        [SerializeField] private Button continueButton;

        [Header("Fail Screen")]
        [SerializeField] private GameObject failPanelRoot;
        [SerializeField] private GameObject failSplash;     // the fail stamp image, shown first
        [SerializeField] private GameObject failButtons;    // retry button group, shown after
        [SerializeField] private Button retryButton;

        [Header("Timing")]
        [Tooltip("How long the splash image stays on screen before the buttons appear.")]
        [SerializeField] private float splashDuration = 1f;

        public event System.Action OnContinuePressed;
        public event System.Action OnRetryPressed;

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
            StartSplashThenButtons(winSplash, winButtons);
        }

        public void ShowFail()
        {
            HideAll();
            if (failPanelRoot != null) failPanelRoot.SetActive(true);
            StartSplashThenButtons(failSplash, failButtons);
        }

        /// <summary>Shows the splash image for splashDuration, then hides it and reveals the buttons.</summary>
        private void StartSplashThenButtons(GameObject splash, GameObject buttons)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(SplashRoutine(splash, buttons));
        }

        private IEnumerator SplashRoutine(GameObject splash, GameObject buttons)
        {
            if (splash != null) splash.SetActive(true);
            if (buttons != null) buttons.SetActive(false);

            yield return new WaitForSeconds(splashDuration);

            if (splash != null) splash.SetActive(false);
            if (buttons != null) buttons.SetActive(true);
            _routine = null;
        }

        public void HideAll()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (winPanelRoot != null) winPanelRoot.SetActive(false);
            if (failPanelRoot != null) failPanelRoot.SetActive(false);
        }
    }
}