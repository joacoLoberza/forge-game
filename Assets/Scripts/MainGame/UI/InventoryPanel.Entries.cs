using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    public partial class InventoryPanel
    {
        private void CreateItemEntry(string title, object itemData, System.Action onClick, Sprite icon = null, Color? iconColor = null)
        {
            GameObject entryObj = null;
            if (itemEntryPrefab != null)
            {
                entryObj = Instantiate(itemEntryPrefab, leftItemsContainer);
            }
            else
            {
                entryObj = new GameObject("Entry_" + title, typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
                entryObj.transform.SetParent(leftItemsContainer, false);

                var layout = entryObj.GetComponent<LayoutElement>();
                layout.minHeight = 44f;
                layout.preferredHeight = 44f;
                layout.flexibleWidth = 1f;

                var bg = entryObj.GetComponent<Image>();
                bg.color = new Color(0.18f, 0.19f, 0.22f, 0.85f);

                // Slot contenedor oscuro para el icono (34x34)
                GameObject slotObj = new GameObject("IconSlot", typeof(RectTransform), typeof(Image));
                slotObj.transform.SetParent(entryObj.transform, false);
                var slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.anchorMin = new Vector2(0, 0.5f);
                slotRt.anchorMax = new Vector2(0, 0.5f);
                slotRt.pivot = new Vector2(0, 0.5f);
                slotRt.anchoredPosition = new Vector2(22, 0);
                slotRt.sizeDelta = new Vector2(34, 34);

                var slotImg = slotObj.GetComponent<Image>();
                slotImg.color = new Color(0.10f, 0.11f, 0.14f, 0.95f);
                slotImg.raycastTarget = false;

                // Icono visible
                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(slotObj.transform, false);
                var iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.sizeDelta = new Vector2(-6, -6);
                iconRt.anchoredPosition = Vector2.zero;

                var iconImg = iconObj.GetComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;

                // Texto del item
                GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(entryObj.transform, false);
                var textRt = textObj.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(48, 0);
                textRt.offsetMax = new Vector2(-8, 0);

                var tmp = textObj.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 15;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = new Color(0.92f, 0.90f, 0.86f, 1f);
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;
            }

            var entryUI = entryObj.GetComponent<InventoryItemEntryUI>() ?? entryObj.AddComponent<InventoryItemEntryUI>();
            bool isSelected = _selectedItem == itemData;
            entryUI.Setup(title, onClick, isSelected, icon, iconColor);
            _activeEntries.Add((itemData, entryUI));
        }

        private Color GetMineralColor(string materialId)
        {
            return materialId.ToLowerInvariant() switch
            {
                var s when s.Contains("hierro") || s.Contains("iron") => new Color(0.78f, 0.82f, 0.88f),
                var s when s.Contains("cobre") || s.Contains("copper") => new Color(0.88f, 0.52f, 0.32f),
                var s when s.Contains("oro") || s.Contains("gold") => new Color(0.98f, 0.82f, 0.22f),
                var s when s.Contains("carbon") || s.Contains("coal") => new Color(0.22f, 0.22f, 0.24f),
                var s when s.Contains("estano") || s.Contains("tin") => new Color(0.70f, 0.75f, 0.72f),
                _ => new Color(0.60f, 0.60f, 0.65f)
            };
        }

        private void SelectItem(object item)
        {
            _selectedItem = item;
            for (int i = 0; i < _activeEntries.Count; i++)
            {
                _activeEntries[i].ui.SetSelected(_activeEntries[i].item == item);
            }
            RenderRightPage(item);
            if (rightScrollRect != null) rightScrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
