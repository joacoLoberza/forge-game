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
                bgImg.color = victory ? Color.black : new Color(0.06f, 0.02f, 0.02f, 0.90f);
            }

            // Purgar paneles planos anteriores (DecreeCardRoot / VictoryLeaderboardModal) y usar libro abierto
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
            var (leftPage, rightPage) = BookJournalTemplateBuilder.EnsureOpenBookShell(bookGo.transform);

            ClearPageContents(leftPage);
            ClearPageContents(rightPage);

            BuildEndScreenLeftPage(leftPage, title, victory, glc);
            BuildEndScreenRightPage(bookGo.transform, leftPage, rightPage, victory, glc);

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
            CreateOrGetTitleBanner(leftPage, "DecreeTitle", title, new Vector2(0f, 178f), new Vector2(356f, 44f), fontSize: 17f, topAnchored: false);

            var msgGo = new GameObject("DecreeMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
            msgGo.transform.SetParent(leftPage, false);
            var msgRt = msgGo.GetComponent<RectTransform>();
            msgRt.anchorMin = msgRt.anchorMax = msgRt.pivot = new Vector2(0.5f, 0.5f);
            msgRt.anchoredPosition = new Vector2(0f, 24f); msgRt.sizeDelta = new Vector2(354f, 224f);

            var mTmp = msgGo.GetComponent<TextMeshProUGUI>();
            mTmp.fontSize = 14.5f; mTmp.enableAutoSizing = true; mTmp.fontSizeMin = 11f; mTmp.fontSizeMax = 15.5f;
            mTmp.color = DarkInk; mTmp.alignment = TextAlignmentOptions.Center;

            string outcome = GameLoopController.LastOutcomeSummary;
            if (victory)
            {
                var timer = DelveRunTimerHUD.Instance;
                string timeLine = (timer != null && timer.HasWon)
                    ? $"\n\n<size=13.5><color=#241409><b>Completion Time:</b> {DelveLeaderboardDataSO.FormatSeconds(timer.FinalTime)}</color></size>" : "";
                string extra = !string.IsNullOrEmpty(outcome)
                    ? $"\n\n<size=12.5><color=#1A0F08><b>Sacred Consecration Reward:</b></color>\n<color=#4A2610>{outcome}</color></size>" : "";
                mTmp.text = "<size=16><b>Ancient Sanctum Consecrated!</b></size>\n<color=#613511>Glory is yours and your harvested ores have been blessed by the divine forge.</color>" + timeLine + extra;

                var btnKeep = EnsureNamedButton(leftPage, "BtnKeepPlaying", "CONTINUE PLAYING", new Vector2(0f, -118f), new Vector2(290f, 44f), true);
                btnKeep.onClick.RemoveAllListeners(); btnKeep.onClick.AddListener(() => glc?.AcceptVictory());

                var btnMenu = EnsureNamedButton(leftPage, "BtnReturnMenu", "MAIN MENU", new Vector2(0f, -172f), new Vector2(290f, 40f), false);
                btnMenu.onClick.RemoveAllListeners(); btnMenu.onClick.AddListener(() => glc?.ReturnToMainMenu());
            }
            else
            {
                string lost = !string.IsNullOrEmpty(outcome) ? $"\n\n<size=12.5><color=#7A1818><b>{outcome}</b></color></size>" : "";
                mTmp.text = "<size=16><b>Your strength faltered in the depths.</b></size>\n<color=#613511>You awaken beside the workshop forge, while the gathered minerals remain below.</color>" + lost;

                var btnAccept = EnsureNamedButton(leftPage, "BtnDefeatAccept", "AWAKEN AT WORKSHOP", new Vector2(0f, -138f), new Vector2(300f, 46f), true);
                btnAccept.onClick.RemoveAllListeners(); btnAccept.onClick.AddListener(() => glc?.AcceptDefeat());
            }
            MedievalTypographyProvider.ApplyToTMP(mTmp);
        }

        private static void BuildEndScreenRightPage(Transform bookRoot, RectTransform leftPage, RectTransform rightPage, bool victory, GameLoopController glc)
        {
            CreateOrGetTitleBanner(rightPage, "LdTitle", "HALL OF HEROES", new Vector2(0f, 178f), new Vector2(356f, 44f), fontSize: 17f, topAnchored: false);

            var subGo = new GameObject("LdSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(rightPage, false);
            var sRt = subGo.GetComponent<RectTransform>();
            sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = new Vector2(0f, 140f); sRt.sizeDelta = new Vector2(354f, 22f);
            var sTmp = subGo.GetComponent<TextMeshProUGUI>();
            sTmp.text = victory ? "<i>Speedrun records of the consecrated sanctum</i>" : "<i>Fastest delves etched in the guild tome</i>";
            sTmp.fontSize = 11.5f; sTmp.color = SubInk; sTmp.fontStyle = FontStyles.Italic; sTmp.alignment = TextAlignmentOptions.Center;
            MedievalTypographyProvider.ApplyToTMP(sTmp);

            var bAll = EnsureNamedButton(rightPage, "BtnFilterAll", "ALL", new Vector2(-114f, 108f), new Vector2(104f, 30f), true);
            var bSolo = EnsureNamedButton(rightPage, "BtnFilterSolo", "SOLO", new Vector2(0f, 108f), new Vector2(104f, 30f), false);
            var bLan = EnsureNamedButton(rightPage, "BtnFilterLan", "LAN", new Vector2(114f, 108f), new Vector2(104f, 30f), false);

            var tableInset = CreateOrGetParchmentCard(rightPage, "TableInsetBox", new Vector2(0f, -44f), new Vector2(364f, 260f));
            var textGo = new GameObject("TableContentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(tableInset, false);
            var tRt = textGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(10f, 8f); tRt.offsetMax = new Vector2(-10f, -8f);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 11.8f; tmp.enableAutoSizing = true; tmp.fontSizeMin = 9.5f; tmp.fontSizeMax = 12.2f;
            tmp.color = DarkInk; tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.overflowMode = TextOverflowModes.Truncate;

            var flipper = bookRoot.GetComponent<InventoryPageFlipper>() ?? bookRoot.gameObject.AddComponent<InventoryPageFlipper>();
            flipper.BindTargets(bookRoot, leftPage, rightPage);

            void ApplyFilter(LeaderboardFilterMode mode, bool animate)
            {
                int dir = (int)mode >= (int)_victoryLdFilter ? 1 : -1;
                _victoryLdFilter = mode;
                void SwapContent()
                {
                    StyleForgeButton(bAll, mode == LeaderboardFilterMode.All, rightPage, new Vector2(-114f, 108f), new Vector2(104f, 30f));
                    StyleForgeButton(bSolo, mode == LeaderboardFilterMode.Solo, rightPage, new Vector2(0f, 108f), new Vector2(104f, 30f));
                    StyleForgeButton(bLan, mode == LeaderboardFilterMode.Lan, rightPage, new Vector2(114f, 108f), new Vector2(104f, 30f));
                    PopulateVictoryLeaderboardText(tmp);
                }
                if (animate && bookRoot.gameObject.activeInHierarchy) flipper.PlayPageTurn(dir, SwapContent);
                else SwapContent();
            }

            bAll.onClick.RemoveAllListeners(); bAll.onClick.AddListener(() => ApplyFilter(LeaderboardFilterMode.All, true));
            bSolo.onClick.RemoveAllListeners(); bSolo.onClick.AddListener(() => ApplyFilter(LeaderboardFilterMode.Solo, true));
            bLan.onClick.RemoveAllListeners(); bLan.onClick.AddListener(() => ApplyFilter(LeaderboardFilterMode.Lan, true));
            ApplyFilter(LeaderboardFilterMode.All, false);

            if (!victory)
            {
                var btnMenu = EnsureNamedButton(rightPage, "BtnDefeatMenu", "MAIN MENU", new Vector2(0f, -196f), new Vector2(220f, 32f), false);
                btnMenu.onClick.RemoveAllListeners(); btnMenu.onClick.AddListener(() => glc?.ReturnToMainMenu());
            }
        }

        private static void PopulateVictoryLeaderboardText(TextMeshProUGUI tmp)
        {
            if (tmp == null) return;
            var list = DelveLeaderboardDataSO.Instance.GetFilteredRecords(_victoryLdFilter);
            tmp.text = MedievalTypographyProvider.SanitizeForTMP(DelveLeaderboardDataSO.BuildTableText(list, 8, compact: true));
            MedievalTypographyProvider.ApplyToTMP(tmp);
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
            tmp.fontSize = primary ? 16f : 14.5f; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = primary ? GoldTitleInk : DarkInk;
            return btn;
        }
    }
}