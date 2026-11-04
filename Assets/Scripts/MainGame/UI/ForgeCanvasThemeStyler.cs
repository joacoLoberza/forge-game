using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Sistema global de estilo visual para todas las estaciones de forja, sliders y títulos del Canvas.
    /// Unifica la estética de cuero repujado, pergamino crema, placas de latón/oro y tinta sepia oscura.
    /// </summary>
    public static partial class ForgeCanvasThemeStyler
    {
        public static readonly Color DarkInk = new Color(0.17f, 0.11f, 0.06f, 1f);
        public static readonly Color SubInk = new Color(0.34f, 0.22f, 0.12f, 1f);
        public static readonly Color GoldTitleInk = new Color(0.99f, 0.90f, 0.64f, 1f);
        private static bool _sceneHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitGlobalTheme()
        {
            if (!_sceneHooked)
            {
                _sceneHooked = true;
                SceneManager.sceneLoaded += (_, _) => ApplyGlobalCanvasTheme();
            }
            ApplyGlobalCanvasTheme();
        }

        public static void ApplyGlobalCanvasTheme()
        {
            ConfigureAllCanvasScalers();

            var allSliders = Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sl in allSliders)
            {
                if (sl == null) continue;
                bool isThermal = sl.name.ToLowerInvariant().Contains("temp");
                StyleSliderControl(sl, sl.interactable, isThermal);
            }

            var allScrolls = Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sr in allScrolls) ForgeScrollViewStyler.StyleScrollRectVisuals(sr);

            var furnace = Object.FindFirstObjectByType<FurnacePanel>(FindObjectsInactive.Include);
            if (furnace != null) StyleFurnacePanel(furnace);
            var press = Object.FindFirstObjectByType<PressPanel>(FindObjectsInactive.Include);
            if (press != null) StylePressPanel(press);
            var anvil = Object.FindFirstObjectByType<AnvilPanel>(FindObjectsInactive.Include);
            if (anvil != null) StyleAnvilPanel(anvil);

            StyleEndScreensAndMainMenu();
            MedievalTypographyProvider.ApplyToAllSceneTMP();
        }

        public static void StretchPanelRoot(Transform tr)
        {
            if (tr == null) return;
            var rt = tr as RectTransform ?? tr.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        public static void ConfigureAllCanvasScalers()
        {
            var scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cs in scalers)
            {
                if (cs == null) continue;
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920f, 1080f);
                cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                cs.matchWidthOrHeight = 0.5f;
            }
        }

        public static RectTransform CreateOrGetParchmentCard(Transform parent, string cardName, Vector2 anchoredPos, Vector2 size)
        {
            Transform existing = parent.Find(cardName);
            GameObject outer = existing != null ? existing.gameObject : new GameObject(cardName, typeof(RectTransform), typeof(Image));
            outer.transform.SetParent(parent, false);

            var oRt = outer.GetComponent<RectTransform>();
            oRt.localRotation = Quaternion.identity; oRt.localScale = Vector3.one;
            oRt.anchorMin = oRt.anchorMax = oRt.pivot = new Vector2(0.5f, 0.5f);
            oRt.anchoredPosition = anchoredPos; oRt.sizeDelta = size;

            var oImg = outer.GetComponent<Image>();
            oImg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
            oImg.type = Image.Type.Sliced;
            oImg.color = new Color(0.18f, 0.12f, 0.08f, 0.98f);
            oImg.raycastTarget = true;

            Transform bodyTr = outer.transform.Find("ParchmentBody");
            GameObject body = bodyTr != null ? bodyTr.gameObject : new GameObject("ParchmentBody", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(outer.transform, false);

            var bRt = body.GetComponent<RectTransform>();
            bRt.localRotation = Quaternion.identity; bRt.localScale = Vector3.one;
            bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
            bRt.offsetMin = new Vector2(14f, 14f); bRt.offsetMax = new Vector2(-14f, -14f);

            var bImg = body.GetComponent<Image>();
            bImg.sprite = BookTextureBuilder.GetParchmentSprite();
            bImg.type = Image.Type.Sliced;
            bImg.color = new Color(0.97f, 0.94f, 0.87f, 0.99f);
            bImg.raycastTarget = false;
            return bRt;
        }

        public static RectTransform CreateOrGetTitleBanner(Transform parent, string objName, string titleText, Vector2 anchoredPos, Vector2 size, float fontSize = 21f, bool topAnchored = false)
        {
            Transform existing = parent.Find(objName);
            GameObject plaque = existing != null ? existing.gameObject : new GameObject(objName, typeof(RectTransform), typeof(Image));
            plaque.transform.SetParent(parent, false);

            var rt = plaque.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = plaque.GetComponent<Image>();
            img.sprite = BookTextureBuilder.GetTitlePlaqueSprite();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;

            Transform lblTr = plaque.transform.Find("TitleLabel");
            GameObject lblObj = lblTr != null ? lblTr.gameObject : new GameObject("TitleLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(plaque.transform, false);

            var lRt = lblObj.GetComponent<RectTransform>();
            lRt.localRotation = Quaternion.identity;
            lRt.localScale = Vector3.one;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(12f, 4f);
            lRt.offsetMax = new Vector2(-12f, -4f);

            var tmp = lblObj.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(tmp);
            tmp.text = titleText; tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            tmp.color = GoldTitleInk; tmp.alignment = TextAlignmentOptions.Center;
            tmp.margin = Vector4.zero; tmp.enableAutoSizing = false; tmp.raycastTarget = false;
            return rt;
        }

        public static void DecorateTitleText(TMP_Text txt, float fontSize = 20f)
        {
            if (txt == null || txt.GetComponentInParent<Button>(true) != null) return;
            var rt = txt.rectTransform;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            if (rt.sizeDelta.y < 34f) rt.sizeDelta = new Vector2(Mathf.Max(260f, rt.sizeDelta.x), 38f);

            txt.fontSize = fontSize;
            txt.enableAutoSizing = false;
            txt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            txt.color = GoldTitleInk;
            txt.alignment = TextAlignmentOptions.Center;
            txt.margin = Vector4.zero;
            txt.raycastTarget = false;

            Transform bg = txt.transform.Find("TitlePlaque_BG");
            if (bg == null && txt.transform.parent != null)
                bg = txt.transform.parent.Find($"{txt.name}_PlaqueBG");

            if (bg == null && txt.transform.parent != null && !txt.name.StartsWith("Text (TMP)"))
            {
                var bgObj = new GameObject($"{txt.name}_PlaqueBG", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(txt.transform.parent, false);
                bg = bgObj.transform;
            }

            if (bg != null)
            {
                var bgRt = bg.GetComponent<RectTransform>();
                bgRt.anchorMin = rt.anchorMin;
                bgRt.anchorMax = rt.anchorMax;
                bgRt.pivot = rt.pivot;
                bgRt.anchoredPosition = rt.anchoredPosition;
                bgRt.sizeDelta = rt.sizeDelta + new Vector2(24f, 8f);
                bgRt.localRotation = Quaternion.identity;
                bgRt.localScale = Vector3.one;

                var img = bg.GetComponent<Image>();
                img.sprite = BookTextureBuilder.GetTitlePlaqueSprite();
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                img.raycastTarget = false;
                bg.SetSiblingIndex(Mathf.Max(0, txt.transform.GetSiblingIndex()));
                txt.transform.SetSiblingIndex(bg.GetSiblingIndex() + 1);
            }
        }

        public static void StyleSliderControl(Slider slider, bool interactive, bool isThermalGauge = false)
        {
            if (slider == null) return;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = interactive;

            var sRt = slider.GetComponent<RectTransform>();
            if (sRt != null)
            {
                sRt.localRotation = Quaternion.identity;
                sRt.localScale = Vector3.one;
            }

            Transform bgTr = slider.transform.Find("Background");
            if (bgTr != null && bgTr.TryGetComponent<Image>(out var bgImg))
            {
                bgImg.sprite = BookTextureBuilder.GetSliderTrackSprite(); bgImg.type = Image.Type.Sliced; bgImg.color = Color.white;
                var bgRt = bgTr.GetComponent<RectTransform>();
                bgRt.localRotation = Quaternion.identity; bgRt.localScale = Vector3.one;
                bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            }

            if (slider.fillRect != null && slider.fillRect.TryGetComponent<Image>(out var fillImg))
            {
                slider.fillRect.localRotation = Quaternion.identity; slider.fillRect.localScale = Vector3.one;
                fillImg.sprite = isThermalGauge ? null : BookTextureBuilder.GetSliderFillSprite();
                fillImg.type = Image.Type.Sliced; fillImg.color = isThermalGauge ? Color.clear : Color.white;
                fillImg.raycastTarget = !isThermalGauge;
            }

            if (slider.handleRect != null && slider.handleRect.TryGetComponent<Image>(out var hImg))
            {
                slider.handleRect.localRotation = Quaternion.identity; slider.handleRect.localScale = Vector3.one;
                hImg.sprite = isThermalGauge ? BookTextureBuilder.GetThermalNeedleSprite() : BookTextureBuilder.GetSliderKnobSprite();
                hImg.type = Image.Type.Simple; hImg.color = (isThermalGauge || interactive) ? Color.white : Color.clear;
                slider.handleRect.sizeDelta = isThermalGauge ? new Vector2(16f, 48f) : new Vector2(28f, 28f);
                if (isThermalGauge && slider.handleRect.parent != null) slider.handleRect.parent.SetAsLastSibling();
            }
        }
    }
}
