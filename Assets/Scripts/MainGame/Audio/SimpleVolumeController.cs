using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.Audio
{
    /// <summary>
    /// Controlador de volumen conectado tanto a una etiqueta UI opcional como al ForgeAudioSystem real.
    /// </summary>
    public class SimpleVolumeController : MonoBehaviour, IVolumeController
    {
        [SerializeField] private Text valueLabel;
        [SerializeField] private TMP_Text tmpValueLabel;

        private float _volume = 0.85f;

        private void Awake()
        {
            _volume = ForgeAudioSystem.GetMasterVolume();
            UpdateLabels();
        }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                ForgeAudioSystem.SetMasterVolume(_volume);
                UpdateLabels();
            }
        }

        private void UpdateLabels()
        {
            string txt = $"{Mathf.RoundToInt(_volume * 100)}%";
            if (valueLabel != null) valueLabel.text = txt;
            if (tmpValueLabel != null) tmpValueLabel.text = txt;
        }
    }
}
