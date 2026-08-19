using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ForjaGame.UI.Core;

namespace ForjaGame.UI
{
    /// <summary>
    /// Panel de Forja. Muestra botones para cada zona de trabajo de forma LINEAL
    /// (Horno -> Prensa -> Conformación) y actualiza un texto con el estado.
    ///
    /// Nada de esto usa cercanía/mirada ni mueve cámara: eso se agrega después 
    /// implementando nuevas clases, sin romper esta (OCP).
    /// </summary>
    public class ForgeMenuPanel : UIPanelBase
    {
        [SerializeField] private ForgeZoneOption[] zones;
        [SerializeField] private Text statusLabel;

        [Tooltip("Botón de prueba para simular que terminaste el minijuego de la fase actual.")]
        [SerializeField] private Button debugAdvanceButton;

        private int _currentPhaseIndex = 0;
        private bool _isResetting = false;

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < zones.Length; i++)
            {
                int capturedIndex = i; // Evita el bug de closures compartidas en loops
                if (zones[capturedIndex].button != null)
                {
                    // Usamos el índice para saber en qué fase hizo clic el jugador
                    zones[capturedIndex].button.onClick.AddListener(() => SelectZone(capturedIndex));
                }
            }

            if (debugAdvanceButton != null)
                debugAdvanceButton.onClick.AddListener(AdvancePhase);
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            
            // Al abrir el menú, actualizamos los botones según la fase en la que vamos
            if (!_isResetting) 
                UpdateVisuals();
        }

        private void SelectZone(int index)
        {
            if (_isResetting) return; // Bloquea clics durante el mensaje final

            if (index == _currentPhaseIndex)
            {
                statusLabel.text = $"Trabajando en: {zones[index].zoneName}...";

                // TODO (checkpoint 17/9): acá se dispararía el movimiento de
                // cámara con Cinemachine hacia la zona seleccionada.
            }
            else if (index < _currentPhaseIndex)
            {
                statusLabel.text = $"La fase {zones[index].zoneName} ya está completada.";
            }
        }

        /// <summary>
        /// Se llama cuando el jugador termina la tarea de la zona actual.
        /// </summary>
        public void AdvancePhase()
        {
            if (_isResetting) return;

            if (_currentPhaseIndex < zones.Length - 1)
            {
                _currentPhaseIndex++; // Avanza a la siguiente zona
                UpdateVisuals();
                statusLabel.text = $"¡Fase completada! Siguiente: {zones[_currentPhaseIndex].zoneName}";
            }
            else
            {
                // Si llegamos a la última zona, disparamos el final
                StartCoroutine(FinishAndResetRoutine());
            }
        }

        private IEnumerator FinishAndResetRoutine()
        {
            _isResetting = true;

            // 1. Mensaje de éxito
            if (statusLabel != null)
                statusLabel.text = "¡Arma conformada exitosamente!";

            // Apagamos visualmente las opciones mientras el jugador lee
            foreach (var z in zones)
            {
                if (z.highlight != null) z.highlight.SetActive(false);
                if (z.button != null) z.button.interactable = false;
            }

            // 2. Esperamos 2.5 segundos
            yield return new WaitForSeconds(2.5f);

            // 3. Reiniciamos el menú al estado inicial (Horno)
            _currentPhaseIndex = 0;
            _isResetting = false;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            for (int i = 0; i < zones.Length; i++)
            {
                // El resaltado solo se activa en la fase actual
                if (zones[i].highlight != null) 
                    zones[i].highlight.SetActive(i == _currentPhaseIndex);

                // Bloquea los botones de las fases futuras para que sea lineal
                if (zones[i].button != null)
                    zones[i].button.interactable = (i <= _currentPhaseIndex);
            }

            if (statusLabel != null && !_isResetting)
                statusLabel.text = $"Fase Actual: {zones[_currentPhaseIndex].zoneName}";
        }
    }
}