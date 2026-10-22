using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Interaction;
using ForjaGame.UI.Core;

namespace ForjaGame.Input
{
    /// <summary>
    /// Único punto del proyecto que conoce los paneles concretos y decide
    /// abrirlos/cerrarlos. Si mañana agregan un panel nuevo (ej: crafteo),
    /// agregan UN método acá -sin tocar el código que lee el input, y sin
    /// tocar los paneles existentes- (SRP: "una clase, una responsabilidad";
    /// OCP: se extiende agregando, no modificando).
    ///
    /// Para multiplayer local: puede haber UNA instancia compartida (si el
    /// juego es de pantalla compartida sin splitscreen, cualquier jugador
    /// puede pausar/abrir la Forja) o una por jugador si cada uno tiene su
    /// propio HUD. La clase no cambia en ninguno de los dos casos.
    /// </summary>
    public class MenuCommandRouter : MonoBehaviour, IMenuCommandHandler, IForgeOpener
    {
        [Tooltip("Debe implementar IUIPanel (ej: PauseMenuPanel).")]
        [SerializeField] private MonoBehaviour pausePanelSource;

        [Tooltip("Debe implementar IUIPanel (ej: InventoryPanel).")]
        [SerializeField] private MonoBehaviour inventoryPanelSource;

        [Tooltip("Controlador global de estados de la partida.")]
        [SerializeField] private GameStateController gameStateController;

        private IUIPanel _pausePanel;
        private IUIPanel _inventoryPanel;

        public bool IsMovementBlocked =>
            (_pausePanel != null && _pausePanel.IsOpen) ||
            (_inventoryPanel != null && _inventoryPanel.IsOpen);

        private void Awake()
        {
            _pausePanel = pausePanelSource as IUIPanel;
            _inventoryPanel = inventoryPanelSource as IUIPanel;

            if (gameStateController == null)
                gameStateController = FindFirstObjectByType<GameStateController>();
        }

        public void TogglePause()
        {
            if (_pausePanel == null)
                return;

            if (_pausePanel.IsOpen)
            {
                _pausePanel.Close();
                gameStateController?.TryChangeState(GameState.Exploration);
                return;
            }

            if (gameStateController == null || gameStateController.TryChangeState(GameState.Pause))
            {
                CloseOtherPanels(_pausePanel);
                _pausePanel.Open();
            }
        }

        public void ToggleForge()
        {
            // Las estaciones de forja ahora gestionan sus propios paneles directamente
        }

        public void OpenForge()
        {
            // Las estaciones de forja ahora gestionan sus propios paneles directamente
        }

        public void ToggleInventory()
        {
            if (_inventoryPanel == null)
            {
                _inventoryPanel = UI.InventoryPanel.Instance ?? FindFirstObjectByType<UI.InventoryPanel>(FindObjectsInactive.Include);
            }

            if (_inventoryPanel == null || (_pausePanel != null && _pausePanel.IsOpen))
                return;

            if (_inventoryPanel.IsOpen)
            {
                _inventoryPanel.Close();
                gameStateController?.TryChangeState(GameState.Exploration);
                return;
            }

            CloseOtherPanels(_inventoryPanel);
            _inventoryPanel.Open();
            gameStateController?.TryChangeState(GameState.Inventory);
        }

        private bool IsAnyStationOccupied()
        {
            return ForjaGame.Forge.BaseForgeStationController.IsAnyStationOccupied();
        }

        private void CloseOtherPanels(IUIPanel panelToKeepOpen)
        {
            if (_pausePanel != panelToKeepOpen)
                _pausePanel?.Close();

            if (_inventoryPanel != panelToKeepOpen)
                _inventoryPanel?.Close();
        }
    }
}
