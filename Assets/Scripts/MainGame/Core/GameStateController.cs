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
    /// Mantiene el estado global de la partida y notifica sus cambios.
    /// No controla UI, audio ni gameplay concreto; esos sistemas reaccionan
    /// al evento sin depender de esta implementación.
    /// </summary>
    public class GameStateController : MonoBehaviour
    {
        [SerializeField] private GameState initialState = GameState.Exploration;
        [SerializeField] private GameState currentState;

        public GameState CurrentState => currentState;

        public event Action<GameState, GameState> StateChanged;

        private void Awake()
        {
            currentState = initialState;
        }

        public bool TryChangeState(GameState nextState)
        {
            if (CurrentState == nextState || !CanChangeTo(nextState))
                return false;

            GameState previousState = CurrentState;
            currentState = nextState;
            StateChanged?.Invoke(previousState, nextState);
            return true;
        }

        public bool IsInState(GameState state)
        {
            return CurrentState == state;
        }

        private bool CanChangeTo(GameState nextState)
        {
            if (CurrentState == GameState.Victory || CurrentState == GameState.Defeat)
                return false;

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
