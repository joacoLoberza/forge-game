using UnityEngine;

namespace ForjaGame.UI.Core
{
    /// <summary>
    /// Clase base para todos los paneles de UI.
    ///
    /// - SRP: su única responsabilidad es activar/desactivar el GameObject
    ///   raíz del panel.
    /// - OCP: las subclases (PauseMenuPanel, ForgeMenuPanel, InventoryPanel...)
    ///   agregan su lógica particular sobreescribiendo OnOpened()/OnClosed(),
    ///   sin tener que tocar ni duplicar el Open()/Close()/Toggle() de acá
    ///   (patrón Template Method).
    /// </summary>
    public abstract class UIPanelBase : MonoBehaviour, IUIPanel
    {
        [Tooltip("GameObject que se activa/desactiva. Si se deja vacío, se usa este mismo objeto.")]
        [SerializeField] protected GameObject root;

        public bool IsOpen => root != null && root.activeSelf;

        protected virtual void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen) return;
            root.SetActive(true);
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            OnClosed();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Hook para lógica extra al abrir (ej: pausar el juego).</summary>
        protected virtual void OnOpened() { }

        /// <summary>Hook para lógica extra al cerrar.</summary>
        protected virtual void OnClosed() { }
    }
}
