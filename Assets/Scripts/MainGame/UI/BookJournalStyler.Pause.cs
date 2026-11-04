using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    public static partial class BookJournalStyler
    {
        public static void StylePauseMenuPanel()
        {
            var pause = Object.FindFirstObjectByType<PauseMenuPanel>(FindObjectsInactive.Include);
            if (pause == null) return;

            Transform root = pause.transform;
            var rt = root.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(440f, 520f);
            }

            var img = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            img.sprite = BookTextureBuilder.GetParchmentSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.96f, 0.93f, 0.86f, 0.98f);

            foreach (var legacyTxt in root.GetComponentsInChildren<Text>(true))
            {
                if (legacyTxt == null) continue;
                Font medFont = MedievalTypographyProvider.GetIMGUIFont();
                if (medFont != null) legacyTxt.font = medFont;
                if (legacyTxt.GetComponentInParent<Button>() != null) continue;
                string ln = legacyTxt.name.ToLowerInvariant();
                if (ln.Contains("porcent") || ln.Contains("percent") || ln.Contains("value"))
                {
                    PlaceVolumePercentInsideSheet(legacyTxt.rectTransform, root);
                    legacyTxt.fontSize = 17;
                    legacyTxt.fontStyle = FontStyle.Bold;
                    legacyTxt.alignment = TextAnchor.MiddleCenter;
                    legacyTxt.color = ForgeCanvasThemeStyler.DarkInk;
                    legacyTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    legacyTxt.verticalOverflow = VerticalWrapMode.Overflow;
                    legacyTxt.raycastTarget = false;
                }
            }

            foreach (var tmpTxt in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmpTxt == null || tmpTxt.GetComponentInParent<Button>() != null) continue;
                MedievalTypographyProvider.ApplyToTMP(tmpTxt);
                string tn = tmpTxt.name.ToLowerInvariant();
                if (tn.Contains("porcent") || tn.Contains("percent") || tn.Contains("volvalue"))
                {
                    PlaceVolumePercentInsideSheet(tmpTxt.rectTransform, root);
                    tmpTxt.fontSize = 17f;
                    tmpTxt.fontStyle = FontStyles.Bold;
                    tmpTxt.alignment = TextAlignmentOptions.Center;
                    tmpTxt.color = ForgeCanvasThemeStyler.DarkInk;
                    tmpTxt.margin = Vector4.zero;
                    tmpTxt.raycastTarget = false;
                }
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform ch = root.GetChild(i);
                string n = ch.name.ToLowerInvariant();
                var chRt = ch.GetComponent<RectTransform>();
                if (chRt == null) continue;

                chRt.localRotation = Quaternion.identity; chRt.localScale = Vector3.one;
                chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 0.5f);

                if (n.Contains("porcent") || n.Contains("percent") || n.Contains("volvalue"))
                {
                    chRt.anchoredPosition = new Vector2(138f, 65f);
                    chRt.sizeDelta = new Vector2(84f, 32f);
                }
                else if (n.Contains("pausetext") || n.Contains("title"))
                {
                    chRt.anchoredPosition = new Vector2(0f, 180f);
                    chRt.sizeDelta = new Vector2(340f, 48f);
                }
                else if (n.Contains("volumentext"))
                {
                    chRt.anchoredPosition = new Vector2(0f, 108f);
                    chRt.sizeDelta = new Vector2(320f, 32f);
                    var t = ch.GetComponent<TMP_Text>();
                    if (t != null)
                    {
                        MedievalTypographyProvider.ApplyToTMP(t);
                        t.text = "Volumen Maestro";
                        t.fontSize = 17f;
                        t.enableAutoSizing = true;
                        t.fontSizeMin = 13f;
                        t.fontSizeMax = 18f;
                        t.alignment = TextAlignmentOptions.Center;
                        t.color = ForgeCanvasThemeStyler.DarkInk;
                    }
                }
                else if (ch.GetComponent<Slider>() != null || n.Contains("slider"))
                {
                    chRt.anchoredPosition = new Vector2(-34f, 65f);
                    chRt.sizeDelta = new Vector2(240f, 30f);
                    var sl = ch.GetComponent<Slider>();
                    if (sl != null) ForgeCanvasThemeStyler.StyleSliderControl(sl, interactive: true);
                }
                else if (n.Contains("resume") || n.Contains("reanudar"))
                {
                    chRt.anchoredPosition = new Vector2(0f, -20f);
                    chRt.sizeDelta = new Vector2(240f, 44f);
                    var bTxt = ch.GetComponentInChildren<TMP_Text>(true);
                    if (bTxt != null)
                    {
                        MedievalTypographyProvider.ApplyToTMP(bTxt);
                        bTxt.text = "Reanudar Partida";
                        bTxt.fontSize = 15f;
                        bTxt.fontStyle = FontStyles.Bold;
                        bTxt.color = ForgeCanvasThemeStyler.DarkInk;
                        bTxt.alignment = TextAlignmentOptions.Center;
                    }
                }
                else if (n.Contains("back") || n.Contains("menu") || n.Contains("salir"))
                {
                    chRt.anchoredPosition = new Vector2(0f, -85f);
                    chRt.sizeDelta = new Vector2(240f, 44f);
                    var bTxt = ch.GetComponentInChildren<TMP_Text>(true);
                    if (bTxt != null)
                    {
                        MedievalTypographyProvider.ApplyToTMP(bTxt);
                        bTxt.text = "Volver al Menú";
                        bTxt.fontSize = 15f;
                        bTxt.fontStyle = FontStyles.Bold;
                        bTxt.color = ForgeCanvasThemeStyler.DarkInk;
                        bTxt.alignment = TextAlignmentOptions.Center;
                    }
                }
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                var bImg = b.GetComponent<Image>() ?? b.gameObject.AddComponent<Image>();
                bImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                bImg.type = Image.Type.Sliced;
                bImg.color = new Color(0.90f, 0.84f, 0.74f, 1f);
            }

            StyleTexts(root);
        }

        private static void PlaceVolumePercentInsideSheet(RectTransform pctRt, Transform pauseRoot)
        {
            if (pctRt == null || pauseRoot == null) return;
            pctRt.SetParent(pauseRoot, false);
            pctRt.localRotation = Quaternion.identity; pctRt.localScale = Vector3.one;
            pctRt.anchorMin = pctRt.anchorMax = pctRt.pivot = new Vector2(0.5f, 0.5f);
            pctRt.anchoredPosition = new Vector2(138f, 65f);
            pctRt.sizeDelta = new Vector2(84f, 32f);
        }
    }
}
