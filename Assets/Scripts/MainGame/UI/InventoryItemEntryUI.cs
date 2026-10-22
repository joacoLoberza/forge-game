using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Individual item entry in the inventory journal list.
    /// Displays item title and representative sketch icon in journal style.
    /// </summary>
    public class InventoryItemEntryUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image selectionHighlight;
        [SerializeField] private Image backgroundImage;

        [Header("Journal Colors")]
        [SerializeField] private Color normalColor = new Color(0.92f, 0.90f, 0.86f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.25f, 1f);

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
                    if (images[i].gameObject.name.Contains("Icon")) { iconImage = images[i]; break; }
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
                rt.anchoredPosition = new Vector2(8, 0);
                rt.sizeDelta = new Vector2(34, 34);

                iconImage = iconObj.GetComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            // Always ensure the label is cleanly positioned to the right of the icon
            if (labelText != null)
            {
                var lrt = labelText.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(48, 0);
                lrt.offsetMax = new Vector2(-8, 0);
            }
        }

        public void Setup(string title, Action onClick, bool isSelected = false, Sprite icon = null, Color? iconColor = null)
        {
            _onClickAction = onClick;
            EnsureReferences();

            if (labelText != null) labelText.text = title;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.color = icon != null ? Color.white : (iconColor ?? Color.gray);
            }

            SetSelected(isSelected);
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            EnsureReferences();

            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected
                    ? new Color(0.35f, 0.28f, 0.12f, 0.9f)
                    : new Color(0.18f, 0.19f, 0.22f, 0.85f);
            }

            if (selectionHighlight != null)
                selectionHighlight.gameObject.SetActive(isSelected);

            if (labelText != null)
            {
                labelText.color = isSelected ? selectedColor : normalColor;
                labelText.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
