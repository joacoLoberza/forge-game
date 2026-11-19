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
                rt.anchoredPosition = new Vector2(-216f, -10f);
                rt.sizeDelta = new Vector2(416f, 464f);
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
                    sRt.anchoredPosition = new Vector2(0f, -22f);
                    sRt.sizeDelta = new Vector2(382f, 384f);
                }

                var sImg = scroll.GetComponent<Image>() ?? scroll.gameObject.AddComponent<Image>();
                sImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                sImg.type = Image.Type.Sliced;
                sImg.color = new Color(0.92f, 0.87f, 0.77f, 0.78f);

                if (scroll.viewport != null)
                {
                    scroll.viewport.anchorMin = Vector2.zero; scroll.viewport.anchorMax = Vector2.one;
                    scroll.viewport.pivot = new Vector2(0.5f, 0.5f);
                    scroll.viewport.offsetMin = new Vector2(6f, 6f); scroll.viewport.offsetMax = new Vector2(-6f, -6f);
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
                rt.anchoredPosition = new Vector2(216f, -10f);
                rt.sizeDelta = new Vector2(416f, 464f);
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
                    chRt.sizeDelta = new Vector2(346f, 36f);
                    var t = ch.GetComponent<TMP_Text>();
                    if (t != null)
                    {
                        t.margin = new Vector4(12f, 2f, 12f, 2f);
                        t.fontSize = 18f;
                        t.enableAutoSizing = true;
                        t.fontSizeMin = 9.5f;
                        t.fontSizeMax = 18f;
                        t.textWrappingMode = TextWrappingModes.NoWrap;
                        t.overflowMode = TextOverflowModes.Ellipsis;
                        t.alignment = TextAlignmentOptions.Center;
                        t.fontStyle = FontStyles.Bold;
                        t.raycastTarget = false;
                    }
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
                    sRt.sizeDelta = new Vector2(378f, 294f);
                }

                var sImg = scroll.GetComponent<Image>() ?? scroll.gameObject.AddComponent<Image>();
                sImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                sImg.type = Image.Type.Sliced;
                sImg.color = new Color(0.92f, 0.87f, 0.77f, 0.78f);

                if (scroll.viewport != null)
                {
                    scroll.viewport.anchorMin = Vector2.zero; scroll.viewport.anchorMax = Vector2.one;
                    scroll.viewport.pivot = new Vector2(0.5f, 0.5f);
                    scroll.viewport.offsetMin = new Vector2(8f, 6f); scroll.viewport.offsetMax = new Vector2(-8f, -6f);
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
                    ForgeCanvasThemeStyler.StyleForgeButton(b, primary: true, page, new Vector2(0f, -196f), new Vector2(264f, 40f));
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
