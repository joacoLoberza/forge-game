using UnityEngine;
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
    public class MenuCommandRouter : MonoBehaviour, IMenuCommandHandler
    {
        [Tooltip("Debe implementar IUIPanel (ej: PauseMenuPanel).")]
        [SerializeField] private MonoBehaviour pausePanelSource;

        [Tooltip("Debe implementar IUIPanel (ej: ForgeMenuPanel).")]
        [SerializeField] private MonoBehaviour forgePanelSource;

        private IUIPanel _pausePanel;
        private IUIPanel _forgePanel;

        private void Awake()
        {
            _pausePanel = pausePanelSource as IUIPanel;
            _forgePanel = forgePanelSource as IUIPanel;
        }

        public void TogglePause() => _pausePanel?.Toggle();
        public void ToggleForge() => _forgePanel?.Toggle();
    }
}
