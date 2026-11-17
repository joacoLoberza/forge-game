using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ForjaGame.Core;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    /// <summary>
    /// Muestra en el viewport las aleaciones definidas (bloqueadas como "???" con silueta
    /// o reveladas tras fundirlas) tanto en el taller/exploración [J] como dentro del Horno.
    /// </summary>
    public sealed class AlloyCodexViewportHUD : MonoBehaviour
    {
        private static AlloyCodexViewportHUD _instance;
        private bool _manualOverride, _isExpanded = true;
        private GUIStyle _parchmentStyle, _titleStyle, _entryTitleStyle, _entrySubStyle, _mysteryStyle;
        private Texture2D _solidTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            if (_instance == null)
                _instance = new GameObject("[ALLOY_CODEX_HUD]").AddComponent<AlloyCodexViewportHUD>();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu" || PauseMenuPanel.IsGamePaused) return;
            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame && !ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen)
            {
                _manualOverride = true;
                _isExpanded = !_isExpanded;
                ForjaGame.Audio.ForgeAudioSystem.PlayUITab();
            }
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            if (PauseMenuPanel.IsGamePaused || GameLoopController.IsEndScreenOpen || VictoryCreditsOverlay.IsBackdropActive) return;

            var tut = ForgeTutorialDirector.Instance;
            if (tut != null && tut.IsTutorialActive && tut.CurrentStage == TutorialStage.ControlsQTE) return;

            EnsureStyles();
            GUI.depth = -120;

            if (Time.unscaledTime < AlloyCodexRegistry.RecentUnlockUntil && !string.IsNullOrEmpty(AlloyCodexRegistry.RecentUnlockName))
                DrawDiscoveryToast();

            if (FurnacePanel.Instance != null && FurnacePanel.Instance.IsOpen)
            {
                DrawFurnaceViewportCodex();
                return;
            }

            if (ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen) return;
            var anvil = AnvilController.Instance;
            if (anvil != null && (anvil.IsForging || anvil.IsCarvingHandle)) return;

            if (!_manualOverride && PlayerMovement.LocalInstance != null)
                _isExpanded = PlayerMovement.LocalInstance.transform.position.z < 36f;

            DrawWorkshopSideWidget();
        }

        private void DrawWorkshopSideWidget()
        {
            var entries = AlloyCodexRegistry.Entries;
            int unlocked = AlloyCodexRegistry.UnlockedCount;
            float w = 252f, rowH = 38f, headerH = 28f;
            float h = _isExpanded ? (headerH + entries.Length * (rowH + 4f) + 8f) : headerH + 6f;
            float x = 18f, y = 128f;

            GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), BookTextureBuilder.GetLeatherCoverSprite().texture);
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _parchmentStyle);

            string arrow = _isExpanded ? "▾" : "▸";
            if (GUI.Button(new Rect(x + 6f, y + 4f, w - 12f, headerH - 4f), $"<b>ALLOY CODEX  ({unlocked}/{entries.Length})  [J] {arrow}</b>", _titleStyle))
            {
                _manualOverride = true;
                _isExpanded = !_isExpanded;
            }
            if (!_isExpanded) return;

            float cy = y + headerH + 4f;
            for (int i = 0; i < entries.Length; i++)
            {
                DrawSideRow(new Rect(x + 8f, cy, w - 16f, rowH), entries[i], AlloyCodexRegistry.IsUnlocked(entries[i].Id));
                cy += rowH + 4f;
            }
        }

        private void DrawSideRow(Rect r, DefinedAlloyEntry e, bool unlocked)
        {
            Color prev = GUI.color;
            GUI.color = unlocked ? new Color(0.84f, 0.76f, 0.60f, 0.75f) : new Color(0.24f, 0.19f, 0.15f, 0.35f);
            GUI.DrawTexture(r, _solidTex);

            Rect badge = new Rect(r.x + 5f, r.y + 6f, 26f, 26f);
            GUI.color = unlocked ? e.SwatchColor : new Color(0.16f, 0.13f, 0.12f, 0.92f);
            GUI.DrawTexture(badge, _solidTex);
            GUI.color = prev;

            if (!unlocked)
            {
                GUI.Label(badge, "<b>?</b>", _mysteryStyle);
                GUI.Label(new Rect(badge.xMax + 8f, r.y + 2f, r.width - 42f, 18f), "<b>???</b>", _entryTitleStyle);
                GUI.Label(new Rect(badge.xMax + 8f, r.y + 18f, r.width - 42f, 16f), e.MysteryHint, _entrySubStyle);
            }
            else
            {
                GUI.Label(new Rect(badge.xMax + 8f, r.y + 2f, r.width - 42f, 18f), $"<b>{e.DisplayName}</b>", _entryTitleStyle);
                GUI.Label(new Rect(badge.xMax + 8f, r.y + 18f, r.width - 42f, 16f), e.FormulaText, _entrySubStyle);
            }
        }

        private void DrawFurnaceViewportCodex()
        {
            var entries = AlloyCodexRegistry.Entries;
            float totalW = Mathf.Clamp(Screen.width * 0.41f, 480f, 680f);
            float h = 114f, x = (Screen.width - totalW) * 0.5f, y = Screen.height - h - 14f;

            GUI.DrawTexture(new Rect(x - 3f, y - 3f, totalW + 6f, h + 6f), BookTextureBuilder.GetLeatherCoverSprite().texture);
            GUI.Box(new Rect(x, y, totalW, h), GUIContent.none, _parchmentStyle);
            GUI.Label(new Rect(x + 8f, y + 4f, totalW - 16f, 20f), $"<b>DEFINED ALLOYS IN VIEWPORT ({AlloyCodexRegistry.UnlockedCount}/{entries.Length})</b>", _titleStyle);

            float pad = 8f, gap = 6f;
            float cardW = (totalW - pad * 2f - gap * (entries.Length - 1)) / entries.Length;
            float cx = x + pad, cy = y + 26f, ch = h - 32f;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                bool unlocked = AlloyCodexRegistry.IsUnlocked(e.Id);
                Rect cr = new Rect(cx, cy, cardW, ch);
                Color prev = GUI.color;
                GUI.color = unlocked ? new Color(0.86f, 0.78f, 0.62f, 0.85f) : new Color(0.20f, 0.16f, 0.13f, 0.45f);
                GUI.DrawTexture(cr, _solidTex);
                GUI.color = unlocked ? e.SwatchColor : new Color(0.15f, 0.12f, 0.11f, 0.94f);
                GUI.DrawTexture(new Rect(cr.x + 6f, cr.y + 6f, cr.width - 12f, 4f), _solidTex);
                GUI.color = prev;

                string title = unlocked ? $"<b>{e.DisplayName}</b>" : "<b>?  ???</b>";
                string sub = unlocked ? $"{e.FormulaText}\n<i>{e.EffectSummary}</i>" : $"{e.MysteryHint}\nMix in Furnace";
                GUI.Label(new Rect(cr.x + 5f, cr.y + 12f, cr.width - 10f, 18f), title, _entryTitleStyle);
                GUI.Label(new Rect(cr.x + 5f, cr.y + 30f, cr.width - 10f, ch - 32f), sub, _entrySubStyle);
                cx += cardW + gap;
            }
        }

        private void DrawDiscoveryToast()
        {
            float w = 420f, h = 44f, x = (Screen.width - w) * 0.5f, y = 102f;
            GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), BookTextureBuilder.GetLeatherCoverSprite().texture);
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _parchmentStyle);
            GUI.Label(new Rect(x + 10f, y + 6f, w - 20f, 32f),
                $"<size=14><color=#7A1B0C><b>✦ NEW ALLOY DISCOVERED: {AlloyCodexRegistry.RecentUnlockName.ToUpperInvariant()} ✦</b></color></size>", _titleStyle);
        }

        private void EnsureStyles()
        {
            if (_parchmentStyle != null) return;
            _solidTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            _solidTex.SetPixel(0, 0, Color.white); _solidTex.Apply();

            _parchmentStyle = new GUIStyle { normal = { background = BookTextureBuilder.GetParchmentSprite().texture }, border = new RectOffset(14, 14, 14, 14) };
            _titleStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = ForgeCanvasThemeStyler.DarkInk } };
            _entryTitleStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 11, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = ForgeCanvasThemeStyler.DarkInk } };
            _entrySubStyle = new GUIStyle { alignment = TextAnchor.UpperLeft, fontSize = 10, wordWrap = true, richText = true, normal = { textColor = ForgeCanvasThemeStyler.SubInk } };
            _mysteryStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.95f, 0.80f, 0.34f) } };
            MedievalTypographyProvider.ApplyToGUIStyle(_titleStyle, 11, FontStyle.Bold);
            MedievalTypographyProvider.ApplyToGUIStyle(_entryTitleStyle, 11, FontStyle.Bold);
            MedievalTypographyProvider.ApplyToGUIStyle(_entrySubStyle, 10, FontStyle.Normal);
            MedievalTypographyProvider.ApplyToGUIStyle(_mysteryStyle, 14, FontStyle.Bold);
        }
    }
}

