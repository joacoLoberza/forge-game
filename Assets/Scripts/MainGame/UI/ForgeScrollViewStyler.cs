using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Shared utility to ensure uniform item row styling across all forge panels.
    /// Strictly preserves user-defined ScrollRect and Content positions and bounds.
    /// </summary>
    public static class ForgeScrollViewStyler
    {
        public const float StandardRowHeight = 44f;
        public const float StandardSpacing = 8f;
        public const int StandardPadding = 6;

        public static void ConfigureFixedScrollView(Transform content)
        {
            if (content == null) return;

            // 1. Vertical-only ScrollRect setup (never modify position, anchors, or sizeDelta!)
            var scrollRect = content.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                if (scrollRect.horizontalScrollbar != null)
                    scrollRect.horizontalScrollbar.gameObject.SetActive(false);
            }

            // 2. Configure layout group without altering parent/container anchors
            var vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = StandardSpacing;
            vlg.padding = new RectOffset(StandardPadding, StandardPadding, StandardPadding, StandardPadding);

            var csf = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        public static GameObject CreateRowEntry(Transform container, string title, Sprite icon, Color fallbackColor, bool isSelected, UnityEngine.Events.UnityAction onClick)
        {
            GameObject entryObj = new GameObject("Entry_" + title, typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
            entryObj.transform.SetParent(container, false);

            var layout = entryObj.GetComponent<LayoutElement>();
            layout.minHeight = StandardRowHeight;
            layout.preferredHeight = StandardRowHeight;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1f;

            var btn = entryObj.GetComponent<Button>();
            var bg = entryObj.GetComponent<Image>();
            bg.color = isSelected ? new Color(0.35f, 0.28f, 0.12f, 0.9f) : new Color(0.18f, 0.19f, 0.22f, 0.85f);
            btn.targetGraphic = bg;

            // Icon on the left
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(entryObj.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0.5f);
            iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(8, 0);
            iconRt.sizeDelta = new Vector2(34, 34);

            var iconImg = iconObj.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.sprite = icon;
            iconImg.color = icon != null ? Color.white : fallbackColor;

            // Text on the right
            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(entryObj.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(50, 0);
            textRt.offsetMax = new Vector2(-8, 0);

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = isSelected ? new Color(1.0f, 0.85f, 0.25f, 1f) : new Color(0.95f, 0.93f, 0.88f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.text = title;

            btn.onClick.AddListener(onClick);
            return entryObj;
        }

        public static GameObject CreateNoticeEntry(Transform container, string noticeText)
        {
            GameObject noticeObj = new GameObject("Notice", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            noticeObj.transform.SetParent(container, false);

            var layout = noticeObj.GetComponent<LayoutElement>();
            layout.minHeight = 50;
            layout.preferredHeight = 60;
            layout.flexibleWidth = 1f;

            var tmp = noticeObj.GetComponent<TextMeshProUGUI>();
            tmp.text = noticeText;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Italic;
            tmp.color = new Color(0.75f, 0.73f, 0.70f, 0.9f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            return noticeObj;
        }
    }
}
