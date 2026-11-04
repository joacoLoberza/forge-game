using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Core;
using ForjaGame.UI;
using ForjaGame.Inventory;
using ForjaGame.Combat;

namespace ForjaGame.Input
{
    /// <summary>
    /// Componente de entrada de menú por jugador.
    /// Soporta eventos individuales desde PlayerInput para Local Multiplayer (gamepads o teclados divididos).
    /// </summary>
    public class PlayerMenuInput : MonoBehaviour
    {
        [Tooltip("Debe implementar IMenuCommandHandler (ej: MenuCommandRouter).")]
        [SerializeField] private MonoBehaviour commandHandlerSource;

        [Tooltip("Movimiento de este jugador. Si se deja vacío, se busca en este mismo objeto.")]
        [SerializeField] private PlayerMovement playerMovement;

        private IMenuCommandHandler _handler;
        private PlayerInput _playerInput;
        private static int _lastInventoryFrame = -1;
        private static int _lastPauseFrame = -1;
        private static float _lastInventoryTime = -1f;
        private static float _lastPauseTime = -1f;

        private void Awake()
        {
            _handler = commandHandlerSource as IMenuCommandHandler;
            _playerInput = GetComponent<PlayerInput>();

            if (_handler == null)
            {
                _handler = FindFirstObjectByType<MenuCommandRouter>();
            }

            if (playerMovement == null)
                playerMovement = GetComponentInParent<PlayerMovement>() ?? GetComponentInChildren<PlayerMovement>() ?? FindFirstObjectByType<PlayerMovement>();

            UpdateMovementAvailability();
        }

        private void Update()
        {
            // Solo polling de teclado si este componente no tiene PlayerInput asignado
            if (_playerInput == null)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.eKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)
                    {
                        RequestInventory();
                    }
                    else if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
                    {
                        RequestPause();
                    }
                }
            }

            UpdateMovementAvailability();
        }

        public void OnPause(InputAction.CallbackContext context)
        {
            if (!context.started) return;
            RequestPause();
        }

        public void OnPause(InputValue value)
        {
            if (value.isPressed) RequestPause();
        }

        public void OnForge(InputAction.CallbackContext context)
        {
            if (!context.started) return;
            RequestForge();
        }

        public void OnForge(InputValue value)
        {
            if (value.isPressed) RequestForge();
        }

        public void OnInventory(InputAction.CallbackContext context)
        {
            if (!context.started) return;
            RequestInventory();
        }

        public void OnInventory(InputValue value)
        {
            if (value.isPressed) RequestInventory();
        }

        public void RequestPause()
        {
            if (Time.frameCount == _lastPauseFrame) return;
            if (Time.unscaledTime - _lastPauseTime < 0.2f) return;
            _lastPauseFrame = Time.frameCount;
            _lastPauseTime = Time.unscaledTime;

            // 1. Si el menú de pausa ya está abierto, cerrarlo y reanudar el juego
            if (PauseMenuPanel.Instance != null && PauseMenuPanel.Instance.IsOpen)
            {
                PauseMenuPanel.Instance.Close();
                UpdateMovementAvailability();
                GameStateController.ChangeState(GameState.Exploration);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            // 2. Si hay cualquier otro panel abierto (ej: inventario, etc.), cerrarlo y volver a exploración
            if (UI.Core.UIPanelBase.IsAnyPanelOpen)
            {
                UI.Core.UIPanelBase.CloseAllPanels();
                UpdateMovementAvailability();
                GameStateController.ChangeState(GameState.Exploration);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            // 3. Si no hay paneles abiertos, abrir el menú de pausa
            var pause = PauseMenuPanel.Instance ?? FindFirstObjectByType<PauseMenuPanel>(FindObjectsInactive.Include);
            if (pause != null)
            {
                pause.Open();
            }
            else
            {
                if (_handler == null)
                    _handler = commandHandlerSource as IMenuCommandHandler ?? FindFirstObjectByType<MenuCommandRouter>();

                _handler?.TogglePause();
            }

            UpdateMovementAvailability();
        }

        public void RequestForge()
        {
            _handler?.ToggleForge();
            UpdateMovementAvailability();
        }

        public void RequestInventory()
        {
            if (Time.frameCount == _lastInventoryFrame) return;
            if (Time.unscaledTime - _lastInventoryTime < 0.2f) return;
            _lastInventoryFrame = Time.frameCount;
            _lastInventoryTime = Time.unscaledTime;

            // No abrir inventario si el menú de pausa está activo
            if (PauseMenuPanel.Instance != null && PauseMenuPanel.Instance.IsOpen) return;

            var inv = UI.InventoryPanel.Instance ?? FindFirstObjectByType<UI.InventoryPanel>(FindObjectsInactive.Include);
            if (inv != null)
            {
                var myInv = GetComponentInParent<InventoryController>() ?? GetComponentInChildren<InventoryController>();
                var myEquip = GetComponentInParent<PlayerEquipmentController>() ?? GetComponentInChildren<PlayerEquipmentController>();
                if (myInv != null)
                {
                    inv.OpenFor(myInv, myEquip);
                }
                else
                {
                    inv.Toggle();
                }
            }
            else
            {
                if (_handler == null)
                    _handler = commandHandlerSource as IMenuCommandHandler ?? FindFirstObjectByType<MenuCommandRouter>();

                _handler?.ToggleInventory();
            }

            UpdateMovementAvailability();
        }

        public void ResetMenuInputState()
        {
            UpdateMovementAvailability();
        }

        private bool _lastUiBlocked = false;

        public void UpdateMovementAvailability()
        {
            bool isForging = ForjaGame.Core.GameStateController.IsForging;
            bool uiBlocked = UI.Core.UIPanelBase.IsAnyPanelOpen || isForging || (_handler != null && _handler.IsMovementBlocked);
            if (uiBlocked == _lastUiBlocked && playerMovement != null) return;
            _lastUiBlocked = uiBlocked;
            if (playerMovement == null)
                playerMovement = GetComponentInParent<PlayerMovement>() ?? GetComponentInChildren<PlayerMovement>() ?? PlayerMovement.LocalInstance;
            playerMovement?.SetMovementEnabled(!uiBlocked);
        }
    }
}