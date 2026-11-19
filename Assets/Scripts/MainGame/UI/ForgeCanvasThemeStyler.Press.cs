using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    public static partial class ForgeCanvasThemeStyler
    {
        public static void StylePressPanel(PressPanel panel)
        {
            if (panel == null) return;
            Transform root = panel.transform;
            StretchPanelRoot(root);

            var rootImg = root.GetComponent<Image>();
            if (rootImg != null) rootImg.enabled = false;

            for (int i = 0; i < root.childCount; i++)
            {
                var ch = root.GetChild(i);
                string n = ch.name.Trim().ToLowerInvariant();
                if ((n.Contains("title") || n.Contains("header")) && ch.GetComponent<TMP_Text>() != null)
                    ch.gameObject.SetActive(false);
            }

            CreateOrGetTitleBanner(root, "PressTopTitlePlaque", "MECHANICAL PRESS",
                new Vector2(0f, -14f), new Vector2(468f, 56f), fontSize: 22.5f, topAnchored: true);

            RectTransform leftBody = CreateOrGetParchmentCard(root, "LeftPressCard", new Vector2(-665f, -32f), new Vector2(480f, 800f));
            RectTransform rightBody = CreateOrGetParchmentCard(root, "RightPressCard", new Vector2(665f, -32f), new Vector2(480f, 800f));

            // Tarjeta inferior ampliada para albergar holgadamente detalles y pureza del lingote
            RectTransform bottomBody = CreateOrGetParchmentCard(root, "BottomPressDetailsCard", Vector2.zero, new Vector2(740f, 156f));
            var bottomOuter = bottomBody.parent as RectTransform;
            if (bottomOuter != null)
            {
                bottomOuter.anchorMin = bottomOuter.anchorMax = bottomOuter.pivot = new Vector2(0.5f, 0f);
                bottomOuter.anchoredPosition = new Vector2(0f, 16f);
                bottomOuter.sizeDelta = new Vector2(740f, 156f);
            }

            CreateOrGetTitleBanner(bottomBody, "BottomDetailsHeader", "PRESSING DETAILS & DIFFICULTY",
                new Vector2(0f, 44f), new Vector2(420f, 28f), fontSize: 14f);

            if (panel.BloomInfoText != null)
                PlaceBodyText(panel.BloomInfoText, bottomBody, new Vector2(0f, -14f), new Vector2(680f, 76f), 14.5f, TextAlignmentOptions.Center, DarkInk);

            CreateOrGetTitleBanner(leftBody, "LeftPressHeader", "BLOOMS IN INVENTORY",
                new Vector2(0f, 348f), new Vector2(400f, 42f), fontSize: 17f);

            CreateOrGetTitleBanner(rightBody, "RightPressHeader", "INGOT COMPACTING",
                new Vector2(0f, 348f), new Vector2(400f, 42f), fontSize: 17f);

            ScrollRect bloomScroll = panel.GetComponentInChildren<ScrollRect>(true);
            if (bloomScroll != null)
            {
                Transform selSection = bloomScroll.transform.parent != root && bloomScroll.transform.parent != leftBody
                    ? bloomScroll.transform.parent
                    : bloomScroll.transform;
                selSection.SetParent(leftBody, false);
                var secRt = selSection.GetComponent<RectTransform>();
                if (secRt != null)
                {
                    secRt.localRotation = Quaternion.identity; secRt.localScale = Vector3.one;
                    secRt.anchorMin = secRt.anchorMax = secRt.pivot = new Vector2(0.5f, 0.5f);
                    secRt.anchoredPosition = new Vector2(0f, 68f);
                    secRt.sizeDelta = new Vector2(400f, 400f);
                }

                var sRt = bloomScroll.GetComponent<RectTransform>();
                sRt.localRotation = Quaternion.identity; sRt.localScale = Vector3.one;
                sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
                sRt.anchoredPosition = Vector2.zero;
                sRt.sizeDelta = new Vector2(400f, 390f);

                var sImg = bloomScroll.GetComponent<Image>() ?? bloomScroll.gameObject.AddComponent<Image>();
                sImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                sImg.type = Image.Type.Sliced;
                sImg.color = new Color(0.91f, 0.86f, 0.77f, 0.95f);
            }

            var guideInset = BookJournalTemplateBuilder.CreateOrGetPageInset(leftBody, "PressGuideInset", new Vector2(0f, -255f), new Vector2(408f, 134f), new Color(0.90f, 0.84f, 0.72f, 0.82f));
            EnsureHelperNote(guideInset, "PressGuideNote",
                "<b>How to use the Press:</b>\n1. Select a Bloom: it rests on the lower base.\n2. Press [SPACE] repeatedly: compacting the billet.\n3. At 100%, collect your 3D Ingot.",
                Vector2.zero, new Vector2(382f, 118f));

            CreateOrGetTitleBanner(rightBody, "PressGaugeHeader", "MECHANICAL COMPACTION",
                new Vector2(0f, 240f), new Vector2(396f, 38f), fontSize: 15.5f);

            Slider pressSlider = panel.PressProgressSlider;
            if (pressSlider == null) pressSlider = panel.GetComponentInChildren<Slider>(true);
            if (pressSlider != null)
            {
                pressSlider.transform.SetParent(rightBody, false);
                var slRt = pressSlider.GetComponent<RectTransform>();
                slRt.localRotation = Quaternion.identity; slRt.localScale = Vector3.one;
                slRt.anchorMin = slRt.anchorMax = slRt.pivot = new Vector2(0.5f, 0.5f);
                slRt.anchoredPosition = new Vector2(0f, 180f);
                slRt.sizeDelta = new Vector2(400f, 38f);
                StyleSliderControl(pressSlider, interactive: false, isThermalGauge: false);
            }

            if (panel.ProgressText != null)
            {
                var pctPlaque = BookJournalTemplateBuilder.CreateOrGetPageInset(rightBody, "PressPctPlaque", new Vector2(0f, 124f), new Vector2(210f, 40f), new Color(0.92f, 0.86f, 0.74f, 0.90f));
                PlaceBodyText(panel.ProgressText, pctPlaque, Vector2.zero, new Vector2(190f, 34f), 22f, TextAlignmentOptions.Center, new Color(0.62f, 0.24f, 0.04f, 1f), bold: true);
            }

            if (panel.MashButton != null)
            {
                StyleForgeButton(panel.MashButton, primary: true, rightBody, new Vector2(0f, 38f), new Vector2(400f, 56f));
                SetButtonText(panel.MashButton, "PRESS [SPACE]");
            }
            if (panel.ExtractIngotButton != null)
            {
                StyleForgeButton(panel.ExtractIngotButton, primary: true, rightBody, new Vector2(0f, 38f), new Vector2(400f, 56f));
                SetButtonText(panel.ExtractIngotButton, "Collect Ingot");
            }
            if (panel.RetrieveBloomButton != null)
            {
                StyleForgeButton(panel.RetrieveBloomButton, primary: false, rightBody, new Vector2(0f, -32f), new Vector2(400f, 44f));
                SetButtonText(panel.RetrieveBloomButton, "Retrieve Bloom");
            }
            if (panel.StatusText != null)
                PlaceInsetStatusBox(panel.StatusText, rightBody, new Vector2(0f, -150f), new Vector2(400f, 88f));
            if (panel.CloseButton != null)
            {
                StyleCloseButton(panel.CloseButton, rightBody, new Vector2(0f, -320f), topRightCorner: false, label: "Close");
            }

            foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null || t == panel.BloomInfoText || t == panel.ProgressText || t == panel.StatusText) continue;
                if (t.GetComponentInParent<Button>() != null || t.name == "TitleLabel" || t.name == "PressGuideNote") continue;
                string tn = t.name.Trim().ToLowerInvariant();
                if (tn.Contains("empty") || tn.Contains("notice"))
                {
                    MedievalTypographyProvider.ApplyToTMP(t);
                    t.margin = Vector4.zero;
                    t.color = SubInk;
                    t.text = "No Blooms in inventory.\nSmelt raw ores in the Smelting Furnace!";
                    t.fontSize = 14.5f;
                    t.fontStyle = FontStyles.Italic;
                    t.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        public static void StyleForgeButton(Button btn, bool primary, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            if (btn == null) return;
            if (parent != null) btn.transform.SetParent(parent, false);
            var rt = btn.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;

            var img = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
            img.sprite = primary ? BookTextureBuilder.GetPrimaryActionButtonSprite() : BookTextureBuilder.GetParchmentBannerSprite();
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = primary ? new Color(1.12f, 1.07f, 0.96f, 1f) : new Color(1.06f, 1.01f, 0.92f, 1f);
            cb.pressedColor = primary ? new Color(0.82f, 0.74f, 0.62f, 1f) : new Color(0.84f, 0.77f, 0.66f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.62f, 0.58f, 0.52f, 0.65f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                MedievalTypographyProvider.ApplyToTMP(tmp);
                tmp.rectTransform.localRotation = Quaternion.identity;
                tmp.rectTransform.localScale = Vector3.one;
                tmp.margin = Vector4.zero;
                tmp.fontSize = primary ? 16f : 15f;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 11.5f;
                tmp.fontSizeMax = primary ? 16.5f : 15.5f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = primary ? new Color(1f, 0.96f, 0.85f, 1f) : DarkInk;
                tmp.alignment = TextAlignmentOptions.Center;
            }
        }
    }
}
