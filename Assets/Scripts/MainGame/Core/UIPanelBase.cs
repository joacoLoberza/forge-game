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
        private static readonly System.Collections.Generic.HashSet<UIPanelBase> _activeOpenPanels = new();

        public static bool IsAnyPanelOpen
        {
            get
            {
                _activeOpenPanels.RemoveWhere(p => p == null || !p.IsOpen);
                return _activeOpenPanels.Count > 0;
            }
        }

        public static void CloseAllPanels()
        {
            var list = new System.Collections.Generic.List<UIPanelBase>(_activeOpenPanels);
            foreach (var panel in list)
            {
                if (panel != null && panel.IsOpen)
                {
                    panel.Close();
                }
            }
            _activeOpenPanels.Clear();
        }

        [Tooltip("GameObject que se activa/desactiva. Si se deja vacío, se usa este mismo objeto.")]
        [SerializeField] protected GameObject root;

        public bool IsOpen => root != null && root.activeSelf;

        protected virtual void Awake()
        {
            if (root == null) root = gameObject;
            root.SetActive(false);
            _activeOpenPanels.Remove(this);
        }

        protected virtual void OnDisable()
        {
            _activeOpenPanels.Remove(this);
        }

        protected virtual void OnDestroy()
        {
            _activeOpenPanels.Remove(this);
        }

        public virtual void Open()
        {
            if (IsOpen) return;
            root.SetActive(true);
            _activeOpenPanels.Add(this);
            OnOpened();
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            _activeOpenPanels.Remove(this);
            OnClosed();
        }

        public virtual void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Hook para lógica extra al abrir (ej: pausar el juego).</summary>
        protected virtual void OnOpened() { }

        /// <summary>Hook para lógica extra al cerrar.</summary>
        protected virtual void OnClosed() { }

        /// <summary>
        /// Vincula una acción a un botón de UI para que se ejecute inmediatamente al presionarlo (PointerDown),
        /// en lugar de tener que soltar el botón para que se active.
        /// </summary>
        protected void BindPressDown(UnityEngine.UI.Button button, System.Action action)
        {
            if (button == null || action == null) return;

            var trigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var entry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown
            };
            entry.callback.AddListener((_) =>
            {
                if (button.interactable) action.Invoke();
            });
            trigger.triggers.Add(entry);
        }
    }
}
