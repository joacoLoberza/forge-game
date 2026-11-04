using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.Core;

namespace ForjaGame.UI
{
    /// <summary>
    /// Estilo de libro/pergamino para el Yunque: selección de armas, lingotes, mangos y botones
    /// de acción dimensionados con precisión dentro de las tarjetas sin desbordar los marcos.
    /// </summary>
    public static partial class ForgeCanvasThemeStyler
    {
        public static void StyleAnvilPanel(AnvilPanel panel)
        {
            if (panel == null) return;
            panel.EnsureStepContainers();
            Transform root = panel.transform;
            StretchPanelRoot(root);

            var rootImg = root.GetComponent<Image>();
            if (rootImg != null) rootImg.enabled = false;

            CreateOrGetTitleBanner(root, "AnvilTopTitlePlaque", "YUNQUE DE FORJA",
                new Vector2(0f, -14f), new Vector2(420f, 50f), fontSize: 21f, topAnchored: true);

            RectTransform leftBody = CreateOrGetParchmentCard(root, "LeftAnvilCard", new Vector2(-665f, -32f), new Vector2(480f, 820f));
            RectTransform rightBody = CreateOrGetParchmentCard(root, "RightAnvilCard", new Vector2(665f, -32f), new Vector2(480f, 820f));

            CreateOrGetTitleBanner(rightBody, "RightAnvilHeader", "GUÍA DE FORJA",
                new Vector2(0f, 355f), new Vector2(400f, 42f), fontSize: 17f);

            Transform guideObj = rightBody.Find("AnvilGuideText");
            TMP_Text guideTxt = guideObj != null ? guideObj.GetComponent<TMP_Text>() : null;
            if (guideTxt == null)
            {
                var go = new GameObject("AnvilGuideText", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(rightBody, false);
                guideTxt = go.GetComponent<TextMeshProUGUI>();
            }
            guideTxt.gameObject.name = "AnvilGuideText";
            guideTxt.text = "<b>PASOS DEL FORJADO ARTESANAL:</b>\n\n" +
                            "<b>1. Selección de Arma:</b>\nElige el arma a forjar.\n\n" +
                            "<b>2. Selección de Lingote:</b>\nEl metal define peso, filo y daño.\n\n" +
                            "<b>3. Martillado y Corte:</b>\n[1] Extiende el metal con el martillo.\n[2] Recorta el exceso con el cincel.\n[3] Lija y pule las aristas.\n\n" +
                            "<b>4. Fragua y Templado:</b>\n[H] Recalienta el lingote si se enfría.\n[Espacio] Templa en agua al completar el filo.\n\n" +
                            "<b>5. Ensamblaje:</b>\nAlinea el mango y consagra tu arma.";
            PlaceBodyText(guideTxt, rightBody, new Vector2(0f, 55f), new Vector2(400f, 500f), 13.5f, TextAlignmentOptions.TopLeft, DarkInk);

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform ch = root.GetChild(i);
                if (ch.name == "AnvilTopTitlePlaque" || ch.name == "LeftAnvilCard" || ch.name == "RightAnvilCard") continue;
                ch.SetParent(leftBody, false);
            }

            for (int i = 0; i < leftBody.childCount; i++)
            {
                Transform ch = leftBody.GetChild(i);
                var rt = ch.GetComponent<RectTransform>();
                if (rt == null) continue;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
                string n = ch.name.ToLowerInvariant();
                bool isStep = (panel.Step1WeaponSelection != null && ch.gameObject == panel.Step1WeaponSelection)
                    || (panel.Step2IngotSelection != null && ch.gameObject == panel.Step2IngotSelection)
                    || (panel.StepIngotConformation != null && ch.gameObject == panel.StepIngotConformation)
                    || (panel.Step3HandleSelection != null && ch.gameObject == panel.Step3HandleSelection)
                    || n.Contains("step") || n.Contains("selec") || n.Contains("conformation");

                if (isStep)
                {
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0f, 32f);
                    rt.sizeDelta = new Vector2(420f, 660f);
                    StyleAnvilStepContainer(ch);
                }
            }

            if (panel.Step1WeaponSelection != null)
                StyleWeaponSelectionStep(panel.Step1WeaponSelection.transform, panel.SwordButton, panel.AxeButton, panel.HammerButton);

            if (panel.StatusText != null)
                PlaceInsetStatusBox(panel.StatusText, rightBody, new Vector2(0f, -270f), new Vector2(420f, 56f));

            if (panel.CloseButton != null)
                StyleForgeButton(panel.CloseButton, primary: false, rightBody, new Vector2(0f, -345f), new Vector2(280f, 44f));

            foreach (var b in leftBody.GetComponentsInChildren<Button>(true))
            {
                if (b == null || b.GetComponentInParent<ScrollRect>(true) != null) continue;
                if (b == panel.SwordButton || b == panel.AxeButton || b == panel.HammerButton || b == panel.CloseButton) continue;

                string bn = b.name.ToLowerInvariant();
                if (bn.Contains("(1)") || bn.Contains("start") || bn.Contains("forjar") || bn.Contains("confirm"))
                {
                    StyleForgeButton(b, primary: true, b.transform.parent, new Vector2(0f, -214f), new Vector2(340f, 48f));
                    var t = b.GetComponentInChildren<TMP_Text>(true);
                    if (t != null) { MedievalTypographyProvider.ApplyToTMP(t); t.text = "FORJAR EN EL YUNQUE"; }
                }
                else if (bn.Contains("back") || bn.Contains("atras") || bn.Contains("regresar"))
                {
                    StyleForgeButton(b, primary: false, b.transform.parent, new Vector2(0f, -276f), new Vector2(260f, 42f));
                    var t = b.GetComponentInChildren<TMP_Text>(true);
                    if (t != null) { MedievalTypographyProvider.ApplyToTMP(t); t.text = "« Volver"; }
                }
                else StyleForgeButton(b, primary: false, b.transform.parent, b.GetComponent<RectTransform>().anchoredPosition, new Vector2(280f, 44f));
            }
        }

        private static void StyleWeaponSelectionStep(Transform step1, Button swordBtn, Button axeBtn, Button hammerBtn)
        {
            CleanButtonPlaques(step1, swordBtn, axeBtn, hammerBtn);

            Transform existing = step1.Find("WeaponTitle");
            TMP_Text header = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (header == null)
            {
                var go = new GameObject("WeaponTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(step1, false);
                header = go.GetComponent<TextMeshProUGUI>();
            }
            header.gameObject.name = "WeaponTitle";
            header.text = "SELECCIONA EL ARMA A FORJAR";
            var hRt = header.rectTransform;
            hRt.anchorMin = hRt.anchorMax = hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = new Vector2(0f, -6f);
            hRt.sizeDelta = new Vector2(396f, 42f);
            DecorateTitleText(header, 17.5f);

            StyleLargeWeaponOptionButton(swordBtn, step1, new Vector2(0f, 154f), new Vector2(404f, 138f),
                "<size=21><b>ESPADA</b></size>\n<size=13.5>Hoja equilibrada · Cortes veloces y gran control</size>");
            StyleLargeWeaponOptionButton(axeBtn, step1, new Vector2(0f, 2f), new Vector2(404f, 138f),
                "<size=21><b>HACHA</b></size>\n<size=13.5>Hoja pesada · Alto daño de tajo lateral</size>");
            StyleLargeWeaponOptionButton(hammerBtn, step1, new Vector2(0f, -150f), new Vector2(404f, 138f),
                "<size=21><b>MARTILLO</b></size>\n<size=13.5>Maza contundente · Impacto demoledor contra corazas</size>");
        }

        private static void CleanButtonPlaques(Transform step1, params Button[] buttons)
        {
            for (int i = step1.childCount - 1; i >= 0; i--)
            {
                var ch = step1.GetChild(i);
                if (ch.name.StartsWith("Text (TMP)_PlaqueBG") || (ch.name.Contains("_PlaqueBG") && ch.name != "WeaponTitle_PlaqueBG"))
                    UnitySafeUtility.DestroyGameObjectSafe(ch.gameObject);
            }
            for (int b = 0; b < buttons.Length; b++)
            {
                var btn = buttons[b];
                if (btn == null) continue;
                for (int i = btn.transform.childCount - 1; i >= 0; i--)
                {
                    var c = btn.transform.GetChild(i);
                    if (c.name.Contains("_PlaqueBG")) UnitySafeUtility.DestroyGameObjectSafe(c.gameObject);
                }
            }
        }

        private static void StyleLargeWeaponOptionButton(Button btn, Transform parent, Vector2 pos, Vector2 size, string richLabel)
        {
            if (btn == null) return;
            StyleForgeButton(btn, primary: true, parent, pos, size);
            var tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null) return;
            MedievalTypographyProvider.ApplyToTMP(tmp);
            var lRt = tmp.rectTransform;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(16f, 10f);
            lRt.offsetMax = new Vector2(-16f, -10f);
            tmp.richText = true;
            tmp.text = richLabel;
            tmp.fontSize = 17f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 12f;
            tmp.fontSizeMax = 18f;
            tmp.lineSpacing = 6f;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private static void StyleAnvilStepContainer(Transform stepRoot)
        {
            string sn = stepRoot.name.ToLowerInvariant();
            for (int i = stepRoot.childCount - 1; i >= 0; i--)
            {
                var ch = stepRoot.GetChild(i);
                if (ch.name.StartsWith("Text (TMP)_PlaqueBG") || (ch.name.Contains("_PlaqueBG") && !ch.name.Contains("Title_PlaqueBG")))
                    UnitySafeUtility.DestroyGameObjectSafe(ch.gameObject);
            }

            foreach (var t in stepRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<Button>(true) != null || t.GetComponentInParent<ScrollRect>(true) != null) continue;
                MedievalTypographyProvider.ApplyToTMP(t);
                var tRt = t.rectTransform;
                tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0.5f, 1f);
                tRt.anchoredPosition = new Vector2(0f, -4f);
                tRt.sizeDelta = new Vector2(396f, 38f);
                if (sn.Contains("ingot") || sn.Contains("step2")) t.text = "ELIGE UN LINGOTE METÁLICO";
                else if (sn.Contains("handle") || sn.Contains("step3")) t.text = "ELIGE UN MANGO DE ARMA";
                DecorateTitleText(t, 16.5f);
            }

            foreach (var sr in stepRoot.GetComponentsInChildren<ScrollRect>(true))
            {
                var sRt = sr.GetComponent<RectTransform>();
                sRt.localRotation = Quaternion.identity;
                sRt.localScale = Vector3.one;
                sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
                sRt.anchoredPosition = new Vector2(0f, 10f);
                sRt.sizeDelta = new Vector2(408f, 440f);

                var sImg = sr.GetComponent<Image>() ?? sr.gameObject.AddComponent<Image>();
                sImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                sImg.type = Image.Type.Sliced;
                sImg.color = new Color(0.91f, 0.86f, 0.77f, 0.95f);
            }
        }
    }
}
