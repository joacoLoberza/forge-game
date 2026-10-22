using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Interaction;
using ForjaGame.Core;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Interfaz segregada para estaciones interactivas (ISP / DIP).
    /// Permite a sistemas externos consultar ocupación o liberar la estación sin acoplarse a implementaciones concretas.
    /// </summary>
    public interface IStationController
    {
        bool IsOccupied { get; }
        GameObject CurrentInteractor { get; }
        void ReleaseStation();
    }

    /// <summary>
    /// Clase base abstracta para estaciones de forja 3D interactivas (SRP, OCP, LSP).
    /// Centraliza el ciclo de vida de interacción, el control de ocupación (preparado para multiplayer),
    /// el congelamiento de movimiento del jugador y el registro global O(1) de estaciones activas.
    /// </summary>
    public abstract class BaseForgeStationController : MonoBehaviour, IInteractable, IStationController
    {
        [Header("Interfaz de Estación")]
        [SerializeField] protected GameObject stationUI;
        [SerializeField] protected float maxStationDistance = 2.20f;

        protected GameObject _currentInteractor;
        protected bool _isOccupied;

        private static readonly List<BaseForgeStationController> _activeStations = new();

        public bool IsOccupied => _isOccupied;
        public GameObject CurrentInteractor => _currentInteractor;
        public GameObject StationUI => stationUI;
        public virtual string InteractionPrompt => "Usar Estación";

        protected float GetDistanceToInteractor(GameObject interactor)
        {
            if (interactor == null) return float.PositiveInfinity;
            Collider col = GetComponentInChildren<Collider>();
            Vector3 stationPoint = col != null ? col.ClosestPoint(interactor.transform.position) : transform.position;
            return Vector3.Distance(stationPoint, interactor.transform.position);
        }

        protected virtual void Update()
        {
        }

        /// <summary>
        /// Comprueba en O(1) si cualquier estación del juego está actualmente en uso.
        /// </summary>
        public static bool IsAnyStationOccupied()
        {
            for (int i = 0; i < _activeStations.Count; i++)
            {
                if (_activeStations[i] != null && _activeStations[i]._isOccupied) return true;
            }
            return false;
        }

        public static bool IsPlayerOccupyingAnyStation(GameObject interactor)
        {
            if (interactor == null) return false;
            for (int i = 0; i < _activeStations.Count; i++)
            {
                if (_activeStations[i] != null && _activeStations[i].IsOccupiedBy(interactor)) return true;
            }
            return false;
        }

        public static BaseForgeStationController GetStationOccupiedBy(GameObject interactor)
        {
            if (interactor == null) return null;
            for (int i = 0; i < _activeStations.Count; i++)
            {
                if (_activeStations[i] != null && _activeStations[i].IsOccupiedBy(interactor))
                    return _activeStations[i];
            }
            return null;
        }

        public bool IsOccupiedBy(GameObject interactor)
        {
            if (!_isOccupied) return false;
            if (_currentInteractor == null || interactor == null) return false;
            return _currentInteractor == interactor ||
                   interactor.transform.IsChildOf(_currentInteractor.transform) ||
                   _currentInteractor.transform.IsChildOf(interactor.transform);
        }

        protected virtual void OnEnable()
        {
            if (!_activeStations.Contains(this))
                _activeStations.Add(this);
        }

        protected virtual void OnDisable()
        {
            _activeStations.Remove(this);
        }

        public virtual bool CanInteract(GameObject interactor)
        {
            if (!_isOccupied) return true;
            return IsOccupiedBy(interactor);
        }

        public virtual void Interact(GameObject interactor)
        {
            // Si la estación ya está ocupada por otro jugador, rechazar interacción
            if (_isOccupied && !IsOccupiedBy(interactor))
            {
                return;
            }

            // Toggle: si la estación ya está ocupada por este jugador o su UI ya está abierta, cerrarla y salir
            bool isAlreadyOpen = _isOccupied && _currentInteractor != null && IsOccupiedBy(interactor);
            if (stationUI != null)
            {
                var panel = stationUI.GetComponent<UI.Core.IUIPanel>();
                if (panel != null && panel.IsOpen) isAlreadyOpen = true;
            }

            if (isAlreadyOpen)
            {
                ReleaseStation();
                return;
            }

            // Si está cerrada, abrirla
            _isOccupied = true;
            _currentInteractor = interactor;

            // Bloquear movimiento mientras se usa la estación
            if (interactor != null)
            {
                PlayerMovement movement = interactor.GetComponentInParent<PlayerMovement>();
                movement?.SetMovementEnabled(false);
            }

            if (stationUI == null)
            {
                if (this is AnvilStationController)
                    stationUI = FindFirstObjectByType<UI.AnvilPanel>(FindObjectsInactive.Include)?.gameObject;
                else if (this is FurnaceStationController)
                    stationUI = FindFirstObjectByType<UI.FurnacePanel>(FindObjectsInactive.Include)?.gameObject;
                else if (this is PressStationController)
                    stationUI = FindFirstObjectByType<UI.PressPanel>(FindObjectsInactive.Include)?.gameObject;
            }

            if (stationUI != null)
            {
                var panel = stationUI.GetComponent<UI.Core.IUIPanel>();
                if (panel != null)
                {
                    if (!panel.IsOpen) panel.Open();
                }
                else
                {
                    stationUI.SetActive(true);
                }
            }

            OnStationEntered(interactor);
        }

        public virtual void ReleaseStation()
        {
            if (stationUI != null && stationUI != gameObject && !stationUI.transform.IsChildOf(transform))
            {
                var panel = stationUI.GetComponent<UI.Core.IUIPanel>();
                if (panel != null && panel.IsOpen)
                    panel.Close();
                else
                    stationUI.SetActive(false);
            }

            // Desbloquear movimiento del jugador
            if (_currentInteractor != null)
            {
                PlayerMovement movement = _currentInteractor.GetComponentInParent<PlayerMovement>();
                movement?.SetMovementEnabled(true);
            }

            GameObject previousInteractor = _currentInteractor;
            _currentInteractor = null;
            _isOccupied = false;

            FindFirstObjectByType<GameStateController>()?.TryChangeState(GameState.Exploration);

            OnStationExited(previousInteractor);
        }

        /// <summary>
        /// Hook opcional para estaciones derivadas cuando un jugador entra.
        /// </summary>
        protected virtual void OnStationEntered(GameObject interactor) { }

        /// <summary>
        /// Hook opcional para estaciones derivadas cuando un jugador sale.
        /// </summary>
        protected virtual void OnStationExited(GameObject previousInteractor) { }
    }
}
