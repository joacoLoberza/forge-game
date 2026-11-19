using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    public static partial class ForgeCanvasThemeStyler
    {
        public static void StyleFurnacePanel(FurnacePanel panel)
        {
            if (panel == null) return;
            Transform root = panel.transform;
            StretchPanelRoot(root);

            var rootImg = root.GetComponent<Image>();
            if (rootImg != null) rootImg.enabled = false;

            ScrollRect oreScroll = panel.GetComponentInChildren<ScrollRect>(true);
            Slider tempSlider = panel.TemperatureSlider, progSlider = panel.ProgressSlider;
            if (tempSlider == null || progSlider == null)
                foreach (var s in panel.GetComponentsInChildren<Slider>(true))
                    if (s.name.ToLowerInvariant().Contains("temp")) tempSlider = s; else progSlider = s;

            CreateOrGetTitleBanner(root, "FurnaceTopTitlePlaque", "SMELTING FURNACE",
                new Vector2(0f, -14f), new Vector2(468f, 56f), fontSize: 22.5f, topAnchored: true);

            RectTransform leftBody = CreateOrGetParchmentCard(root, "LeftFurnaceCard", new Vector2(-665f, -32f), new Vector2(480f, 820f));
            RectTransform rightBody = CreateOrGetParchmentCard(root, "RightFurnaceCard", new Vector2(665f, -32f), new Vector2(480f, 820f));

            CreateOrGetTitleBanner(leftBody, "LeftFurnaceHeader", "RAW ORES",
                new Vector2(0f, 355f), new Vector2(400f, 42f), fontSize: 17f);

            CreateOrGetTitleBanner(rightBody, "RightFurnaceHeader", "THERMAL CONTROL & SMELTING",
                new Vector2(0f, 355f), new Vector2(400f, 42f), fontSize: 17f);

            if (oreScroll != null)
            {
                oreScroll.gameObject.SetActive(true);
                oreScroll.transform.SetParent(leftBody, false);
                var sRt = oreScroll.GetComponent<RectTransform>();
                sRt.localRotation = Quaternion.identity; sRt.localScale = Vector3.one;
                sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
                sRt.anchoredPosition = new Vector2(0f, 160f);
                sRt.sizeDelta = new Vector2(400f, 300f);

                var sImg = oreScroll.GetComponent<Image>() ?? oreScroll.gameObject.AddComponent<Image>();
                sImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                sImg.type = Image.Type.Sliced;
                sImg.color = new Color(0.91f, 0.86f, 0.77f, 0.95f);
                ForgeScrollViewStyler.StyleScrollRectVisuals(oreScroll);
            }

            if (panel.OreCountText != null)
                PlaceBodyText(panel.OreCountText, leftBody, new Vector2(0f, -14f), new Vector2(400f, 34f), 14f, TextAlignmentOptions.Center, DarkInk, bold: true);
            if (panel.InsertOresButton != null)
                StyleForgeButton(panel.InsertOresButton, primary: true, leftBody, new Vector2(0f, -62f), new Vector2(400f, 44f));
            if (panel.ExtractOreButton != null)
                StyleForgeButton(panel.ExtractOreButton, primary: false, leftBody, new Vector2(0f, -114f), new Vector2(400f, 40f));

            var leftHintBox = BookJournalTemplateBuilder.CreateOrGetPageInset(leftBody, "LeftControlsHintBox", new Vector2(0f, -342f), new Vector2(404f, 44f), new Color(0.90f, 0.84f, 0.72f, 0.80f));
            EnsureHelperNote(leftHintBox, "LeftControlsHint",
                "Click: select ore  |  Shift+Click: batch x3",
                Vector2.zero, new Vector2(384f, 34f));

            // Tarjeta Derecha: Temperatura + Rango objetivo en placa termica unificada
            var thermalBox = BookJournalTemplateBuilder.CreateOrGetPageInset(rightBody, "ThermalReadoutPlaque", new Vector2(0f, 304f), new Vector2(404f, 48f), new Color(0.91f, 0.85f, 0.74f, 0.90f));
            if (panel.TemperatureText != null)
                PlaceBodyText(panel.TemperatureText, thermalBox, new Vector2(-114f, 0f), new Vector2(156f, 38f), 22f, TextAlignmentOptions.Left, new Color(0.68f, 0.18f, 0.05f, 1f), bold: true);

            if (panel.TargetRangeText != null)
                PlaceBodyText(panel.TargetRangeText, thermalBox, new Vector2(76f, 0f), new Vector2(226f, 38f), 13.5f, TextAlignmentOptions.Right, SubInk, bold: true);

            if (tempSlider != null)
            {
                tempSlider.transform.SetParent(rightBody, false);
                var rt = tempSlider.GetComponent<RectTransform>();
                rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 238f);
                rt.sizeDelta = new Vector2(400f, 40f);
                StyleSliderControl(tempSlider, interactive: false, isThermalGauge: true);
            }

            panel.EnsureBellowsButtonExists();
            if (panel.AddCoalButton != null && panel.BellowsButton != null)
            {
                StyleForgeButton(panel.AddCoalButton, primary: true, rightBody, new Vector2(-102f, 158f), new Vector2(196f, 48f));
                StyleForgeButton(panel.BellowsButton, primary: false, rightBody, new Vector2(102f, 158f), new Vector2(196f, 48f));
            }
            else if (panel.AddCoalButton != null)
                StyleForgeButton(panel.AddCoalButton, primary: true, rightBody, new Vector2(0f, 158f), new Vector2(400f, 48f));

            var rightHintBox = BookJournalTemplateBuilder.CreateOrGetPageInset(rightBody, "RightVentHintBox", new Vector2(0f, 102f), new Vector2(404f, 50f), new Color(0.90f, 0.84f, 0.72f, 0.80f));
            EnsureHelperNote(rightHintBox, "RightVentHint",
                "Timing: [Shift] Heat up quickly  |  [Space] Bellows cool down rapidly",
                Vector2.zero, new Vector2(384f, 42f));

            CreateOrGetTitleBanner(rightBody, "ProgressSubHeader", "SMELTING PROGRESS",
                new Vector2(0f, 38f), new Vector2(390f, 36f), fontSize: 15f);

            if (progSlider != null)
            {
                progSlider.transform.SetParent(rightBody, false);
                var rt = progSlider.GetComponent<RectTransform>();
                rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -18f);
                rt.sizeDelta = new Vector2(400f, 30f);
                StyleSliderControl(progSlider, interactive: false, isThermalGauge: false);
            }

            if (panel.StatusText != null)
                PlaceInsetStatusBox(panel.StatusText, rightBody, new Vector2(0f, -108f), new Vector2(400f, 82f));

            if (panel.ExtractBloomButton != null)
            {
                StyleForgeButton(panel.ExtractBloomButton, primary: true, rightBody, new Vector2(0f, -208f), new Vector2(400f, 50f));
                SetButtonText(panel.ExtractBloomButton, "Extract Bloom");
            }

            if (panel.CloseButton != null)
            {
                StyleCloseButton(panel.CloseButton, rightBody, new Vector2(0f, -335f), topRightCorner: false, label: "Close");
            }
            SetButtonText(panel.InsertOresButton, "Insert Ores");
            SetButtonText(panel.ExtractOreButton, "Remove Ore");
            panel.UpdateThermalButton();

            RemoveLegacyNodeIfUnused(root.Find("RightStationCard"), oreScroll, tempSlider, progSlider);
            RemoveLegacyNodeIfUnused(root.Find("LeftStationCard"), oreScroll, tempSlider, progSlider);
            RemoveLegacyNodeIfUnused(leftBody.Find("ParchmentBody"), oreScroll, tempSlider, progSlider);
            RemoveLegacyNodeIfUnused(rightBody.Find("ParchmentBody"), oreScroll, tempSlider, progSlider);
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr != null && tr.parent == root && tr.name != "FurnaceTopTitlePlaque" && tr.name != "LeftFurnaceCard" && tr.name != "RightFurnaceCard" && tr.GetComponent<TMP_Text>() != null)
                    tr.gameObject.SetActive(false);
        }

        private static void SetButtonText(Button btn, string label)
        {
            var t = btn != null ? btn.GetComponentInChildren<TMP_Text>(true) : null;
            if (t != null) t.text = label;
        }

        private static void RemoveLegacyNodeIfUnused(Transform t, ScrollRect oreScroll, Slider s1, Slider s2)
        {
            if (t == null) return;
            if ((oreScroll != null && oreScroll.transform.IsChildOf(t)) || (s1 != null && s1.transform.IsChildOf(t)) || (s2 != null && s2.transform.IsChildOf(t))) return;
            t.gameObject.SetActive(false);
            if (Application.isPlaying) Object.Destroy(t.gameObject);
            else Object.DestroyImmediate(t.gameObject);
        }

        private static void PlaceBodyText(TMP_Text t, Transform parent, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, Color color, bool bold = false)
        {
            if (t == null) return;
            MedievalTypographyProvider.ApplyToTMP(t);
            t.transform.SetParent(parent, false);
            var rt = t.rectTransform;
            rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            t.margin = Vector4.zero;
            t.fontSize = fontSize;
            t.enableAutoSizing = true;
            t.fontSizeMin = Mathf.Max(9.5f, fontSize - 4.5f);
            t.fontSizeMax = fontSize + 1f;
            t.alignment = align;
            t.color = color;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void PlaceInsetStatusBox(TMP_Text t, Transform parent, Vector2 pos, Vector2 size)
        {
            Transform boxTr = parent.Find("StatusInsetBox");
            GameObject box = boxTr != null ? boxTr.gameObject : new GameObject("StatusInsetBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(parent, false);
            var bRt = box.GetComponent<RectTransform>();
            bRt.localRotation = Quaternion.identity; bRt.localScale = Vector3.one;
            bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = pos; bRt.sizeDelta = size;
            var img = box.GetComponent<Image>();
            img.sprite = BookTextureBuilder.GetParchmentBannerSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.90f, 0.84f, 0.73f, 1f);
            img.raycastTarget = false;

            PlaceBodyText(t, box.transform, Vector2.zero, size - new Vector2(20f, 12f), 14f, TextAlignmentOptions.Center, DarkInk, bold: true);
        }

        private static void EnsureHelperNote(Transform parent, string name, string content, Vector2 pos, Vector2 size)
        {
            Transform tr = parent.Find(name);
            GameObject go = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.raycastTarget = false;
            PlaceBodyText(tmp, parent, pos, size, 13f, TextAlignmentOptions.Center, SubInk);
        }
    }
}
