using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI;

namespace ForjaGame.Speedrun
{
    public partial class SecondaryDisplayLeaderboardController
    {
        private static readonly Color ColInkDark = new Color(0.15f, 0.09f, 0.05f, 1f);
        private static readonly Color ColInkMuted = new Color(0.36f, 0.25f, 0.16f, 1f);
        private static readonly Color ColGoldText = new Color(0.97f, 0.89f, 0.65f, 1f);
        private static readonly Color ColBrassSub = new Color(0.79f, 0.69f, 0.51f, 1f);
        private static readonly Color ColCrimson = new Color(0.55f, 0.09f, 0.06f, 1f);

        private void BuildSecondaryCanvas(int targetDisplayIndex = 1)
        {
            if (_secondaryCanvasRoot != null) Destroy(_secondaryCanvasRoot);
            if (_secondaryCameraObj != null) Destroy(_secondaryCameraObj);

            if (targetDisplayIndex > 0)
            {
                _secondaryCameraObj = new GameObject("SecondaryDisplayCamera", typeof(Camera));
                _secondaryCameraObj.transform.SetParent(transform, false);
                var cam = _secondaryCameraObj.GetComponent<Camera>();
                cam.targetDisplay = targetDisplayIndex;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.05f, 0.04f, 1f);
                cam.cullingMask = 0;
                cam.depth = -100;
            }

            _secondaryCanvasRoot = new GameObject("LeaderboardSecondaryDisplayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(_secondaryCanvasRoot);

            var canvas = _secondaryCanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = targetDisplayIndex;
            canvas.sortingOrder = 500;

            var scaler = _secondaryCanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Fondo de piedra de forja y tomo encuadernado en cuero + hojas apiladas + pergamino
            var bg = CreatePanel(_secondaryCanvasRoot.transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.07f, 0.05f, 0.04f, 1f), null, stretch: true);
            CreatePanel(bg, "LeatherCover", Vector2.zero, new Vector2(1848f, 1024f), Color.white, BookTextureBuilder.GetLeatherCoverSprite());
            CreatePanel(bg, "PageStack", Vector2.zero, new Vector2(1796f, 974f), Color.white, BookTextureBuilder.GetPageStackSprite());
            var board = CreatePanel(bg, "ParchmentChronicle", Vector2.zero, new Vector2(1764f, 944f), Color.white, BookTextureBuilder.GetParchmentSprite());

            BuildHeaderBar(board);
            BuildLiveTelemetryStrip(board);
            BuildPodiumRow(board);
            BuildStandingsLedger(board);
            BuildFooterBar(board);

            RefreshLeaderboardDisplay();
            UpdateLiveTelemetry();
        }

        private void BuildHeaderBar(RectTransform board)
        {
            var bar = CreatePanel(board, "HeaderPlaque", new Vector2(0f, 416f), new Vector2(1680f, 76f), Color.white, BookTextureBuilder.GetTitlePlaqueSprite());
            _filterLabelText = CreateText(bar, "FilterBadge", "VIEW: ALL RUNS", new Vector2(-650f, 0f), new Vector2(320f, 44f), 16f, TextAlignmentOptions.Left, ColBrassSub, true);
            CreateText(bar, "MainTitle", "HALL OF HEROES  -  CHRONICLES OF THE FORGE", Vector2.zero, new Vector2(960f, 54f), 30f, TextAlignmentOptions.Center, ColGoldText, true);
            CreateText(bar, "KeyHints", "[F11] FILTER   |   [F10] HIDE", new Vector2(650f, 0f), new Vector2(320f, 44f), 15f, TextAlignmentOptions.Right, ColBrassSub, false);
        }

        private void BuildLiveTelemetryStrip(RectTransform board)
        {
            var strip = CreatePanel(board, "LiveTelemetryStrip", new Vector2(0f, 316f), new Vector2(1680f, 96f), Color.white, BookTextureBuilder.GetLeatherCoverSprite());

            // Separadores verticales de laton
            CreatePanel(strip, "SepLeft", new Vector2(-265f, 0f), new Vector2(2f, 64f), new Color(0.66f, 0.52f, 0.28f, 0.45f), null);
            CreatePanel(strip, "SepRight", new Vector2(265f, 0f), new Vector2(2f, 64f), new Color(0.66f, 0.52f, 0.28f, 0.45f), null);

            // Izquierda: Pildora de estado + Equipo actual
            var badgeRt = CreatePanel(strip, "StatusBadge", new Vector2(-732f, 0f), new Vector2(146f, 40f), new Color(0.18f, 0.38f, 0.20f, 1f), BookTextureBuilder.GetParchmentBannerSprite());
            _statusBadgeImage = badgeRt.GetComponent<Image>();
            _statusBadgeText = CreateText(badgeRt, "StatusLabel", "STANDBY", Vector2.zero, new Vector2(138f, 36f), 16f, TextAlignmentOptions.Center, Color.white, true);
            _livePartyText = CreateText(strip, "LiveParty", "Iron Vanguard", new Vector2(-460f, 0f), new Vector2(360f, 66f), 20f, TextAlignmentOptions.Left, Color.white, false);

            // Centro: Cronometro gigante en vivo
            CreateText(strip, "TimerCaption", "CURRENT EXPEDITION TIME", new Vector2(0f, 27f), new Vector2(460f, 24f), 13.5f, TextAlignmentOptions.Center, ColBrassSub, true);
            _liveTimerText = CreateText(strip, "LiveTimer", "00:00.0", new Vector2(0f, -10f), new Vector2(460f, 56f), 44f, TextAlignmentOptions.Center, ColGoldText, true);

            // Derecha: Comparativa contra el Record #1
            _liveTargetText = CreateText(strip, "LiveTarget", "RECORD #1 TO BEAT: --:--.-", new Vector2(545f, 17f), new Vector2(500f, 30f), 16.5f, TextAlignmentOptions.Right, ColGoldText, true);
            _livePaceText = CreateText(strip, "LivePace", "Ready for expedition", new Vector2(545f, -16f), new Vector2(500f, 30f), 16f, TextAlignmentOptions.Right, ColBrassSub, false);
        }

        private void BuildPodiumRow(RectTransform board)
        {
            // Orden visual de podio clasico: #2 Plata (Izq), #1 Oro Campeon (Centro mas alto), #3 Bronce (Der)
            _podiumCards[1] = CreatePodiumCard(board, "Podium_2nd", new Vector2(-566f, 149f), new Vector2(524f, 176f),
                "#2  SILVER VANGUARD", new Color(0.56f, 0.60f, 0.64f, 1f), new Color(0.24f, 0.26f, 0.29f, 1f), false);

            _podiumCards[0] = CreatePodiumCard(board, "Podium_1st", new Vector2(0f, 158f), new Vector2(582f, 194f),
                "#1  GRAND CHAMPION", new Color(0.84f, 0.63f, 0.18f, 1f), new Color(0.28f, 0.18f, 0.06f, 1f), true);

            _podiumCards[2] = CreatePodiumCard(board, "Podium_3rd", new Vector2(566f, 149f), new Vector2(524f, 176f),
                "#3  BRONZE WARDEN", new Color(0.68f, 0.42f, 0.22f, 1f), new Color(0.27f, 0.16f, 0.09f, 1f), false);
        }

        private PodiumCardRefs CreatePodiumCard(RectTransform parent, string name, Vector2 pos, Vector2 size, string title, Color accentColor, Color headerBg, bool isChampion)
        {
            var outer = CreatePanel(parent, name, pos, size, accentColor, BookTextureBuilder.GetParchmentBannerSprite());
            var inner = CreatePanel(outer, "Inner", Vector2.zero, size - new Vector2(10f, 10f), Color.white, BookTextureBuilder.GetParchmentSprite());

            float halfH = (size.y - 10f) * 0.5f;
            float headerH = isChampion ? 38f : 34f;
            var ribbon = CreatePanel(inner, "Ribbon", new Vector2(0f, halfH - headerH * 0.5f), new Vector2(size.x - 14f, headerH), headerBg, null);
            CreatePanel(ribbon, "AccentLine", new Vector2(0f, -headerH * 0.5f + 2f), new Vector2(size.x - 14f, 3f), accentColor, isChampion ? BookTextureBuilder.GetSliderFillSprite() : null);
            CreateText(ribbon, "RankTitle", title, Vector2.zero, new Vector2(size.x - 24f, headerH), isChampion ? 18f : 16f, TextAlignmentOptions.Center, isChampion ? ColGoldText : new Color(0.95f, 0.92f, 0.86f, 1f), true);

            float w = size.x - 36f;
            var refs = new PodiumCardRefs
            {
                teamText = CreateText(inner, "Team", "UNCLAIMED", new Vector2(0f, isChampion ? 36f : 31f), new Vector2(w, 34f), isChampion ? 25f : 21f, TextAlignmentOptions.Center, ColInkDark, true),
                playersText = CreateText(inner, "Players", "Awaiting challengers", new Vector2(0f, isChampion ? 7f : 4f), new Vector2(w, 26f), isChampion ? 15.5f : 14f, TextAlignmentOptions.Center, ColInkMuted, false),
                timeText = CreateText(inner, "Time", "--:--.-", new Vector2(0f, isChampion ? -31f : -27f), new Vector2(w, 46f), isChampion ? 38f : 31f, TextAlignmentOptions.Center, ColCrimson, true),
                metaText = CreateText(inner, "Meta", "SOLO  |  ---", new Vector2(0f, isChampion ? -68f : -61f), new Vector2(w, 24f), 13.5f, TextAlignmentOptions.Center, ColInkMuted, true)
            };
            return refs;
        }

        private void BuildStandingsLedger(RectTransform board)
        {
            var outer = CreatePanel(board, "StandingsFrame", new Vector2(0f, -160f), new Vector2(1680f, 408f), Color.white, BookTextureBuilder.GetLeatherCoverSprite());
            var inner = CreatePanel(outer, "StandingsBody", Vector2.zero, new Vector2(1656f, 384f), Color.white, BookTextureBuilder.GetParchmentSprite());

            // Barra de encabezado de columnas
            var hdr = CreatePanel(inner, "HeaderRow", new Vector2(0f, 168f), new Vector2(1640f, 40f), new Color(0.17f, 0.11f, 0.08f, 0.98f), null);
            CreateColumnLabels(hdr, new[] { "RANK", "PARTY", "BLACKSMITHS", "MODE", "DATE", "RECORD TIME" }, ColGoldText, 15.5f, true);

            // 7 filas estructuradas para los puestos #4 al #10
            for (int i = 0; i < _ledgerRows.Length; i++)
            {
                float rowY = 122f - i * 47f;
                Color rowBg = (i % 2 == 0) ? new Color(0.54f, 0.42f, 0.28f, 0.11f) : new Color(0.54f, 0.42f, 0.28f, 0.04f);
                var row = CreatePanel(inner, $"Row_{i + 4}", new Vector2(0f, rowY), new Vector2(1640f, 44f), rowBg, null);
                CreatePanel(row, "Divider", new Vector2(0f, -21.5f), new Vector2(1632f, 1.5f), new Color(0.50f, 0.38f, 0.26f, 0.28f), null);

                _ledgerRows[i] = new LedgerRowRefs
                {
                    rankText = CreateText(row, "Rank", $"#{i + 4}", new Vector2(-755f, 0f), new Vector2(110f, 38f), 19f, TextAlignmentOptions.Center, ColInkDark, true),
                    teamText = CreateText(row, "Team", "---", new Vector2(-495f, 0f), new Vector2(390f, 38f), 19f, TextAlignmentOptions.Left, ColInkDark, true),
                    playersText = CreateText(row, "Players", "---", new Vector2(-20f, 0f), new Vector2(530f, 38f), 17f, TextAlignmentOptions.Left, ColInkMuted, false),
                    modeText = CreateText(row, "Mode", "---", new Vector2(345f, 0f), new Vector2(170f, 38f), 16.5f, TextAlignmentOptions.Center, ColInkMuted, true),
                    dateText = CreateText(row, "Date", "---", new Vector2(530f, 0f), new Vector2(180f, 38f), 15.5f, TextAlignmentOptions.Center, ColInkMuted, false),
                    timeText = CreateText(row, "Time", "--:--.-", new Vector2(715f, 0f), new Vector2(175f, 38f), 21f, TextAlignmentOptions.Right, ColCrimson, true)
                };
            }
        }

        private void BuildFooterBar(RectTransform board)
        {
            var footer = CreatePanel(board, "FooterPlaque", new Vector2(0f, -416f), new Vector2(1680f, 62f), Color.white, BookTextureBuilder.GetTitlePlaqueSprite());
            _footerTotalsText = CreateText(footer, "Totals", "TOTAL RUNS: 0   |   SOLO: 0   |   LAN: 0", new Vector2(-520f, 0f), new Vector2(580f, 40f), 16.5f, TextAlignmentOptions.Left, ColGoldText, true);
            _footerBestSoloText = CreateText(footer, "BestSolo", "BEST SOLO: --:--.-", Vector2.zero, new Vector2(460f, 40f), 16.5f, TextAlignmentOptions.Center, ColBrassSub, true);
            _footerBestLanText = CreateText(footer, "BestLan", "BEST LAN: --:--.-", new Vector2(520f, 0f), new Vector2(580f, 40f), 16.5f, TextAlignmentOptions.Right, ColBrassSub, true);
        }

        private void CreateColumnLabels(RectTransform row, string[] labels, Color color, float size, bool bold)
        {
            CreateText(row, "ColRank", labels[0], new Vector2(-755f, 0f), new Vector2(110f, 36f), size, TextAlignmentOptions.Center, color, bold);
            CreateText(row, "ColParty", labels[1], new Vector2(-495f, 0f), new Vector2(390f, 36f), size, TextAlignmentOptions.Left, color, bold);
            CreateText(row, "ColSmiths", labels[2], new Vector2(-20f, 0f), new Vector2(530f, 36f), size, TextAlignmentOptions.Left, color, bold);
            CreateText(row, "ColMode", labels[3], new Vector2(345f, 0f), new Vector2(170f, 36f), size, TextAlignmentOptions.Center, color, bold);
            CreateText(row, "ColDate", labels[4], new Vector2(530f, 0f), new Vector2(180f, 36f), size, TextAlignmentOptions.Center, color, bold);
            CreateText(row, "ColTime", labels[5], new Vector2(715f, 0f), new Vector2(175f, 36f), size, TextAlignmentOptions.Right, color, bold);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite sprite, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
            return rt;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, Color color, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = MedievalTypographyProvider.SanitizeForTMP(text);
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            MedievalTypographyProvider.ApplyToTMP(tmp);
            return tmp;
        }
    }
}

