using UnityEngine;
using BlockJam.Data;
using BlockJam.Gameplay;
using BlockJam.Save;
using BlockJam.UI;

namespace BlockJam.Core
{
    /// <summary>
    /// Composition root for the gameplay scene. This is the single MonoBehaviour that wires
    /// every subsystem together and owns the high-level flow. It deliberately replaces the
    /// "singletons everywhere" pattern: each system is referenced explicitly and handed its
    /// dependencies via Setup(...). That makes data flow obvious and the systems unit-testable.
    ///
    /// Responsibilities:
    ///  - On Start: build pools, load saved progress, build the current level, start playing.
    ///  - React to board-cleared (win) and slot-full (lose) via callbacks.
    ///  - Drive UI (level label, win/fail screens) and the state machine.
    ///  - Persist progress on win so a restart resumes on the right level (case requirement).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private GameConfig config;
        [SerializeField] private ColorPalette palette;

        [Header("Systems (assigned in scene)")]
        [SerializeField] private BoardController board;
        [SerializeField] private SlotController slots;
        [SerializeField] private UnitPool unitPool;
        [SerializeField] private VfxPool vfxPool;
        [SerializeField] private InputRouter input;
        [SerializeField] private CameraFitter cameraFitter;
        [SerializeField] private UIManager ui;
        [SerializeField] private Camera gameCamera;

        private readonly GameStateMachine _fsm = new GameStateMachine();
        private SaveSystem _save;
        private int _currentLevelIndex;

        private void Start()
        {
            // --- 1. Pools (the only place Instantiate happens, at prewarm time) ---
            unitPool.Setup(config.UnitPrewarm);
            vfxPool.Setup(config.VfxPrewarm);

            // --- 2. Save: resume on the stored level ---
            _save = new SaveSystem();
            _currentLevelIndex = _save.Load().CurrentLevelIndex;

            // --- 3. Wire subsystems with explicit dependencies ---
            slots.Setup(config, vfxPool, onUnitPopped: board.ReturnUnitToPool);
            board.Setup(config, palette, unitPool, slots,
                        onBoardCleared: HandleWin,
                        onLose: HandleLose);
            input.Setup(board, gameCamera);

            ui.OnContinuePressed += HandleContinue;
            ui.OnRetryPressed += HandleRetry;

            // --- 4. Go ---
            LoadCurrentLevel();
        }

        private void OnDestroy()
        {
            if (ui != null)
            {
                ui.OnContinuePressed -= HandleContinue;
                ui.OnRetryPressed -= HandleRetry;
            }
        }

        private void LoadCurrentLevel()
        {
            _fsm.Set(GameState.Loading);
            ui.HideAll();

            LevelData level = config.GetLevel(_currentLevelIndex);
            if (level == null)
            {
                Debug.LogError("[GameController] No levels configured in GameConfig.");
                return;
            }

            slots.ClearImmediate();
            board.BuildLevel(level);
            if (cameraFitter != null) cameraFitter.Fit(level, config);
            if (ui != null) ui.SetLevelNumber(level.LevelNumber);

            input.SetEnabled(true);
            _fsm.Set(GameState.Playing);
        }

        private void HandleWin()
        {
            if (!_fsm.IsPlaying) return;
            _fsm.Set(GameState.Won);
            input.SetEnabled(false);

            if (vfxPool != null && gameCamera != null)
                vfxPool.PlayWin(gameCamera.transform.position + gameCamera.transform.forward * 6f);

            // Advance + persist so a restart resumes on the next level.
            _currentLevelIndex = config.ClampLevelIndex(_currentLevelIndex + 1);
            _save.SaveLevelProgress(_currentLevelIndex);

            if (ui != null) ui.ShowWin();
        }

        private void HandleLose()
        {
            if (!_fsm.IsPlaying) return;
            _fsm.Set(GameState.Lost);
            input.SetEnabled(false);
            if (ui != null) ui.ShowFail();
        }

        // Win screen -> next level (already advanced + saved in HandleWin).
        private void HandleContinue() => LoadCurrentLevel();

        // Fail screen -> replay the same level.
        private void HandleRetry() => LoadCurrentLevel();
    }
}