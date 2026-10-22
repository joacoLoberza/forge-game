using UnityEngine;
using ForjaGame.Interaction;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Estación de forja física en el escenario.
    /// Preparada para Multiplayer: rastrea ocupación y referencia al jugador que interactúa.
    /// </summary>
    public class ForgeStationController : MonoBehaviour, IInteractable
    {
        [Tooltip("Componente que implementa IForgeOpener, normalmente MenuCommandRouter.")]
        [SerializeField] private MonoBehaviour forgeOpenerSource;

        private IForgeOpener _forgeOpener;
        private GameObject _currentInteractor;
        private bool _isOccupied;

        public bool IsOccupied => _isOccupied;
        public GameObject CurrentInteractor => _currentInteractor;
        public string InteractionPrompt => "Abrir Forja";

        private void Awake()
        {
            _forgeOpener = forgeOpenerSource as IForgeOpener;
        }

        public bool CanInteract(GameObject interactor)
        {
            // En multiplayer, solo permite interactuar si la estación no está ocupada por otro jugador
            if (_isOccupied && _currentInteractor != null && _currentInteractor != interactor)
                return false;

            return _forgeOpener != null;
        }

        public void Interact(GameObject interactor)
        {
            _currentInteractor = interactor;
            _isOccupied = true;
            _forgeOpener?.OpenForge();
        }

        public void ReleaseStation()
        {
            _currentInteractor = null;
            _isOccupied = false;
        }
    }
}