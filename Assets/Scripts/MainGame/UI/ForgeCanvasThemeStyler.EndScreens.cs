using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ForjaGame.Core;
using ForjaGame.Speedrun;

namespace ForjaGame.UI
{
    public static partial class ForgeCanvasThemeStyler
    {
        public static void StyleEndScreensAndMainMenu()
        {
            GameObject victoryObj = null, defeatObj = null;
            var glc = Object.FindFirstObjectByType<GameLoopController>(FindObjectsInactive.Include);
            if (glc != null) { victoryObj = glc.VictoryPanel; defeatObj = glc.DefeatPanel; }

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c == null) continue;
                foreach (var tr in c.GetComponentsInChildren<Transform>(true))
                {
                    if (victoryObj == null && (tr.name == "PanelVictory" || tr.name == "VictoryPanel")) victoryObj = tr.gameObject;
                    if (defeatObj == null && (tr.name == "PanelDefeat" || tr.name == "DefeatPanel")) defeatObj = tr.gameObject;
                }
            }

            if (victoryObj != null) StyleDecreeModal(victoryObj, "SANCTUM OF THE DEPTHS", true);
            if (defeatObj != null) StyleDecreeModal(defeatObj, "FALLEN IN THE CAVERN", false);
        }

        private static LeaderboardFilterMode _victoryLdFilter = LeaderboardFilterMode.All;

        private static void StyleDecreeModal(GameObject modal, string title, bool victory)
        {
            if (modal == null) return;
            var glc = Object.FindFirstObjectByType<GameLoopController>(FindObjectsInactive.Include);

            var mRt = modal.GetComponent<RectTransform>();
            if (mRt != null)
            {
                mRt.anchorMin = Vector2.zero; mRt.anchorMax = Vector2.one;
                mRt.offsetMin = mRt.offsetMax = Vector2.zero;
                mRt.pivot = new Vector2(0.5f, 0.5f);
            }

            var bgImg = modal.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = null;
                bgImg.color = victory ? new Color(0.03f, 0.02f, 0.01f, 0.94f) : new Color(0.06f, 0.02f, 0.02f, 0.92f);
            }

            for (int i = modal.transform.childCount - 1; i >= 0; i--)
            {
                var ch = modal.transform.GetChild(i);
                if (ch.name != "BookJournalRoot")
                {
                    ch.gameObject.SetActive(false);
                    ch.localScale = Vector3.zero;
                    ch.SetParent(null);
                    UnitySafeUtility.DestroyGameObjectSafe(ch.gameObject);
                }
            }

            Transform existingBook = modal.transform.Find("BookJournalRoot");
            GameObject bookGo = existingBook != null ? existingBook.gameObject : new GameObject("BookJournalRoot", typeof(RectTransform), typeof(Image));
            bookGo.transform.SetParent(modal.transform, false);
            var (leftPage, rightPage) = BookJournalTemplateBuilder.EnsureOpenBookShell(bookGo.transform, resetRootRect: true, bookScale: 1.24f);

            ClearPageContents(leftPage);
            ClearPageContents(rightPage);

            BuildEndScreenLeftPage(leftPage, title, victory, glc);
            BuildEndScreenRightPage(rightPage, victory, glc);

            var closeTab = BookJournalTemplateBuilder.CreateOrGetCloseBookmarkTab(
                bookGo.transform,
                "BtnEndBookCloseTab",
                victory ? "Resume" : "Workshop",
                () => { if (victory) glc?.AcceptVictory(); else glc?.AcceptDefeat(); });
            closeTab.gameObject.SetActive(true);

            MedievalTypographyProvider.ApplyToAllTMPInHierarchy(modal.transform);
        }

        private static void ClearPageContents(RectTransform page)
        {
            if (page == null) return;
            for (int i = page.childCount - 1; i >= 0; i--)
            {
                var ch = page.GetChild(i);
                ch.gameObject.SetActive(false);
                ch.SetParent(null);
                UnitySafeUtility.DestroyGameObjectSafe(ch.gameObject);
            }
        }

        private static void BuildEndScreenLeftPage(RectTransform leftPage, string title, bool victory, GameLoopController glc)
        {
            CreateOrGetTitleBanner(leftPage, "DecreeTitle", title, new Vector2(0f, 178f), new Vector2(358f, 46f), fontSize: 17.5f, topAnchored: false);

            string outcome = GameLoopController.LastOutcomeSummary;
            if (victory)
            {
                var timer = DelveRunTimerHUD.Instance;
                string plaqueText = (timer != null && timer.HasWon)
                    ? $"COMPLETION TIME:  {DelveLeaderboardDataSO.FormatSeconds(timer.FinalTime)}"
                    : "ANCIENT SANCTUM CONSECRATED";
                CreateOrGetTitleBanner(leftPage, "VictoryTimePlaque", plaqueText, new Vector2(0f, 122f), new Vector2(354f, 40f), fontSize: 15f, topAnchored: false);

                var card = BookJournalTemplateBuilder.CreateOrGetPageInset(leftPage, "DecreeSummaryCard", new Vector2(0f, 6f), new Vector2(356f, 168f), new Color(0.92f, 0.86f, 0.75f, 0.82f));
                string extra = !string.IsNullOrEmpty(outcome)
                    ? $"\n\n<size=12.5><color=#1A0F08><b>Sacred Consecration Reward:</b></color>\n<color=#5A2D10>{outcome}</color></size>" : "";
                CreateDecreeText(card, "<size=15.5><b>Glory to the Forge!</b></size>\n<color=#5A3212>The Titan has fallen and your harvested ores are blessed by the sacred flame.</color>" + extra, new Vector2(328f, 146f));

                var btnKeep = EnsureNamedButton(leftPage, "BtnKeepPlaying", "CONTINUE PLAYING", new Vector2(0f, -118f), new Vector2(304f, 46f), true);
                btnKeep.onClick.RemoveAllListeners(); btnKeep.onClick.AddListener(() => glc?.AcceptVictory());

                var btnMenu = EnsureNamedButton(leftPage, "BtnReturnMenu", "MAIN MENU", new Vector2(0f, -172f), new Vector2(304f, 40f), false);
                btnMenu.onClick.RemoveAllListeners(); btnMenu.onClick.AddListener(() => glc?.ReturnToMainMenu());
            }
            else
            {
                CreateOrGetTitleBanner(leftPage, "DefeatSubPlaque", "EXPEDITION INTERRUPTED", new Vector2(0f, 122f), new Vector2(354f, 36f), fontSize: 14f, topAnchored: false);

                var card = BookJournalTemplateBuilder.CreateOrGetPageInset(leftPage, "DecreeSummaryCard", new Vector2(0f, 4f), new Vector2(356f, 174f), new Color(0.92f, 0.85f, 0.74f, 0.82f));
                string lost = !string.IsNullOrEmpty(outcome) ? $"\n\n<size=12.5><color=#7A1818><b>{outcome}</b></color></size>" : "";
                CreateDecreeText(card, "<size=15.5><b>Your strength faltered in the depths.</b></size>\n<color=#5A3212>You awaken beside the workshop forge while dropped ores await recovery below.</color>" + lost, new Vector2(328f, 152f));

                var btnAccept = EnsureNamedButton(leftPage, "BtnDefeatAccept", "AWAKEN AT WORKSHOP", new Vector2(0f, -140f), new Vector2(310f, 48f), true);
                btnAccept.onClick.RemoveAllListeners(); btnAccept.onClick.AddListener(() => glc?.AcceptDefeat());
            }
        }

        private static void CreateDecreeText(RectTransform parent, string richText, Vector2 size)
        {
            var msgGo = new GameObject("DecreeMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
            msgGo.transform.SetParent(parent, false);
            var msgRt = msgGo.GetComponent<RectTransform>();
            msgRt.anchorMin = msgRt.anchorMax = msgRt.pivot = new Vector2(0.5f, 0.5f);
            msgRt.anchoredPosition = Vector2.zero; msgRt.sizeDelta = size;
            var mTmp = msgGo.GetComponent<TextMeshProUGUI>();
            mTmp.text = MedievalTypographyProvider.SanitizeForTMP(richText);
            mTmp.fontSize = 14f; mTmp.enableAutoSizing = true; mTmp.fontSizeMin = 10.5f; mTmp.fontSizeMax = 15f;
            mTmp.color = DarkInk; mTmp.alignment = TextAlignmentOptions.Center;
            mTmp.textWrappingMode = TextWrappingModes.Normal;
            mTmp.raycastTarget = false;
            MedievalTypographyProvider.ApplyToTMP(mTmp);
        }

        private static void BuildEndScreenRightPage(RectTransform rightPage, bool victory, GameLoopController glc)
        {
            CreateOrGetTitleBanner(rightPage, "LdTitle", "HALL OF HEROES", new Vector2(0f, 178f), new Vector2(358f, 46f), fontSize: 17.5f, topAnchored: false);

            var subGo = new GameObject("LdSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(rightPage, false);
            var sRt = subGo.GetComponent<RectTransform>();
            sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = new Vector2(0f, 140f); sRt.sizeDelta = new Vector2(354f, 22f);
            var sTmp = subGo.GetComponent<TextMeshProUGUI>();
            sTmp.text = victory ? "<i>Speedrun records of the consecrated sanctum</i>" : "<i>Fastest delves etched in the guild tome</i>";
            sTmp.fontSize = 11.5f; sTmp.color = SubInk; sTmp.fontStyle = FontStyles.Italic; sTmp.alignment = TextAlignmentOptions.Center;
            sTmp.raycastTarget = false;
            MedievalTypographyProvider.ApplyToTMP(sTmp);

            var bAll = EnsureNamedButton(rightPage, "BtnFilterAll", "ALL", new Vector2(-116f, 108f), new Vector2(106f, 32f), true);
            var bSolo = EnsureNamedButton(rightPage, "BtnFilterSolo", "SOLO", new Vector2(0f, 108f), new Vector2(106f, 32f), false);
            var bLan = EnsureNamedButton(rightPage, "BtnFilterLan", "LAN", new Vector2(116f, 108f), new Vector2(106f, 32f), false);

            float tableH = victory ? 286f : 254f;
            float tableY = victory ? -56f : -40f;
            var tableInset = CreateOrGetParchmentCard(rightPage, "TableInsetBox", new Vector2(0f, tableY), new Vector2(364f, tableH));

            void ApplyFilter(LeaderboardFilterMode mode)
            {
                _victoryLdFilter = mode;
                StyleForgeButton(bAll, mode == LeaderboardFilterMode.All, rightPage, new Vector2(-116f, 108f), new Vector2(106f, 32f));
                StyleForgeButton(bSolo, mode == LeaderboardFilterMode.Solo, rightPage, new Vector2(0f, 108f), new Vector2(106f, 32f));
                StyleForgeButton(bLan, mode == LeaderboardFilterMode.Lan, rightPage, new Vector2(116f, 108f), new Vector2(106f, 32f));
                var list = DelveLeaderboardDataSO.Instance.GetFilteredRecords(_victoryLdFilter);
                CompactLeaderboardTableBuilder.Render(tableInset, list, maxRows: victory ? 7 : 6);
            }

            bAll.onClick.RemoveAllListeners(); bAll.onClick.AddListener(() => ApplyFilter(LeaderboardFilterMode.All));
            bSolo.onClick.RemoveAllListeners(); bSolo.onClick.AddListener(() => ApplyFilter(LeaderboardFilterMode.Solo));
            bLan.onClick.RemoveAllListeners(); bLan.onClick.AddListener(() => ApplyFilter(LeaderboardFilterMode.Lan));
            ApplyFilter(LeaderboardFilterMode.All);

            if (!victory)
            {
                var btnMenu = EnsureNamedButton(rightPage, "BtnDefeatMenu", "MAIN MENU", new Vector2(0f, -196f), new Vector2(230f, 34f), false);
                btnMenu.onClick.RemoveAllListeners(); btnMenu.onClick.AddListener(() => glc?.ReturnToMainMenu());
            }
        }

        private static Button EnsureNamedButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, bool primary)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var btn = go.GetComponent<Button>();
            StyleForgeButton(btn, primary, parent, pos, size);

            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                var ch = go.transform.GetChild(i);
                if (ch.name != "Text") Object.Destroy(ch.gameObject);
            }

            Transform txtTr = go.transform.Find("Text");
            GameObject txtGo = txtTr != null ? txtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(go.transform, false);
            var tRt = txtGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            MedievalTypographyProvider.ApplyToTMP(tmp);
            tmp.fontSize = primary ? 15.5f : 14f; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = primary ? GoldTitleInk : DarkInk;
            return btn;
        }
    }
}
