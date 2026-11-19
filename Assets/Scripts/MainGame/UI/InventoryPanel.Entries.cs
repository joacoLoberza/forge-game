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
            Transform targetContent = ForgeScrollViewStyler.ResolveContentContainer(leftItemsContainer);
            GameObject entryObj = null;
            if (itemEntryPrefab != null)
            {
                entryObj = Instantiate(itemEntryPrefab, targetContent);
            }
            else
            {
                entryObj = new GameObject("Entry_" + title, typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
                entryObj.transform.SetParent(targetContent, false);

                var layout = entryObj.GetComponent<LayoutElement>();
                layout.minHeight = 48f;
                layout.preferredHeight = 48f;
                layout.flexibleWidth = 1f;

                var bg = entryObj.GetComponent<Image>();
                bg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.92f, 0.86f, 0.75f, 0.98f);

                // Slot contenedor de cuero repujado para el icono (36x36)
                GameObject slotObj = new GameObject("IconSlot", typeof(RectTransform), typeof(Image));
                slotObj.transform.SetParent(entryObj.transform, false);
                var slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.anchorMin = new Vector2(0, 0.5f);
                slotRt.anchorMax = new Vector2(0, 0.5f);
                slotRt.pivot = new Vector2(0, 0.5f);
                slotRt.anchoredPosition = new Vector2(10, 0);
                slotRt.sizeDelta = new Vector2(36, 36);

                var slotImg = slotObj.GetComponent<Image>();
                slotImg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
                slotImg.type = Image.Type.Sliced;
                slotImg.color = new Color(0.22f, 0.15f, 0.10f, 0.95f);
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

                // Texto del item (desplazado a x=62 para despejar el slot de icono)
                GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(entryObj.transform, false);
                var textRt = textObj.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(54, 2);
                textRt.offsetMax = new Vector2(-10, -2);

                var tmp = textObj.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 15;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = ForgeCanvasThemeStyler.DarkInk;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 10;
                tmp.fontSizeMax = 15;
                tmp.raycastTarget = false;
            }

            var entryUI = entryObj.GetComponent<InventoryItemEntryUI>();
            if (entryUI == null) entryUI = entryObj.AddComponent<InventoryItemEntryUI>();
            bool isSelected = IsSameItem(_selectedItem, itemData);
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
            ForjaGame.Audio.ForgeAudioSystem.PlayItemSelect();
            _selectedItem = item;
            for (int i = 0; i < _activeEntries.Count; i++)
            {
                _activeEntries[i].ui.SetSelected(IsSameItem(_activeEntries[i].item, item));
            }
            RenderRightPage(item);
            if (rightScrollRect != null) rightScrollRect.verticalNormalizedPosition = 1f;
        }

        private void EnsureSceneReferencesFallback()
        {
            if (tabMineralsButton == null || tabBloomsButton == null || tabIngotsButton == null || tabHandlesButton == null || tabWeaponsButton == null || closeButton == null)
            {
                foreach (var b in GetComponentsInChildren<Button>(true))
                {
                    string n = b.name.ToLowerInvariant();
                    if (tabMineralsButton == null && (n.Contains("mineral") || n.Contains("raw"))) tabMineralsButton = b;
                    else if (tabBloomsButton == null && n.Contains("bloom")) tabBloomsButton = b;
                    else if (tabIngotsButton == null && n.Contains("ingot")) tabIngotsButton = b;
                    else if (tabHandlesButton == null && n.Contains("handle")) tabHandlesButton = b;
                    else if (tabWeaponsButton == null && n.Contains("weapon")) tabWeaponsButton = b;
                    else if (closeButton == null && (n.Contains("close") || n.Contains("cerrar"))) closeButton = b;
                }
            }
            if (leftItemsContainer == null || leftScrollRect == null || rightScrollRect == null)
            {
                Transform lp = transform.Find("LeftPage"), rp = transform.Find("RightPage");
                if (leftScrollRect == null && lp != null) leftScrollRect = lp.GetComponentInChildren<ScrollRect>(true);
                if (leftItemsContainer == null && leftScrollRect != null) leftItemsContainer = leftScrollRect.content;
                if (rightScrollRect == null && rp != null) rightScrollRect = rp.GetComponentInChildren<ScrollRect>(true);
            }
            if (rightItemTitleText == null || rightItemCategoryText == null || rightItemDescriptionText == null || leftPageTitleText == null)
            {
                foreach (var t in GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.GetComponentInParent<Button>(true) != null) continue;
                    string n = t.name.ToLowerInvariant();
                    if (leftPageTitleText == null && n.Contains("left") && n.Contains("title")) leftPageTitleText = t;
                    else if (rightItemTitleText == null && (n.Contains("itemtitle") || (n.Contains("right") && n.Contains("title")))) rightItemTitleText = t;
                    else if (rightItemCategoryText == null && n.Contains("category")) rightItemCategoryText = t;
                    else if (rightItemDescriptionText == null && n.Contains("desc")) rightItemDescriptionText = t;
                }
            }
        }
    }
}
