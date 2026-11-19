using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.Speedrun;

namespace ForjaGame.UI
{
    /// <summary>
    /// Constructor reutilizable de tabla estructurada de Hall of Heroes para paginas de libro
    /// (Menu Principal y Pantallas de Victoria/Derrota), con columnas fijas, cabecera de madera
    /// oscura y filas destacadas en oro/plata/bronce coherentes con el tablero de Display 2.
    /// </summary>
    public static class CompactLeaderboardTableBuilder
    {
        private static readonly Color HeaderBg = new Color(0.18f, 0.11f, 0.08f, 0.98f);
        private static readonly Color ColGold = new Color(0.78f, 0.56f, 0.10f, 1f);
        private static readonly Color ColSilver = new Color(0.46f, 0.50f, 0.54f, 1f);
        private static readonly Color ColBronze = new Color(0.62f, 0.36f, 0.16f, 1f);
        private static readonly Color ColCrimson = new Color(0.55f, 0.09f, 0.06f, 1f);
        private static readonly Color ColLan = new Color(0.12f, 0.40f, 0.20f, 1f);

        public static void Render(RectTransform container, List<SpeedrunRecord> records, int maxRows = 7)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var ch = container.GetChild(i);
                ch.gameObject.SetActive(false);
                Object.Destroy(ch.gameObject);
            }

            float availW = container.rect.width;
            float availH = container.rect.height;
            if (availW <= 10f || availH <= 10f)
            {
                var parentRt = container.parent as RectTransform;
                if (parentRt != null && parentRt.sizeDelta.x > 40f && parentRt.sizeDelta.y > 40f)
                {
                    availW = parentRt.sizeDelta.x - 28f;
                    availH = parentRt.sizeDelta.y - 28f;
                }
                else
                {
                    availW = Mathf.Max(310f, container.sizeDelta.x);
                    availH = Mathf.Max(220f, container.sizeDelta.y);
                }
            }
            float rowW = availW - 8f;
            float topY = availH * 0.5f - 16f;

            // Barra de encabezado con columnas reales
            var hdr = CreateBox(container, "LdHeader", new Vector2(0f, topY), new Vector2(rowW, 24f), HeaderBg);
            CreateRowTexts(hdr, rowW, "#", "PARTY", "BLACKSMITHS", "TIME",
                ForgeCanvasThemeStyler.GoldTitleInk, ForgeCanvasThemeStyler.GoldTitleInk,
                ForgeCanvasThemeStyler.GoldTitleInk, ForgeCanvasThemeStyler.GoldTitleInk, 10.5f, true);

            if (records == null || records.Count == 0)
            {
                var emptyGo = new GameObject("EmptyNotice", typeof(RectTransform), typeof(TextMeshProUGUI));
                emptyGo.transform.SetParent(container, false);
                var eRt = emptyGo.GetComponent<RectTransform>();
                eRt.anchorMin = eRt.anchorMax = eRt.pivot = new Vector2(0.5f, 0.5f);
                eRt.anchoredPosition = new Vector2(0f, -12f);
                eRt.sizeDelta = new Vector2(rowW - 24f, availH - 50f);
                var tmp = emptyGo.GetComponent<TextMeshProUGUI>();
                tmp.text = MedievalTypographyProvider.SanitizeForTMP(
                    "<b>No expeditions recorded in this category yet.</b>\n\n<size=11>Consecrate the Deep Sanctum Altar to engrave your party in glory.</size>");
                tmp.fontSize = 12.5f;
                tmp.color = ForgeCanvasThemeStyler.SubInk;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;
                MedievalTypographyProvider.ApplyToTMP(tmp);
                return;
            }

            int count = Mathf.Min(maxRows, records.Count);
            float bodyH = Mathf.Max(120f, availH - 36f);
            float rowStep = Mathf.Min(33f, bodyH / Mathf.Max(1, maxRows));
            float rowH = Mathf.Max(22f, rowStep - 2.5f);
            float firstRowY = topY - 14f - rowH * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var r = records[i];
                float y = firstRowY - i * rowStep;

                Color bgCol = i switch
                {
                    0 => new Color(0.86f, 0.67f, 0.20f, 0.22f),
                    1 => new Color(0.58f, 0.62f, 0.66f, 0.18f),
                    2 => new Color(0.72f, 0.45f, 0.22f, 0.18f),
                    _ => (i % 2 == 0) ? new Color(0.54f, 0.42f, 0.28f, 0.11f) : new Color(0.54f, 0.42f, 0.28f, 0.04f)
                };

                var row = CreateBox(container, $"LdRow_{i + 1}", new Vector2(0f, y), new Vector2(rowW, rowH), bgCol);
                CreateBox(row, "Div", new Vector2(0f, -rowH * 0.5f + 0.75f), new Vector2(rowW, 1f), new Color(0.50f, 0.38f, 0.26f, 0.24f));

                if (i < 3)
                {
                    Color stripe = i == 0 ? ColGold : (i == 1 ? ColSilver : ColBronze);
                    CreateBox(row, "MedalBar", new Vector2(-rowW * 0.5f + 2f, 0f), new Vector2(3.5f, rowH - 2f), stripe);
                }

                Color rankCol = i == 0 ? ColGold : (i == 1 ? ColSilver : (i == 2 ? ColBronze : ForgeCanvasThemeStyler.DarkInk));
                string rankStr = $"#{i + 1}";
                string teamStr = Truncate(r.teamName, 14);
                string smithStr = r.isMultiplayer ? $"[LAN] {Truncate(r.playerNames, 11)}" : Truncate(r.playerNames, 15);
                Color smithCol = r.isMultiplayer ? ColLan : ForgeCanvasThemeStyler.SubInk;

                CreateRowTexts(row, rowW, rankStr, teamStr, smithStr, r.formattedTime,
                    rankCol, ForgeCanvasThemeStyler.DarkInk, smithCol, ColCrimson, 11.2f, i < 3);
            }
        }

        private static void CreateRowTexts(
            RectTransform row, float rowW,
            string rank, string party, string smiths, string time,
            Color cRank, Color cParty, Color cSmiths, Color cTime,
            float fontSize, bool boldParty)
        {
            float half = rowW * 0.5f;
            float wRank = 36f;
            float wTime = 68f;
            float rem = rowW - wRank - wTime - 12f;
            float wParty = rem * 0.50f;
            float wSmith = rem * 0.50f;

            float xRank = -half + 4f + wRank * 0.5f;
            float xParty = -half + 6f + wRank + wParty * 0.5f;
            float xSmith = xParty + wParty * 0.5f + 4f + wSmith * 0.5f;
            float xTime = half - 4f - wTime * 0.5f;

            CreateCell(row, "Rank", rank, xRank, wRank, fontSize, TextAlignmentOptions.Center, cRank, true);
            CreateCell(row, "Party", party, xParty, wParty, fontSize, TextAlignmentOptions.Left, cParty, boldParty);
            CreateCell(row, "Smiths", smiths, xSmith, wSmith, fontSize - 0.5f, TextAlignmentOptions.Left, cSmiths, false);
            CreateCell(row, "Time", time, xTime, wTime, fontSize, TextAlignmentOptions.Right, cTime, true);
        }

        private static RectTransform CreateBox(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static void CreateCell(Transform parent, string name, string value, float x, float width, float fontSize, TextAlignmentOptions align, Color color, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(width, 24f);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = MedievalTypographyProvider.SanitizeForTMP(value);
            tmp.fontSize = fontSize;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            MedievalTypographyProvider.ApplyToTMP(tmp);
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return "---";
            string s = value.Trim();
            return s.Length <= max ? s : s.Substring(0, max - 2) + "..";
        }
    }
}
