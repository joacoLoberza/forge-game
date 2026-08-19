using UnityEngine;

namespace ForjaGame.Input
{
    /// <summary>
    /// Un componente de este tipo POR JUGADOR. Su única responsabilidad es
    /// leer input y avisarle a un IMenuCommandHandler (SRP) - no sabe nada
    /// de paneles, sliders ni UI.
    ///
    /// Multiplayer local con teclado (rápido para probar ya):
    /// cada jugador tiene su propio set de teclas en el Inspector (ej.
    /// Jugador 1: Esc/Q, Jugador 2: P/O).
    ///
    /// Multiplayer local "de verdad" (varios gamepads):
    /// cuando migren al nuevo Input System, cada jugador tiene un
    /// PlayerInput (Unity) con "Invoke Unity Events", y sus eventos
    /// OnPause/OnForge llaman a estos mismos métodos públicos de acá abajo.
    /// Ni MenuCommandRouter ni los paneles se enteran del cambio (DIP).
    
    /// Refactorizado para el Nuevo Input System.
    /// Ya no lee las teclas en el Update, sino que espera ser llamado 
    /// externamente (vía PlayerInput y Unity Events).
    /// </summary>
    public class PlayerMenuInput : MonoBehaviour
    {
        [Tooltip("Debe implementar IMenuCommandHandler (ej: MenuCommandRouter).")]
        [SerializeField] private MonoBehaviour commandHandlerSource;

        private IMenuCommandHandler _handler;

        private void Awake()
        {
            _handler = commandHandlerSource as IMenuCommandHandler;
        }

        // Estos métodos serán llamados por los eventos del componente PlayerInput
        public void RequestPause() => _handler?.TogglePause();
        public void RequestForge() => _handler?.ToggleForge();
    }
}