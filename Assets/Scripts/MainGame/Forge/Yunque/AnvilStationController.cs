using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Physical 3D Anvil & Quenching station controller.
    /// Supports resuming active ingots on table, station toggling, and movement locking.
    /// </summary>
    public class AnvilStationController : BaseForgeStationController
    {
        [Header("Anvil References")]
        [SerializeField] private AnvilController anvilController;
        [SerializeField] private WeaponAssemblyController assemblyController;
        [SerializeField] private GameObject anvilUI;

        public AnvilController Anvil => anvilController;
        public WeaponAssemblyController Assembly => assemblyController;

        private void Awake()
        {
            if (anvilController == null)
                anvilController = GetComponentInChildren<AnvilController>() ?? GetComponent<AnvilController>();

            if (assemblyController == null)
                assemblyController = GetComponentInChildren<WeaponAssemblyController>() ?? GetComponent<WeaponAssemblyController>();

            if (stationUI == gameObject) stationUI = null;

            if (stationUI == null && anvilUI != null)
                stationUI = anvilUI;
            else if (anvilUI == null && stationUI != null)
                anvilUI = stationUI;

            if (stationUI == null)
            {
                var panel = FindFirstObjectByType<UI.AnvilPanel>(FindObjectsInactive.Include);
                if (panel != null) stationUI = panel.gameObject;
            }
        }

        public override string InteractionPrompt => "Forjar en el Yunque";

        public override bool CanInteract(GameObject interactor) => true;

        public override void Interact(GameObject interactor)
        {
            // Toggle: si la estación ya está en uso por este jugador o su UI está abierta, cerrarla y liberar
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

            if (anvilController != null && (anvilController.HasIngotOnAnvil || anvilController.IsCarvingHandle))
            {
                _isOccupied = true;
                _currentInteractor = interactor;
                interactor.GetComponentInParent<PlayerMovement>()?.SetMovementEnabled(false);
                FindFirstObjectByType<GameStateController>()?.TryChangeState(GameState.Forge);

                if (stationUI != null) stationUI.SetActive(false);
                anvilController.ResumeForging();
                StartSmoothCameraToAnvil(interactor);
                OnStationEntered(interactor);
                return;
            }

            if (stationUI == null)
            {
                var panel = FindFirstObjectByType<UI.AnvilPanel>(FindObjectsInactive.Include);
                if (panel != null) stationUI = panel.gameObject;
            }

            if (stationUI != null && stationUI != gameObject)
            {
                var panel = stationUI.GetComponent<UI.Core.IUIPanel>();
                if (panel != null)
                    panel.Open();
                else
                    stationUI.SetActive(true);
            }

            _isOccupied = true;
            _currentInteractor = interactor;
            interactor.GetComponentInParent<PlayerMovement>()?.SetMovementEnabled(false);
            FindFirstObjectByType<GameStateController>()?.TryChangeState(GameState.Forge);
            StartSmoothCameraToAnvil(interactor);

            OnStationEntered(interactor);
        }

        private void StartSmoothCameraToAnvil(GameObject interactor)
        {
            var cam = GetInteractorCamera(interactor);
            if (cam == null) return;

            // Calcular el punto de enfoque sobre la mesa del yunque
            Vector3 focusPoint = anvilController != null
                ? anvilController.transform.position + new Vector3(0f, 0.55f, 0f)
                : transform.position + new Vector3(0f, 0.55f, 0f);

            // Posición de cámara oblicua desde arriba y ligeramente atrás del yunque
            Vector3 camOffset = transform.TransformDirection(new Vector3(0f, 0.90f, -0.18f));
            Vector3 camPos = focusPoint + camOffset;
            Quaternion targetRot = Quaternion.LookRotation(focusPoint - camPos);

            // Velocidad moderada: ~4.5 → viaje suave ~0.7s, sin TP
            cam.SetStationView(camPos, targetRot, 4.5f);
        }

        private PlayerCameraFollow GetInteractorCamera(GameObject interactor)
        {
            if (interactor != null)
            {
                var pMove = interactor.GetComponentInParent<PlayerMovement>();
                if (pMove != null && pMove.PlayerCamera != null)
                {
                    var cf = pMove.PlayerCamera.GetComponent<PlayerCameraFollow>();
                    if (cf != null) return cf;
                }
            }
            return PlayerCameraFollow.Instance;
        }

        public override void ReleaseStation()
        {
            if (anvilController != null && (anvilController.HasIngotOnAnvil || anvilController.IsCarvingHandle))
            {
                anvilController.PauseForgingAndLeaveOnTable();
            }

            GetInteractorCamera(_currentInteractor)?.ClearStationView();

            if (_currentInteractor != null)
            {
                PlayerMovement movement = _currentInteractor.GetComponentInParent<PlayerMovement>();
                movement?.SetMovementEnabled(true);
            }

            GameObject previousInteractor = _currentInteractor;
            _currentInteractor = null;
            _isOccupied = false;

            if (stationUI != null && stationUI != gameObject && !stationUI.transform.IsChildOf(transform))
            {
                var panel = stationUI.GetComponent<UI.Core.IUIPanel>();
                if (panel != null && panel.IsOpen)
                    panel.Close();
                else
                    stationUI.SetActive(false);
            }

            FindFirstObjectByType<GameStateController>()?.TryChangeState(GameState.Exploration);

            OnStationExited(previousInteractor);
        }
    }
}
