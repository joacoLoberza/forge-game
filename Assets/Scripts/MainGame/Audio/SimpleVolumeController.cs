using UnityEngine;
using UnityEngine.UI;

namespace ForjaGame.Audio
{
    /// <summary>
    /// Implementación de prueba pedida por el checkpoint: modifica un valor
    /// visible, sin controlar audio real todavía.
    /// Si usan TextMeshPro, cambien "Text" por "TMPro.TMP_Text" (using TMPro;).
    /// </summary>
    public class SimpleVolumeController : MonoBehaviour, IVolumeController
    {
        [SerializeField] private Text valueLabel;

        private float _volume = 1f;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                if (valueLabel != null)
                    valueLabel.text = $"{Mathf.RoundToInt(_volume * 100)}%";
            }
        }
    }
}
