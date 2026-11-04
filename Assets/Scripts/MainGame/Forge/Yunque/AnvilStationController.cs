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

            ForgeStationModelSetup.SetupAnvilVisual(false);
        }

        public override string InteractionPrompt
        {
            get
            {
                var bridge = GetComponent<ForjaGame.Multiplayer.NetworkForgeStationBridge>();
                if (bridge != null && bridge.IsSpawned && bridge.IsOccupiedInNetwork)
                {
                    if (Unity.Netcode.NetworkManager.Singleton != null && bridge.CurrentOccupantClientId != Unity.Netcode.NetworkManager.Singleton.LocalClientId)
                        return "Yunque ocupado";
                }
                return "Forjar en el Yunque";
            }
        }

        public override bool CanInteract(GameObject interactor)
        {
            var bridge = GetComponent<ForjaGame.Multiplayer.NetworkForgeStationBridge>();
            if (bridge != null && bridge.IsSpawned && bridge.IsOccupiedInNetwork)
            {
                if (Unity.Netcode.NetworkManager.Singleton != null && bridge.CurrentOccupantClientId != Unity.Netcode.NetworkManager.Singleton.LocalClientId)
                    return false;
            }
            if (!_isOccupied) return true;
            return IsOccupiedBy(interactor);
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

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

            GetComponent<ForjaGame.Multiplayer.NetworkForgeStationBridge>()?.RequestOccupyStation();

            if (anvilController != null && (anvilController.HasIngotOnAnvil || anvilController.IsCarvingHandle))
            {
                _isOccupied = true;
                _currentInteractor = interactor;
                interactor.GetComponentInParent<PlayerMovement>()?.SetMovementEnabled(false);
                GameStateController.ChangeState(GameState.Forge);

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
            GameStateController.ChangeState(GameState.Forge);
            StartSmoothCameraToAnvil(interactor);

            OnStationEntered(interactor);
        }

        protected override void GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset)
        {
            float baseY = ForgeStationModelSetup.GetGroundHeightAt(transform.position.x, transform.position.z);
            focusPoint = new Vector3(transform.position.x, baseY + 0.96f, transform.position.z);
            camOffset = new Vector3(-0.56f, 0.54f, -1.48f);
        }

        private void StartSmoothCameraToAnvil(GameObject interactor)
        {
            StartStationCamera(interactor);
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
            GetComponent<ForjaGame.Multiplayer.NetworkForgeStationBridge>()?.ReleaseStation();

            if (stationUI != null && stationUI != gameObject && !stationUI.transform.IsChildOf(transform))
            {
                var panel = stationUI.GetComponent<UI.Core.IUIPanel>();
                if (panel != null && panel.IsOpen)
                    panel.Close();
                else
                    stationUI.SetActive(false);
            }

            GameStateController.ChangeState(GameState.Exploration);

            OnStationExited(previousInteractor);
        }
    }
}
