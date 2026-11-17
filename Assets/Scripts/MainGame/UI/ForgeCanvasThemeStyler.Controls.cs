using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ForjaGame.UI
{
    /// <summary>
    /// Shared medieval forge volume control chassis (iron/leather plaque, crest glyph,
    /// forged amber bar, octagonal brass dial, and percentage badge) for Main Menu and Pause Menu.
    /// </summary>
    public static partial class ForgeCanvasThemeStyler
    {
        public static (Slider slider, TMP_Text valueLabel) EnsureStyledVolumeChassis(
            Transform parent, string rootName, Vector2 anchoredPos, float chassisWidth,
            float initialVolume, UnityAction<float> onChanged,
            Slider existingSlider = null, TMP_Text existingPctLabel = null)
        {
            if (parent == null) return (existingSlider, existingPctLabel);

            Transform tr = parent.Find(rootName);
            GameObject rootGo = tr != null ? tr.gameObject : new GameObject(rootName, typeof(RectTransform), typeof(Image));
            rootGo.transform.SetParent(parent, false);
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.localRotation = Quaternion.identity; rootRt.localScale = Vector3.one;
            rootRt.anchorMin = rootRt.anchorMax = rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = anchoredPos;
            rootRt.sizeDelta = new Vector2(chassisWidth, 52f);

            var chassisImg = rootGo.GetComponent<Image>() ?? rootGo.AddComponent<Image>();
            chassisImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
            chassisImg.type = Image.Type.Sliced;
            chassisImg.color = new Color(0.90f, 0.84f, 0.72f, 0.65f);
            chassisImg.raycastTarget = false;

            // Limpiar ornamentos pesados antiguos si existen
            foreach (var oldName in new[] { "VolCrestFrame", "VolPercentPlaque" })
            {
                var old = rootGo.transform.Find(oldName);
                if (old != null) old.gameObject.SetActive(false);
            }

            // Icono limpio de altavoz minimalista
            Transform iconTr = rootGo.transform.Find("VolSpeakerSimple");
            GameObject iconGo = iconTr != null ? iconTr.gameObject : new GameObject("VolSpeakerSimple", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rootGo.transform, false);
            var iRt = iconGo.GetComponent<RectTransform>();
            iRt.anchorMin = iRt.anchorMax = new Vector2(0f, 0.5f); iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(12f, 11f); iRt.sizeDelta = new Vector2(16f, 16f);
            var iImg = iconGo.GetComponent<Image>();
            iImg.sprite = BookTextureBuilder.GetSpeakerIconSprite(); iImg.preserveAspect = true; iImg.color = SubInk; iImg.raycastTarget = false;

            float padSide = 14f;
            float pctW = 48f;
            float trackW = Mathf.Max(140f, chassisWidth - padSide * 2f - pctW - 10f);

            Transform hdrTr = rootGo.transform.Find("VolumeHeader");
            GameObject hdrGo = hdrTr != null ? hdrTr.gameObject : new GameObject("VolumeHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            hdrGo.transform.SetParent(rootGo.transform, false);
            var hRt = hdrGo.GetComponent<RectTransform>();
            hRt.anchorMin = hRt.anchorMax = new Vector2(0f, 0.5f); hRt.pivot = new Vector2(0f, 0.5f);
            hRt.anchoredPosition = new Vector2(34f, 12f); hRt.sizeDelta = new Vector2(trackW, 18f);
            var hTmp = hdrGo.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(hTmp);
            hTmp.text = "Master Volume"; hTmp.fontSize = 13f; hTmp.fontStyle = FontStyles.Bold;
            hTmp.color = DarkInk; hTmp.alignment = TextAlignmentOptions.MidlineLeft; hTmp.raycastTarget = false;

            Slider sl = existingSlider != null ? existingSlider : rootGo.GetComponentInChildren<Slider>(true);
            if (sl == null) sl = CreateSliderHierarchy(rootGo.transform);
            else { sl.transform.SetParent(rootGo.transform, false); sl.gameObject.SetActive(true); }

            var slRt = sl.GetComponent<RectTransform>();
            slRt.localRotation = Quaternion.identity; slRt.localScale = Vector3.one;
            slRt.anchorMin = slRt.anchorMax = new Vector2(0f, 0.5f); slRt.pivot = new Vector2(0f, 0.5f);
            slRt.anchoredPosition = new Vector2(padSide, -10f);
            slRt.sizeDelta = new Vector2(trackW, 14f);
            EnsureSliderChildrenAligned(sl);
            sl.minValue = 0f; sl.maxValue = 1f;
            sl.SetValueWithoutNotify(Mathf.Clamp01(initialVolume));
            StyleSliderControl(sl, interactive: true);
            if (sl.handleRect != null) sl.handleRect.sizeDelta = new Vector2(18f, 18f);

            TMP_Text pctTmp = existingPctLabel;
            if (pctTmp == null)
            {
                Transform pt = rootGo.transform.Find("VolumePercentText");
                GameObject pGo = pt != null ? pt.gameObject : new GameObject("VolumePercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
                pctTmp = pGo.GetComponent<TextMeshProUGUI>();
            }
            pctTmp.transform.SetParent(rootGo.transform, false);
            var pRt = pctTmp.rectTransform;
            pRt.anchorMin = pRt.anchorMax = new Vector2(1f, 0.5f); pRt.pivot = new Vector2(1f, 0.5f);
            pRt.anchoredPosition = new Vector2(-12f, -10f); pRt.sizeDelta = new Vector2(pctW, 22f);
            MedievalTypographyProvider.ApplyToTMP(pctTmp);
            pctTmp.fontSize = 13f; pctTmp.fontStyle = FontStyles.Bold; pctTmp.color = DarkInk;
            pctTmp.alignment = TextAlignmentOptions.MidlineRight; pctTmp.raycastTarget = false;
            pctTmp.text = $"{Mathf.RoundToInt(Mathf.Clamp01(initialVolume) * 100f)}%";

            sl.onValueChanged.RemoveAllListeners();
            sl.onValueChanged.AddListener(v =>
            {
                if (pctTmp != null) pctTmp.text = $"{Mathf.RoundToInt(Mathf.Clamp01(v) * 100f)}%";
                onChanged?.Invoke(v);
            });

            return (sl, pctTmp);
        }

        private static Slider CreateSliderHierarchy(Transform parent)
        {
            var slGo = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider));
            slGo.transform.SetParent(parent, false);
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(slGo.transform, false);
            var faGo = new GameObject("Fill Area", typeof(RectTransform));
            faGo.transform.SetParent(slGo.transform, false);
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(faGo.transform, false);
            var haGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            haGo.transform.SetParent(slGo.transform, false);
            var hGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            hGo.transform.SetParent(haGo.transform, false);

            var sl = slGo.GetComponent<Slider>();
            sl.fillRect = fillGo.GetComponent<RectTransform>();
            sl.handleRect = hGo.GetComponent<RectTransform>();
            sl.targetGraphic = hGo.GetComponent<Image>();
            return sl;
        }

        private static void EnsureSliderChildrenAligned(Slider sl)
        {
            Transform bg = sl.transform.Find("Background");
            if (bg != null && bg.TryGetComponent<RectTransform>(out var bRt))
            {
                bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
                bRt.offsetMin = Vector2.zero; bRt.offsetMax = Vector2.zero;
            }
            if (sl.fillRect != null && sl.fillRect.parent is RectTransform faRt)
            {
                faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
                faRt.offsetMin = new Vector2(2f, 2f); faRt.offsetMax = new Vector2(-2f, -2f);
                sl.fillRect.offsetMin = Vector2.zero; sl.fillRect.offsetMax = Vector2.zero;
            }
            if (sl.handleRect != null && sl.handleRect.parent is RectTransform haRt)
            {
                haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
                haRt.offsetMin = new Vector2(10f, 0f); haRt.offsetMax = new Vector2(-10f, 0f);
            }
        }

        public static void StyleCloseButton(Button closeBtn, Transform parent, Vector2 anchoredPos, bool topRightCorner = false, string label = "Close")
        {
            if (closeBtn == null) return;
            if (parent != null) closeBtn.transform.SetParent(parent, false);
            var rt = closeBtn.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = topRightCorner ? new Vector2(1f, 1f) : new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = topRightCorner ? new Vector2(92f, 30f) : new Vector2(200f, 38f);

            var img = closeBtn.GetComponent<Image>() ?? closeBtn.gameObject.AddComponent<Image>();
            img.sprite = BookTextureBuilder.GetParchmentBannerSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.88f, 0.80f, 0.68f, 0.96f);
            img.raycastTarget = true;

            closeBtn.transition = Selectable.Transition.ColorTint;
            var cb = closeBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.02f, 0.92f, 1f);
            cb.pressedColor = new Color(0.80f, 0.72f, 0.62f, 1f);
            closeBtn.colors = cb;

            var tmp = closeBtn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                MedievalTypographyProvider.ApplyToTMP(tmp);
                tmp.rectTransform.anchorMin = Vector2.zero;
                tmp.rectTransform.anchorMax = Vector2.one;
                tmp.rectTransform.offsetMin = new Vector2(6f, 2f);
                tmp.rectTransform.offsetMax = new Vector2(-6f, -2f);
                tmp.text = label;
                tmp.fontSize = topRightCorner ? 12.5f : 14.5f;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 10f;
                tmp.fontSizeMax = topRightCorner ? 13f : 15f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = DarkInk;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
            }
        }
    }
}

