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
            Slider existingSlider = null;
            Button resumeBtn = null, backBtn = null;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform ch = root.GetChild(i);
                string cn = ch.name;
                if (cn == "BookPageStackLeft" || cn == "BookPageStackRight" || cn == "LeftPage" || cn == "RightPage" || cn == "BookSpine" || cn == "BtnPauseCloseTab" || cn.StartsWith("BookCorner"))
                    continue;

                if (existingSlider == null) existingSlider = ch.GetComponentInChildren<Slider>(true);
                string n = cn.ToLowerInvariant();
                if (ch.TryGetComponent<Button>(out var b))
                {
                    if (n.Contains("resume") || n.Contains("reanudar") || n.Contains("continuar") || n.Contains("volver"))
                        resumeBtn = resumeBtn ?? b;
                    else if (n.Contains("back") || n.Contains("menu") || n.Contains("salir"))
                        backBtn = backBtn ?? b;
                    continue;
                }
                if (ch.TryGetComponent<Slider>(out var rootSlider))
                {
                    existingSlider = existingSlider ?? rootSlider;
                    continue;
                }
                ch.gameObject.SetActive(false);
            }

            // Misma plantilla de libro abierto que el Inventario
            var (leftPage, rightPage) = BookJournalTemplateBuilder.EnsureOpenBookShell(root, resetRootRect: true, bookScale: 1.18f);

            if (existingSlider == null) existingSlider = rightPage.GetComponentInChildren<Slider>(true);
            foreach (var b in rightPage.GetComponentsInChildren<Button>(true))
            {
                string n = b.name.ToLowerInvariant();
                if (resumeBtn == null && (n.Contains("resume") || n.Contains("reanudar"))) resumeBtn = b;
                else if (backBtn == null && (n.Contains("back") || n.Contains("menu") || n.Contains("salir"))) backBtn = b;
            }

            // Página Izquierda: Identidad del herrero + tarjeta de guía del taller y controles
            ForgeCanvasThemeStyler.CreateOrGetTitleBanner(leftPage, "PauseCodexHeader", "EXPEDITION CODEX",
                new Vector2(0f, 184f), new Vector2(354f, 42f), 17f);

            string player = ForjaGame.Multiplayer.GameSessionData.PlayerName;
            string team = ForjaGame.Multiplayer.GameSessionData.TeamName;
            string who = string.IsNullOrWhiteSpace(team) ? $"Blacksmith: {player}" : $"{player}  |  {team}";
            ForgeCanvasThemeStyler.CreateOrGetTitleBanner(leftPage, "PauseIdentityPlaque", MedievalTypographyProvider.SanitizeForTMP(who),
                new Vector2(0f, 134f), new Vector2(354f, 30f), 12.5f);

            var guideCard = BookJournalTemplateBuilder.CreateOrGetPageInset(leftPage, "PauseGuideCard", new Vector2(0f, -44f), new Vector2(358f, 302f), new Color(0.92f, 0.86f, 0.75f, 0.78f));
            string guideText =
                "<color=#6D3A10><b>WORKSHOP FLOW</b></color>\n" +
                "1. Smelt raw ores & coal in the <b>Furnace</b> into a Bloom.\n" +
                "2. Compact the Bloom in the <b>Mechanical Press</b>.\n" +
                "3. Heat, shape, quench and assemble at the <b>Anvil</b>.\n\n" +
                "<color=#6D3A10><b>COMBAT & EXPLORATION</b></color>\n" +
                "- <b>[WASD]</b> Move  |  <b>[Shift]</b> Sprint  |  <b>[Space]</b> Jump\n" +
                "- <b>[Q]</b> Dodge Roll  |  <b>[LMB]</b> Strike\n" +
                "- <b>[RMB]</b> Block / Timed Parry  |  <b>[F]</b> Interact\n" +
                "- <b>[E]</b> Inventory  |  <b>[F10]</b> Display 2 Board";
            EnsurePausePageBody(guideCard, "PauseGuideBody", guideText, Vector2.zero, new Vector2(334f, 282f));

            // Página Derecha: Controles de Pausa + Slider de volumen KISS
            ForgeCanvasThemeStyler.CreateOrGetTitleBanner(rightPage, "PauseTitleBanner", "PAUSE MENU",
                new Vector2(0f, 184f), new Vector2(354f, 42f), 17f);

            ForgeCanvasThemeStyler.EnsureStyledVolumeChassis(
                rightPage, "PauseVolumeChassis", new Vector2(0f, 102f), 344f,
                ForjaGame.Audio.ForgeAudioSystem.GetMasterVolume(),
                v => pause.ApplyVolumeFromUI(v), existingSlider, null);

            if (resumeBtn != null)
            {
                resumeBtn.gameObject.SetActive(true);
                ForgeCanvasThemeStyler.StyleForgeButton(resumeBtn, primary: true, rightPage, new Vector2(0f, 14f), new Vector2(280f, 46f));
                var bTxt = resumeBtn.GetComponentInChildren<TMP_Text>(true);
                if (bTxt != null) bTxt.text = "Resume Game";
            }

            EnsureTutorialToggleButton(rightPage);
            var tutTr = rightPage.Find("BtnTutorial");
            if (tutTr != null && tutTr.TryGetComponent<Button>(out var tutBtn))
                ForgeCanvasThemeStyler.StyleForgeButton(tutBtn, primary: false, rightPage, new Vector2(0f, -50f), new Vector2(280f, 42f));

            if (backBtn != null)
            {
                backBtn.gameObject.SetActive(true);
                ForgeCanvasThemeStyler.StyleForgeButton(backBtn, primary: false, rightPage, new Vector2(0f, -110f), new Vector2(280f, 42f));
                var bTxt = backBtn.GetComponentInChildren<TMP_Text>(true);
                if (bTxt != null) bTxt.text = "Return to Menu";
            }

            // Solapa-marcapáginas de cierre coherente con Inventario
            BookJournalTemplateBuilder.CreateOrGetCloseBookmarkTab(root, "BtnPauseCloseTab", "Close [ESC]", pause.ResumeGame);
            MedievalTypographyProvider.ApplyToAllTMPInHierarchy(root);
        }

        private static void EnsurePausePageBody(Transform page, string name, string body, Vector2 pos, Vector2 size)
        {
            Transform tr = page.Find(name);
            GameObject go = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(page, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(tmp);
            tmp.text = body; tmp.fontSize = 13.5f;
            tmp.enableAutoSizing = true; tmp.fontSizeMin = 10.5f; tmp.fontSizeMax = 14f;
            tmp.color = ForgeCanvasThemeStyler.DarkInk;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
        }

        private static void EnsureTutorialToggleButton(Transform root)
        {
            Transform tutTr = root.Find("BtnTutorial");
            GameObject tutGo = tutTr != null ? tutTr.gameObject : null;
            if (tutGo == null)
            {
                tutGo = new GameObject("BtnTutorial", typeof(RectTransform), typeof(Image), typeof(Button));
                tutGo.transform.SetParent(root, false);
                var lGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
                lGo.transform.SetParent(tutGo.transform, false);
                var lRt = lGo.GetComponent<RectTransform>();
                lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one; lRt.offsetMin = lRt.offsetMax = Vector2.zero;
            }
            var tutTxt = tutGo.GetComponentInChildren<TMP_Text>(true);
            void RefreshLabel()
            {
                if (tutTxt != null)
                {
                    MedievalTypographyProvider.ApplyToTMP(tutTxt);
                    tutTxt.text = ForjaGame.Core.TutorialSettings.IsTutorialEnabled ? "Tutorial Guides: <b>ON</b>" : "Tutorial Guides: <b>OFF</b>";
                    tutTxt.fontSize = 14.5f; tutTxt.fontStyle = FontStyles.Bold;
                    tutTxt.color = ForgeCanvasThemeStyler.DarkInk; tutTxt.alignment = TextAlignmentOptions.Center;
                }
            }
            RefreshLabel();
            var tutBtn = tutGo.GetComponent<Button>();
            tutBtn.onClick.RemoveAllListeners();
            tutBtn.onClick.AddListener(() =>
            {
                ForjaGame.Core.TutorialSettings.Toggle();
                RefreshLabel();
                if (!ForjaGame.Core.TutorialSettings.IsTutorialEnabled)
                    ForgeTutorialDirector.Instance?.HideBeaconLights();
                ForjaGame.Audio.ForgeAudioSystem.PlayUIClick();
            });
        }
    }
}
