using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    public static partial class BookJournalStyler
    {
        private static void StyleLeftPage(Transform page)
        {
            var rt = page.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-220f, -10f);
                rt.sizeDelta = new Vector2(420f, 470f);
            }

            var img = page.GetComponent<Image>() ?? page.gameObject.AddComponent<Image>();
            img.sprite = BookTextureBuilder.GetParchmentSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.97f, 0.94f, 0.88f, 1f);
            img.raycastTarget = false;

            foreach (var t in page.GetComponentsInChildren<TMP_Text>(true))
            {
                t.margin = Vector4.zero;
                if (t.name.ToLowerInvariant().Contains("title") || t.name.ToLowerInvariant().Contains("header"))
                {
                    var tRt = t.GetComponent<RectTransform>();
                    tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0.5f, 1f);
                    tRt.anchoredPosition = new Vector2(0f, -16f);
                    tRt.sizeDelta = new Vector2(360f, 28f);
                    t.fontSize = 17.5f;
                    t.enableAutoSizing = true;
                    t.fontSizeMin = 13f;
                    t.fontSizeMax = 18f;
                    t.alignment = TextAlignmentOptions.Center;
                    t.fontStyle = FontStyles.Bold;
                    t.raycastTarget = false;
                }
            }

            foreach (var scroll in page.GetComponentsInChildren<ScrollRect>(true))
            {
                var sRt = scroll.GetComponent<RectTransform>();
                if (sRt != null)
                {
                    sRt.localRotation = Quaternion.identity; sRt.localScale = Vector3.one;
                    sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
                    sRt.anchoredPosition = new Vector2(0f, -20f);
                    sRt.sizeDelta = new Vector2(380f, 380f);
                }

                if (scroll.viewport != null)
                {
                    scroll.viewport.anchorMin = Vector2.zero; scroll.viewport.anchorMax = Vector2.one;
                    scroll.viewport.pivot = new Vector2(0.5f, 0.5f);
                    scroll.viewport.anchoredPosition = scroll.viewport.sizeDelta = Vector2.zero;
                    if (scroll.viewport.GetComponent<RectMask2D>() == null && scroll.viewport.GetComponent<Mask>() == null)
                        scroll.viewport.gameObject.AddComponent<RectMask2D>();
                }
                ForgeScrollViewStyler.StyleScrollRectVisuals(scroll);

                if (scroll.content != null)
                {
                    scroll.content.anchorMin = new Vector2(0f, 1f);
                    scroll.content.anchorMax = new Vector2(1f, 1f);
                    scroll.content.pivot = new Vector2(0.5f, 1f);
                    scroll.content.anchoredPosition = Vector2.zero;
                }
            }
        }

        private static void StyleRightPage(Transform page)
        {
            var rt = page.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(220f, -10f);
                rt.sizeDelta = new Vector2(420f, 470f);
            }

            var img = page.GetComponent<Image>() ?? page.gameObject.AddComponent<Image>();
            img.sprite = BookTextureBuilder.GetParchmentSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.97f, 0.94f, 0.88f, 1f);
            img.raycastTarget = false;

            for (int i = 0; i < page.childCount; i++)
            {
                Transform ch = page.GetChild(i);
                string n = ch.name.ToLowerInvariant();
                var chRt = ch.GetComponent<RectTransform>();
                if (chRt == null) continue;

                if (n.Contains("category"))
                {
                    chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 1f);
                    chRt.anchoredPosition = new Vector2(0f, -68f);
                    chRt.sizeDelta = new Vector2(340f, 22f);
                    var t = ch.GetComponent<TMP_Text>();
                    if (t != null) { t.margin = Vector4.zero; t.fontSize = 13f; t.enableAutoSizing = true; t.fontSizeMin = 10f; t.fontSizeMax = 13f; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; }
                }
                else if (n.Contains("title") || n.Contains("header"))
                {
                    chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 1f);
                    chRt.anchoredPosition = new Vector2(0f, -16f);
                    chRt.sizeDelta = new Vector2(360f, 28f);
                    var t = ch.GetComponent<TMP_Text>();
                    if (t != null) { t.margin = Vector4.zero; t.fontSize = 17f; t.enableAutoSizing = true; t.fontSizeMin = 12f; t.fontSizeMax = 17f; t.alignment = TextAlignmentOptions.Center; t.fontStyle = FontStyles.Bold; t.raycastTarget = false; }
                }
                else if (n.Contains("empty") || n.Contains("notice"))
                {
                    chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 0.5f);
                    chRt.anchoredPosition = new Vector2(0f, 20f);
                    chRt.sizeDelta = new Vector2(340f, 50f);
                    var t = ch.GetComponent<TMP_Text>();
                    if (t != null) { t.margin = Vector4.zero; t.fontSize = 15f; t.enableAutoSizing = true; t.fontSizeMin = 11f; t.fontSizeMax = 15f; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; }
                }
            }

            foreach (var scroll in page.GetComponentsInChildren<ScrollRect>(true))
            {
                var sRt = scroll.GetComponent<RectTransform>();
                if (sRt != null)
                {
                    sRt.localRotation = Quaternion.identity; sRt.localScale = Vector3.one;
                    sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
                    sRt.anchoredPosition = new Vector2(0f, -14f);
                    sRt.sizeDelta = new Vector2(375f, 294f);
                }

                if (scroll.viewport != null)
                {
                    scroll.viewport.anchorMin = Vector2.zero; scroll.viewport.anchorMax = Vector2.one;
                    scroll.viewport.pivot = new Vector2(0.5f, 0.5f);
                    scroll.viewport.anchoredPosition = scroll.viewport.sizeDelta = Vector2.zero;
                    if (scroll.viewport.GetComponent<RectMask2D>() == null && scroll.viewport.GetComponent<Mask>() == null)
                        scroll.viewport.gameObject.AddComponent<RectMask2D>();
                }
                ForgeScrollViewStyler.StyleScrollRectVisuals(scroll);

                if (scroll.content != null)
                {
                    scroll.content.anchorMin = new Vector2(0f, 1f); scroll.content.anchorMax = new Vector2(1f, 1f);
                    scroll.content.pivot = new Vector2(0.5f, 1f); scroll.content.anchoredPosition = Vector2.zero;

                    var csf = scroll.content.GetComponent<ContentSizeFitter>() ?? scroll.content.gameObject.AddComponent<ContentSizeFitter>();
                    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                    var vlg = scroll.content.GetComponent<VerticalLayoutGroup>() ?? scroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childControlWidth = true; vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
                    vlg.padding = new RectOffset(6, 6, 4, 4);
                }
            }

            foreach (var t in page.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name.ToLowerInvariant().Contains("desc"))
                {
                    var trt = t.rectTransform;
                    if (trt != null)
                    {
                        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
                        trt.pivot = new Vector2(0.5f, 1f); trt.anchoredPosition = trt.sizeDelta = Vector2.zero;
                    }
                    t.margin = Vector4.zero;
                    t.fontSize = 12.5f;
                    t.fontSizeMin = 10f;
                    t.fontSizeMax = 13.5f;
                    t.enableAutoSizing = true;
                    t.textWrappingMode = TextWrappingModes.Normal;
                    t.overflowMode = TextOverflowModes.Ellipsis;
                    t.alignment = TextAlignmentOptions.TopLeft;
                }
            }

            foreach (var b in page.GetComponentsInChildren<Button>(true))
            {
                if (b.name.ToLowerInvariant().Contains("equip"))
                {
                    var bRt = b.GetComponent<RectTransform>();
                    bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(0.5f, 0f);
                    bRt.anchoredPosition = new Vector2(0f, 16f);
                    bRt.sizeDelta = new Vector2(250f, 38f);
                    var bImg = b.GetComponent<Image>() ?? b.gameObject.AddComponent<Image>();
                    bImg.sprite = BookTextureBuilder.GetPrimaryActionButtonSprite();
                    bImg.type = Image.Type.Sliced;
                    bImg.color = Color.white;
                    var bTxt = b.GetComponentInChildren<TMP_Text>(true);
                    if (bTxt != null)
                    {
                        MedievalTypographyProvider.ApplyToTMP(bTxt);
                        bTxt.fontSize = 14f;
                        bTxt.enableAutoSizing = true;
                        bTxt.fontSizeMin = 11f;
                        bTxt.fontSizeMax = 14.5f;
                        bTxt.fontStyle = FontStyles.Bold;
                        bTxt.color = new Color(1f, 0.96f, 0.85f, 1f);
                        bTxt.alignment = TextAlignmentOptions.Center;
                    }
                }
            }
        }

        private static void StyleTexts(Transform root)
        {
            MedievalTypographyProvider.ApplyToAllTMPInHierarchy(root);
            Color darkInk = new Color(0.17f, 0.11f, 0.06f, 1f), subInk = new Color(0.32f, 0.21f, 0.12f, 1f);
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<BookTabIndicator>() != null || t.GetComponentInParent<Button>(true) != null) continue;
                t.margin = Vector4.zero;
                string n = t.name.ToLowerInvariant();
                if (t.transform.parent != null && t.transform.parent.GetComponent<Button>() != null) { t.color = darkInk; t.fontStyle = FontStyles.Bold; continue; }
                if (n.Contains("title") || n.Contains("header") || n.Contains("pausetext")) ForgeCanvasThemeStyler.DecorateTitleText(t, 20f);
                else if (n.Contains("category") || n.Contains("empty") || n.Contains("notice")) { t.color = subInk; t.fontStyle = FontStyles.Italic; }
                else t.color = darkInk;
            }
        }
    }
}
