using UnityEngine;
using ForjaGame.UI.Core;

namespace ForjaGame.UI
{
    public interface IToggleable
    {
        void Toggle();
    }

    /// <summary>
    /// Controlador simple para alternar la visibilidad de un Canvas o GameObject.
    /// Compatible con el contrato IUIPanel e IToggleable.
    /// </summary>
    public class CanvasController : MonoBehaviour, IUIPanel, IToggleable
    {
        [SerializeField] private GameObject canvasObject;

        public bool IsOpen => canvasObject != null ? canvasObject.activeSelf : gameObject.activeSelf;

        private void Awake()
        {
            if (canvasObject == null)
                canvasObject = gameObject;
        }

        public void Open()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);
        }

        public void Close()
        {
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        public void Toggle()
        {
            if (canvasObject != null)
                canvasObject.SetActive(!canvasObject.activeSelf);
        }
    }
}
