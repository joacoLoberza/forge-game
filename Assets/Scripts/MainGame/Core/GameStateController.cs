using System;
using UnityEngine;

namespace ForjaGame.Core
{
    public enum GameState
    {
        Exploration,
        Forge,
        Inventory,
        Pause,
        Victory,
        Defeat
    }

    /// <summary>
    /// Ajuste global persistente para guías y siluetas tutoriales en las estaciones de forja.
    /// </summary>
    public static class TutorialSettings
    {
        private const string PrefKey = "Forja_Tutorial_Guides";
        public static bool IsTutorialEnabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static void Toggle() => IsTutorialEnabled = !IsTutorialEnabled;
    }

    /// <summary>
    /// Mantiene el estado global de la partida y notifica sus cambios.
    /// </summary>
    public class GameStateController : MonoBehaviour
    {
        public static GameStateController Instance { get; private set; }

        [SerializeField] private GameState initialState = GameState.Exploration;
        [SerializeField] private GameState currentState;

        public GameState CurrentState => currentState;
        public static GameState State => Instance != null ? Instance.CurrentState : GameState.Exploration;
        public static bool IsForging => State == GameState.Forge;

        public event Action<GameState, GameState> StateChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            currentState = initialState;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static bool ChangeState(GameState next) => (Instance ?? FindFirstObjectByType<GameStateController>())?.TryChangeState(next) ?? false;

        public bool TryChangeState(GameState nextState)
        {
            if (CurrentState == nextState || !CanChangeTo(nextState))
                return false;

            GameState previousState = CurrentState;
            currentState = nextState;
            StateChanged?.Invoke(previousState, nextState);
            return true;
        }

        public bool IsInState(GameState state) => CurrentState == state;

        private bool CanChangeTo(GameState nextState)
        {
            if (CurrentState == GameState.Victory || CurrentState == GameState.Defeat)
                return nextState == GameState.Exploration;

            if (nextState == GameState.Pause)
                return CurrentState != GameState.Pause;

            if (CurrentState == GameState.Pause)
                return nextState == GameState.Exploration || nextState == GameState.Forge;

            if (nextState == GameState.Forge)
                return CurrentState == GameState.Exploration ||
                       CurrentState == GameState.Pause ||
                       CurrentState == GameState.Inventory;

            if (nextState == GameState.Inventory)
                return CurrentState == GameState.Exploration ||
                       CurrentState == GameState.Pause ||
                       CurrentState == GameState.Forge;

            return nextState == GameState.Exploration ||
                   nextState == GameState.Victory ||
                   nextState == GameState.Defeat;
        }
    }
}
