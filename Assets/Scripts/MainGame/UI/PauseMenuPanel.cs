using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // <-- 1. AÑADIMOS LA LIBRERÍA DE ESCENAS
using ForjaGame.UI.Core;
using ForjaGame.Audio;

namespace ForjaGame.UI
{
    public class PauseMenuPanel : UIPanelBase
    {
        [SerializeField] private Slider volumeSlider;

        [Tooltip("Arrastrar acá cualquier componente que implemente IVolumeController (ej: SimpleVolumeController).")]
        [SerializeField] private MonoBehaviour volumeControllerSource;

        [Header("Navegación")]
        [Tooltip("Nombre de la escena del menú principal.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu"; // <-- 2. VARIABLE PARA LA ESCENA

        private IVolumeController _volumeController;

        protected override void Awake()
        {
            base.Awake();
            _volumeController = volumeControllerSource as IVolumeController;

            if (volumeSlider != null)
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        private void OnVolumeChanged(float value)
        {
            if (_volumeController != null)
                _volumeController.Volume = value;
        }

        protected override void OnOpened()
        {
            // Pausamos el tiempo al abrir
            Time.timeScale = 0f;
        }

        protected override void OnClosed()
        {
            // Restauramos el tiempo al cerrar
            Time.timeScale = 1f;
        }

        // <-- 3. NUEVO MÉTODO PARA EL BOTÓN
        public void GoToMainMenu()
        {
            // Es vital restaurar el tiempo antes de cargar la otra escena, 
            // de lo contrario el Menú Principal cargará congelado.
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}