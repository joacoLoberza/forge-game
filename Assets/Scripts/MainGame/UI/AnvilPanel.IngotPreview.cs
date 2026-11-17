using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.Forge;
using ForjaGame.Inventory;
using ForjaGame.Data;

namespace ForjaGame.UI
{
    public partial class AnvilPanel
    {
        private IngotData _previewSelectedIngot;
        private TMP_Text _ingotPreviewDetailsText;
        private Button _confirmSelectedIngotButton;

        private void RefreshIngotsList()
        {
            if (ingotsContainer == null) return;
            Transform targetContent = ForgeScrollViewStyler.ResolveContentContainer(ingotsContainer);
            ForgeScrollViewStyler.ConfigureFixedScrollView(targetContent);
            foreach (Transform child in targetContent) Destroy(child.gameObject);

            EnsureIngotPreviewCard();

            IReadOnlyList<IngotData> ingots = GetCurrentPlayerInventory()?.Ingots;
            bool hasIngots = ingots != null && ingots.Count > 0;
            if (emptyIngotsNotice != null)
            {
                ForgeScrollViewStyler.StyleEmptyNotice(emptyIngotsNotice, "No ingots in inventory.\nCompress a bloom in the mechanical press!");
                emptyIngotsNotice.SetActive(!hasIngots);
            }
            if (!hasIngots)
            {
                _previewSelectedIngot = null;
                UpdateIngotPreviewCard();
                if (emptyIngotsNotice == null)
                    ForgeScrollViewStyler.CreateNoticeEntry(targetContent, "No ingots in inventory.\nCompress a bloom in the mechanical press!");
                return;
            }

            if (_previewSelectedIngot == null || !ContainsIngot(ingots, _previewSelectedIngot))
                _previewSelectedIngot = ingots[0];

            for (int i = 0; i < ingots.Count; i++)
            {
                IngotData ingot = ingots[i];
                bool isSel = ingot == _previewSelectedIngot;
                string label = $"{ingot.DisplayName} ({ingot.SizeLabel}) • Purity: {ingot.PurityPercentage}%";
                Color metalColor = ingot.composition != null ? MetalProperties.GetMetalColor(ingot.composition.CalculateWeightedProperties().metalType) : Color.gray;
                Sprite icon = defaultIngotSprite ?? ForgeSpriteDatabase.Instance?.defaultIngotSprite ?? ProceduralItemIconProvider.GetItemIcon(ingot.DisplayName, "Ingot");
                ForgeScrollViewStyler.CreateRowEntry(targetContent, label, icon, metalColor, isSel, () => OnIngotRowClicked(ingot));
            }
            UpdateIngotPreviewCard();
        }

        private static bool ContainsIngot(IReadOnlyList<IngotData> list, IngotData item)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == item) return true;
            return false;
        }

        private void OnIngotRowClicked(IngotData ingot)
        {
            if (ingot == null) return;
            if (_previewSelectedIngot == ingot)
            {
                SelectIngot(ingot);
                return;
            }
            ForjaGame.Audio.ForgeAudioSystem.PlayItemSelect();
            _previewSelectedIngot = ingot;
            RefreshIngotsList();
        }

        private void EnsureIngotPreviewCard()
        {
            if (step2IngotSelection == null) return;
            Transform stepTr = step2IngotSelection.transform;
            var scroll = stepTr.GetComponentInChildren<ScrollRect>(true);
            if (scroll != null)
            {
                var sRt = scroll.GetComponent<RectTransform>();
                sRt.anchoredPosition = new Vector2(0f, 88f);
                sRt.sizeDelta = new Vector2(408f, 300f);
            }

            Transform cardTr = stepTr.Find("IngotPreviewCard");
            if (cardTr == null)
            {
                var cardGo = new GameObject("IngotPreviewCard", typeof(RectTransform), typeof(Image));
                cardGo.transform.SetParent(stepTr, false);
                var cRt = cardGo.GetComponent<RectTransform>();
                cRt.anchorMin = cRt.anchorMax = cRt.pivot = new Vector2(0.5f, 0.5f);
                cRt.anchoredPosition = new Vector2(0f, -152f);
                cRt.sizeDelta = new Vector2(408f, 152f);
                var cImg = cardGo.GetComponent<Image>();
                cImg.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                cImg.type = Image.Type.Sliced;
                cImg.color = new Color(0.92f, 0.87f, 0.77f, 0.98f);

                var txtGo = new GameObject("IngotPreviewDesc", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(cardGo.transform, false);
                var tRt = txtGo.GetComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0f, 0.34f); tRt.anchorMax = new Vector2(1f, 1f);
                tRt.offsetMin = new Vector2(12f, 4f); tRt.offsetMax = new Vector2(-12f, -6f);
                _ingotPreviewDetailsText = txtGo.GetComponent<TextMeshProUGUI>();
                MedievalTypographyProvider.ApplyToTMP(_ingotPreviewDetailsText);
                _ingotPreviewDetailsText.fontSize = 12.5f;
                _ingotPreviewDetailsText.enableAutoSizing = true;
                _ingotPreviewDetailsText.fontSizeMin = 10f;
                _ingotPreviewDetailsText.fontSizeMax = 13f;
                _ingotPreviewDetailsText.color = ForgeCanvasThemeStyler.DarkInk;
                _ingotPreviewDetailsText.alignment = TextAlignmentOptions.TopLeft;

                var btnGo = new GameObject("BtnUseSelectedIngot", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(cardGo.transform, false);
                var lblGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGo.transform.SetParent(btnGo.transform, false);
                var lRt = lblGo.GetComponent<RectTransform>();
                lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one; lRt.offsetMin = lRt.offsetMax = Vector2.zero;

                _confirmSelectedIngotButton = btnGo.GetComponent<Button>();
                ForgeCanvasThemeStyler.StyleForgeButton(_confirmSelectedIngotButton, primary: true, cardGo.transform, new Vector2(0f, -48f), new Vector2(360f, 38f));
                lblGo.GetComponent<TextMeshProUGUI>().text = "PLACE SELECTED INGOT";
                BindPressDown(_confirmSelectedIngotButton, () => { if (_previewSelectedIngot != null) SelectIngot(_previewSelectedIngot); });
                cardTr = cardGo.transform;
            }
            else
            {
                if (_ingotPreviewDetailsText == null) _ingotPreviewDetailsText = cardTr.Find("IngotPreviewDesc")?.GetComponent<TMP_Text>();
                if (_confirmSelectedIngotButton == null) _confirmSelectedIngotButton = cardTr.Find("BtnUseSelectedIngot")?.GetComponent<Button>();
            }
        }

        private void UpdateIngotPreviewCard()
        {
            if (_ingotPreviewDetailsText == null) return;
            if (_previewSelectedIngot == null)
            {
                _ingotPreviewDetailsText.text = "<i>No ingot selected. Compact a bloom at the Mechanical Press first.</i>";
                if (_confirmSelectedIngotButton != null) _confirmSelectedIngotButton.interactable = false;
                return;
            }
            string comp = _previewSelectedIngot.composition != null ? _previewSelectedIngot.composition.GetCompositionDescription() : "Standard Alloy";
            _ingotPreviewDetailsText.text =
                $"<b>{_previewSelectedIngot.DisplayName}</b> ({_previewSelectedIngot.SizeLabel})\n" +
                $"Purity: <color={_previewSelectedIngot.PurityColorHex}><b>{_previewSelectedIngot.PurityPercentage}%</b> [{_previewSelectedIngot.PurityRank}]</color>\n{comp}";
            if (_confirmSelectedIngotButton != null) _confirmSelectedIngotButton.interactable = true;
        }
    }
}

