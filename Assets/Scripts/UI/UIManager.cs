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
    /// TextMeshPro is used for crisp text at any resolution (part of the responsive 1080p goal).
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private TMP_Text levelLabel;

        [Header("Win Screen")]
        [SerializeField] private GameObject winPanelRoot;   // assigned in inspector
        [SerializeField] private Button continueButton;

        [Header("Fail Screen")]
        [SerializeField] private GameObject failPanelRoot;
        [SerializeField] private Button retryButton;

        public event System.Action OnContinuePressed;
        public event System.Action OnRetryPressed;

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
        }

        public void ShowFail()
        {
            HideAll();
            if (failPanelRoot != null) failPanelRoot.SetActive(true);
        }

        public void HideAll()
        {
            if (winPanelRoot != null) winPanelRoot.SetActive(false);
            if (failPanelRoot != null) failPanelRoot.SetActive(false);
        }
    }
}
