using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Utilidad compartida de estilo medieval (cuero y pergamino) para ScrollViews,
    /// Viewports, Barras de desplazamiento y filas de objetos.
    /// Resuelve automáticamente el contenedor Content real sin destruir Viewports ni Scrollbars.
    /// </summary>
    public static class ForgeScrollViewStyler
    {
        public const float StandardRowHeight = 44f;
        public const float StandardSpacing = 8f;
        public const int StandardPadding = 6;

        public static Transform ResolveContentContainer(Transform container)
        {
            if (container == null) return null;

            var sr = container.GetComponent<ScrollRect>();
            if (sr != null)
            {
                if (sr.content != null) return sr.content;
                var vp = sr.viewport != null ? sr.viewport : container.Find("Viewport");
                if (vp != null)
                {
                    var c = vp.Find("Content");
                    if (c != null)
                    {
                        sr.content = c as RectTransform;
                        return c;
                    }
                }
            }

            var viewportChild = container.Find("Viewport");
            if (viewportChild != null)
            {
                var contentChild = viewportChild.Find("Content");
                if (contentChild != null) return contentChild;
            }

            var directContent = container.Find("Content");
            if (directContent != null) return directContent;

            return container;
        }

        public static void StyleScrollRectVisuals(ScrollRect scrollRect)
        {
            if (scrollRect == null) return;

            if (scrollRect.TryGetComponent<Image>(out var srBg))
            {
                srBg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                srBg.type = Image.Type.Sliced;
                srBg.color = new Color(0.86f, 0.80f, 0.69f, 0.42f);
            }

            if (scrollRect.viewport != null)
            {
                var mask = scrollRect.viewport.GetComponent<Mask>();
                var vpImg = scrollRect.viewport.GetComponent<Image>();
                if (mask != null)
                {
                    mask.showMaskGraphic = false;
                    if (vpImg != null) vpImg.color = Color.white;
                }
                else if (vpImg != null)
                {
                    vpImg.sprite = null;
                    vpImg.color = Color.clear;
                    vpImg.raycastTarget = false;
                }

                if (scrollRect.viewport.GetComponent<RectMask2D>() == null && mask == null)
                    scrollRect.viewport.gameObject.AddComponent<RectMask2D>();
            }

            if (scrollRect.horizontalScrollbar != null)
                scrollRect.horizontalScrollbar.gameObject.SetActive(false);

            if (scrollRect.verticalScrollbar != null)
            {
                Scrollbar sb = scrollRect.verticalScrollbar;
                if (sb.TryGetComponent<Image>(out var trackImg))
                {
                    trackImg.sprite = BookTextureBuilder.GetSliderTrackSprite();
                    trackImg.type = Image.Type.Sliced;
                    trackImg.color = new Color(0.24f, 0.16f, 0.10f, 0.92f);
                }
                if (sb.handleRect != null && sb.handleRect.TryGetComponent<Image>(out var handleImg))
                {
                    handleImg.sprite = BookTextureBuilder.GetTitlePlaqueSprite();
                    handleImg.type = Image.Type.Sliced;
                    handleImg.color = new Color(0.95f, 0.82f, 0.48f, 0.98f);
                }
            }
        }

        public static void ConfigureFixedScrollView(Transform container)
        {
            if (container == null) return;
            Transform content = ResolveContentContainer(container);

            var scrollRect = content.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                StyleScrollRectVisuals(scrollRect);

                if (scrollRect.viewport != null)
                {
                    scrollRect.viewport.anchorMin = Vector2.zero;
                    scrollRect.viewport.anchorMax = Vector2.one;
                    scrollRect.viewport.offsetMin = Vector2.zero;
                    scrollRect.viewport.offsetMax = Vector2.zero;
                    scrollRect.viewport.pivot = new Vector2(0f, 1f);
                }
            }

            if (content is RectTransform contentRt)
            {
                contentRt.anchorMin = new Vector2(0f, 1f);
                contentRt.anchorMax = new Vector2(1f, 1f);
                contentRt.pivot = new Vector2(0.5f, 1f);
            }

            if (!content.TryGetComponent<VerticalLayoutGroup>(out var vlg))
                vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = StandardSpacing;
            vlg.padding = new RectOffset(StandardPadding, StandardPadding, StandardPadding, StandardPadding);

            if (!content.TryGetComponent<ContentSizeFitter>(out var csf))
                csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        public static GameObject CreateRowEntry(
            Transform container, string title, Sprite icon, Color fallbackColor, bool isSelected, UnityEngine.Events.UnityAction onClick)
        {
            Transform targetContent = ResolveContentContainer(container);

            GameObject entryObj = new GameObject("Entry_" + title, typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
            entryObj.transform.SetParent(targetContent, false);

            var layout = entryObj.GetComponent<LayoutElement>();
            layout.minHeight = StandardRowHeight;
            layout.preferredHeight = StandardRowHeight;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1f;

            var btn = entryObj.GetComponent<Button>();
            var bg = entryObj.GetComponent<Image>();
            bg.sprite = isSelected ? BookTextureBuilder.GetTitlePlaqueSprite() : BookTextureBuilder.GetParchmentBannerSprite();
            bg.type = Image.Type.Sliced;
            bg.color = isSelected ? Color.white : new Color(0.92f, 0.86f, 0.75f, 0.98f);
            btn.targetGraphic = bg;

            GameObject slotObj = new GameObject("IconSlot", typeof(RectTransform), typeof(Image));
            slotObj.transform.SetParent(entryObj.transform, false);
            var slotRt = slotObj.GetComponent<RectTransform>();
            slotRt.anchorMin = slotRt.anchorMax = slotRt.pivot = new Vector2(0f, 0.5f);
            slotRt.anchoredPosition = new Vector2(8f, 0f);
            slotRt.sizeDelta = new Vector2(34f, 34f);
            var slotImg = slotObj.GetComponent<Image>();
            slotImg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
            slotImg.type = Image.Type.Sliced;
            slotImg.color = new Color(0.22f, 0.15f, 0.10f, 0.95f);
            slotImg.raycastTarget = false;

            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(slotObj.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(3f, 3f);
            iconRt.offsetMax = new Vector2(-3f, -3f);

            var iconImg = iconObj.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.sprite = icon != null ? icon : ProceduralItemIconProvider.GetItemIcon(title);
            iconImg.color = icon != null ? Color.white : (fallbackColor != Color.clear ? fallbackColor : Color.white);

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(entryObj.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(52f, 2f);
            textRt.offsetMax = new Vector2(-8f, -2f);

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(tmp);
            tmp.fontSize = 15f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 10.5f;
            tmp.fontSizeMax = 15f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = isSelected ? ForgeCanvasThemeStyler.GoldTitleInk : ForgeCanvasThemeStyler.DarkInk;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            tmp.text = title;

            btn.onClick.AddListener(onClick);
            return entryObj;
        }

        public static GameObject CreateNoticeEntry(Transform container, string noticeText)
        {
            Transform targetContent = ResolveContentContainer(container);

            GameObject noticeObj = new GameObject("Notice", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            noticeObj.transform.SetParent(targetContent, false);

            var layout = noticeObj.GetComponent<LayoutElement>();
            layout.minHeight = 50f;
            layout.preferredHeight = 60f;
            layout.flexibleWidth = 1f;

            var tmp = noticeObj.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(tmp);
            tmp.text = noticeText;
            tmp.fontSize = 14.5f;
            tmp.fontStyle = FontStyles.Italic;
            tmp.color = ForgeCanvasThemeStyler.SubInk;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            return noticeObj;
        }
    }
}
