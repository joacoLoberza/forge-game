using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Entrada individual de objeto en el diario de inventario y selectores de forja.
    /// Muestra icono representativo nítido (procedural si no hay sprite) y etiqueta auto-escalable sin solapes.
    /// </summary>
    public class InventoryItemEntryUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image selectionHighlight;
        [SerializeField] private Image backgroundImage;

        private Action _onClickAction;
        private bool _isSelected;

        private void Awake() => EnsureReferences();

        private void EnsureReferences()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClickAction?.Invoke());
            }

            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (labelText == null) labelText = GetComponentInChildren<TMP_Text>(true);

            if (iconImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].gameObject.name.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                    {
                        iconImage = images[i];
                        break;
                    }
                }
                if (iconImage == null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        string n = images[i].gameObject.name;
                        if (n.Contains("Icon") && !n.Contains("Slot") && images[i].gameObject != gameObject)
                        {
                            iconImage = images[i];
                            break;
                        }
                    }
                }
            }

            if (iconImage == null)
            {
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(transform, false);
                var rt = iconObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = new Vector2(10, 0);
                rt.sizeDelta = new Vector2(36, 36);

                iconImage = iconObj.GetComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
            else iconImage.preserveAspect = true;

            if (labelText != null)
            {
                var lrt = labelText.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(58, 2);
                lrt.offsetMax = new Vector2(-10, -2);
                labelText.enableAutoSizing = true;
                labelText.fontSizeMin = 10f;
                labelText.fontSizeMax = 14.5f;
                labelText.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        public void Setup(string title, Action onClick, bool isSelected = false, Sprite icon = null, Color? iconColor = null)
        {
            _onClickAction = onClick;
            EnsureReferences();

            if (labelText != null) labelText.text = title;

            if (iconImage != null)
            {
                Sprite resolvedSprite = icon != null ? icon : ProceduralItemIconProvider.GetItemIcon(title);
                iconImage.sprite = resolvedSprite;
                iconImage.color = iconColor ?? Color.white;
                iconImage.gameObject.SetActive(true);
            }

            SetSelected(isSelected);
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            EnsureReferences();

            if (backgroundImage != null)
            {
                backgroundImage.sprite = isSelected ? BookTextureBuilder.GetTitlePlaqueSprite() : BookTextureBuilder.GetParchmentBannerSprite();
                backgroundImage.type = Image.Type.Sliced;
                backgroundImage.color = isSelected ? Color.white : new Color(0.92f, 0.86f, 0.75f, 0.98f);
            }

            if (selectionHighlight != null)
                selectionHighlight.gameObject.SetActive(isSelected);

            if (labelText != null)
            {
                MedievalTypographyProvider.ApplyToTMP(labelText);
                labelText.color = isSelected ? ForgeCanvasThemeStyler.GoldTitleInk : ForgeCanvasThemeStyler.DarkInk;
                labelText.fontStyle = FontStyles.Bold;
            }
        }
    }
}
