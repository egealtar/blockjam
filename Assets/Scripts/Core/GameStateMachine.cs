using System;

namespace BlockJam.Core
{
    public enum GameState
    {
        Loading,
        Playing,
        Won,
        Lost
    }

    /// <summary>
    /// A tiny explicit state machine for the gameplay scene. Not a singleton - GameController
    /// owns one instance. Centralizing the state (instead of scattering bools) makes the flow
    /// auditable: input is only accepted while Playing, win/fail screens only appear once, etc.
    /// </summary>
    public class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.Loading;

        /// <summary>Fired after a successful transition with (from, to).</summary>
        public event Action<GameState, GameState> OnChanged;

        public bool IsPlaying => Current == GameState.Playing;

        public void Set(GameState next)
        {
            if (next == Current) return;
            var prev = Current;
            Current = next;
            OnChanged?.Invoke(prev, next);
        }
    }
}
