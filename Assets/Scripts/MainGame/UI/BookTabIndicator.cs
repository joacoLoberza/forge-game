using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Indicador visual tipo marcapáginas / lengüeta de libro para las pestañas del inventario.
    /// Al activarse, sobresale del borde superior del libro con su color distintivo y símbolo de capítulo.
    /// </summary>
    public class BookTabIndicator : MonoBehaviour
    {
        [Header("Configuración del Marcapáginas")]
        [SerializeField] private Color ribbonColor = new Color(0.75f, 0.20f, 0.18f);
        [SerializeField] private Color inactiveColor = new Color(0.38f, 0.28f, 0.22f);
        [SerializeField] private string chapterSymbol = "•";
        [SerializeField] private float activeYOffset = 10f;

        private RectTransform _rectTransform;
        private Image _ribbonImage;
        private TMP_Text _labelText;
        private Button _button;
        private bool _isInitialized;
        private bool _isActive;

        private void Awake() => Initialize();

        public void Setup(Color activeColor, Color mutedColor, string symbol, float yOffset = 10f)
        {
            ribbonColor = activeColor;
            inactiveColor = mutedColor;
            chapterSymbol = "•";
            activeYOffset = yOffset;
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;
            _rectTransform = GetComponent<RectTransform>();
            _ribbonImage = GetComponent<Image>();
            _labelText = GetComponentInChildren<TMP_Text>(true);
            _button = GetComponent<Button>();

            if (_button != null)
            {
                _button.transition = Selectable.Transition.None;
            }

            if (_labelText != null && !string.IsNullOrEmpty(_labelText.text))
            {
                _labelText.text = CleanSymbols(_labelText.text);
            }
            if (string.IsNullOrEmpty(chapterSymbol) || chapterSymbol.Contains("\u25C6") || chapterSymbol.Contains("\u2756") || chapterSymbol.Contains("◆") || chapterSymbol.Contains("❖"))
            {
                chapterSymbol = "•";
            }

            _isInitialized = true;
        }

        private static string CleanSymbols(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsSurrogate(c) || c == '\u25C6' || c == '\u2756' || c == '❖' || c == '◆' || c == '▲' || c == '■' || c == '⚔') continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public void SetIndicatorActive(bool active)
        {
            Initialize();
            _isActive = active;

            if (_rectTransform != null)
            {
                _rectTransform.sizeDelta = new Vector2(active ? 78f : 73f, active ? 40f + activeYOffset : 38f);
            }

            if (_ribbonImage != null)
            {
                _ribbonImage.color = active ? Color.Lerp(ribbonColor, Color.white, 0.16f) : inactiveColor;
                _ribbonImage.raycastTarget = true;
            }

            if (_labelText != null)
            {
                if (string.IsNullOrEmpty(chapterSymbol) || chapterSymbol.Contains("\u25C6") || chapterSymbol.Contains("\u2756") || chapterSymbol.Contains("◆") || chapterSymbol.Contains("❖"))
                    chapterSymbol = "•";

                string raw = CleanSymbols(_labelText.text).Replace("•", "").Trim();
                _labelText.margin = new Vector4(3f, active ? 5f : 5f, 3f, 12f);
                _labelText.text = active ? $"{chapterSymbol} {raw}" : raw;
                _labelText.color = active ? ForgeCanvasThemeStyler.GoldTitleInk : new Color(0.86f, 0.80f, 0.70f);
                _labelText.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                _labelText.raycastTarget = false;
            }
        }
    }
}
