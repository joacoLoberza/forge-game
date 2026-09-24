using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using ForjaGame.UI.Core;
using ForjaGame.Audio;

namespace ForjaGame.UI
{
    public class PauseMenuPanel : UIPanelBase
    {
        public static PauseMenuPanel Instance { get; private set; }

        [SerializeField] private Slider volumeSlider;

        [Tooltip("Assign any component implementing IVolumeController (e.g. SimpleVolumeController).")]
        [SerializeField] private MonoBehaviour volumeControllerSource;

        [Header("Navigation")]
        [Tooltip("Main menu scene name.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private IVolumeController _volumeController;

        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            _volumeController = volumeControllerSource as IVolumeController;

            if (volumeSlider != null)
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            Time.timeScale = 1f;

            if (!IsAnyPanelOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnVolumeChanged(float value)
        {
            if (_volumeController != null)
                _volumeController.Volume = value;
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}