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
            {
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
                if (_volumeController != null) _volumeController.Volume = volumeSlider.value;
            }

            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string n = btn.gameObject.name.ToLowerInvariant();
                if (n.Contains("resume") || n.Contains("reanudar") || n.Contains("continuar") || n.Contains("volver"))
                {
                    btn.onClick.RemoveListener(ResumeGame);
                    btn.onClick.AddListener(ResumeGame);
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            BookJournalStyler.StylePauseMenuPanel();
            if (volumeSlider != null && _volumeController != null)
                _volumeController.Volume = volumeSlider.value;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            FindFirstObjectByType<ForjaGame.Core.GameStateController>()?.TryChangeState(ForjaGame.Core.GameState.Pause);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            FindFirstObjectByType<ForjaGame.Core.GameStateController>()?.TryChangeState(ForjaGame.Core.GameState.Exploration);

            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            var menuInputs = FindObjectsByType<ForjaGame.Input.PlayerMenuInput>(FindObjectsSortMode.None);
            foreach (var mi in menuInputs) mi?.ResetMenuInputState();

            ForjaGame.Core.PlayerMovement.LocalInstance?.SetMovementEnabled(true);

            if (!IsAnyPanelOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void ResumeGame()
        {
            Close();
        }

        private void OnVolumeChanged(float value)
        {
            if (_volumeController != null)
                _volumeController.Volume = value;
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Unity.Netcode.NetworkManager.Singleton != null)
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}