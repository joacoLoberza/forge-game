using UnityEngine;
using ForjaGame.Inventory;
using ForjaGame.UI;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Interfaz gráfica inmediata (OnGUI) para el sistema de ensamblaje en el yunque.
    /// Diseñada con estética de pergamino, cuero y latón para coincidir con el Horno y la Prensa.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public partial class AnvilAssemblyController
    {
        private static Texture2D _parchmentTex;
        private static Texture2D _goldPlaqueTex;
        private static GUIStyle _hudCardStyle;
        private static GUIStyle _goldBannerStyle;
        private static GUIStyle _actionBtnStyle;

        private void OnGUI()
        {
            if (_bladeObj == null || PauseMenuPanel.IsGamePaused) return;
            EnsureStyles();

            if (_stage == AssemblyStage.Positioning)
            {
                float w = 780f, h = 148f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 20f;
                string wTitle = _anvil?.SelectedWeaponType == WeaponType.Hammer ? "HAMMER" : (_anvil?.SelectedWeaponType == WeaponType.Sword ? "SWORD" : "AXE");
                string status = !IsContactingEye
                    ? (_anvil?.SelectedWeaponType == WeaponType.Sword ? "<color=#C84210><b>TANG OUT OF RANGE:</b> Blade tang must fit inside handle socket.</color>" : "<color=#C84210><b>ALIGNMENT PENDING:</b> Handle must fit within ingot eye.</color>")
                    : "<color=#168A48><b>FIT ALIGNED!</b> Firm socket with zero wobble. Left Click to slide blade head.</color>";
                GUI.Box(new Rect(x, y, w, h), $"<b>ASSEMBLING {wTitle}:</b> [E]/[R]: Rotate ({_rotationStep * 90}°) • [A]/[D]/[W]/[S]: Adjust position ({_tangLength * 100f:F0}cm)\n{status}", _hudCardStyle);
                if (GUI.Button(new Rect(x + (w - 300f) * 0.5f, y + h - 42f, 300f, 32f), "<b>« RETURN TO HANDLE [BACKSPACE]</b>", _actionBtnStyle))
                {
                    ForjaGame.Audio.ForgeAudioSystem.PlayUIClick();
                    ReturnToHandleCarving();
                }
            }
            else if (_stage == AssemblyStage.Sliding)
            {
                string part = _anvil?.SelectedWeaponType == WeaponType.Sword ? "SWORD BLADE" : $"{(_anvil?.SelectedWeaponType == WeaponType.Hammer ? "HAMMER HEAD" : "AXE HEAD")}";
                GUI.Box(new Rect((Screen.width - 560f) * 0.5f, Screen.height - 78f, 560f, 48f), $"<b>SLIDING {part} ONTO HANDLE...</b>", _goldBannerStyle);
            }
            else if (_stage == AssemblyStage.Inspection)
            {
                float w = 800f, h = 212f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 20f;
                string def = _anvil?.SelectedWeaponType == WeaponType.Hammer ? "Artisan Hammer" : (_anvil?.SelectedWeaponType == WeaponType.Sword ? "Artisan Sword" : "Artisan Axe");
                string nm = _previewStats?.weaponName ?? def, q = _previewStats?.qualityPrefix ?? "Standard";
                float d = _previewStats?.baseDamage ?? 30f, s = _previewStats?.attackSpeed ?? 1.0f, sh = _previewStats?.maxSharpness ?? 80f, r = _previewStats?.attackRange ?? 1.2f;
                float mKg = _previewStats != null ? _previewStats.Profile.mass : 1.5f;

                string lenLabel = _previewStats != null ? _previewStats.handleLength.ToString() : "Medium";
                string comboDesc = _previewStats != null ? _previewStats.weaponType switch
                {
                    WeaponType.Axe => $"{lenLabel} Axe (5-Hit Chop & Cleave Combo)",
                    WeaponType.Hammer => $"{lenLabel} Hammer (5-Hit Crush & Slam Combo)",
                    _ => $"{lenLabel} Sword (5-Hit Slash & Thrust Combo)"
                } : "5-Hit Combo";

                var passive = ForjaGame.Combat.AlloyPassiveResolver.Resolve(_previewStats);
                string passiveLine = passive.Kind != ForjaGame.Combat.AlloyPassiveKind.None
                    ? $"\n<color={passive.ColorHex}><b>Passive [{passive.PassiveName}]:</b></color> {passive.Summary}" : "";

                string card = $"<b>★ ARTISAN WEAPON SUCCESSFULLY FORGED ★</b>\n" +
                              $"<color=#845512><size=17><b>{nm}</b> [{q}]</size></color>\n" +
                              $"Damage: <b>{d:F1} DMG</b>  •  Speed: <b>{s:F2}x</b>  •  Weight: <b>{mKg:F1} kg</b>  •  Reach: <b>{r:F2}m</b>\n" +
                              $"Combat Style: <color=#966014><b>{comboDesc}</b></color>{passiveLine}";
                GUI.Box(new Rect(x, y, w, h), card, _hudCardStyle);
                float btnRowY = y + h - 44f;
                if (GUI.Button(new Rect(x + 60f, btnRowY, 310f, 34f), "<b>« RETURN TO HANDLE [BACKSPACE]</b>", _actionBtnStyle))
                {
                    ForjaGame.Audio.ForgeAudioSystem.PlayUIClick();
                    ReturnToHandleCarving();
                }
                if (GUI.Button(new Rect(x + w - 370f, btnRowY, 310f, 34f), "<b>★ CLAIM WEAPON [SPACE] ★</b>", _actionBtnStyle))
                {
                    ForjaGame.Audio.ForgeAudioSystem.PlayUIClick();
                    FinishAssembly();
                }
            }
        }

        private void EnsureStyles()
        {
            if (_hudCardStyle != null) return;

            int sz = 48;
            _parchmentTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { name = "Tex_Assembly_Parchment" };
            Color border = new Color(0.18f, 0.12f, 0.08f, 0.98f);
            Color rim = new Color(0.72f, 0.58f, 0.32f, 0.90f);
            Color paper = new Color(0.95f, 0.92f, 0.85f, 0.96f);
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, sz - 1 - x), Mathf.Min(y, sz - 1 - y));
                    _parchmentTex.SetPixel(x, y, d <= 2 ? border : (d <= 5 ? rim : paper));
                }
            }
            _parchmentTex.Apply();

            _goldPlaqueTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { name = "Tex_Assembly_Gold" };
            Color gRim = new Color(0.24f, 0.16f, 0.08f, 0.98f);
            Color gBevel = new Color(0.96f, 0.82f, 0.44f, 1f);
            Color gBody = new Color(0.78f, 0.60f, 0.26f, 1f);
            for (int y = 0; y < sz; y++)
            {
                for (int x = 0; x < sz; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, sz - 1 - x), Mathf.Min(y, sz - 1 - y));
                    _goldPlaqueTex.SetPixel(x, y, d <= 2 ? gRim : (d <= 5 ? gBevel : gBody));
                }
            }
            _goldPlaqueTex.Apply();

            _hudCardStyle = new GUIStyle
            {
                normal = { background = _parchmentTex, textColor = ForgeCanvasThemeStyler.DarkInk },
                border = new RectOffset(14, 14, 14, 14),
                padding = new RectOffset(16, 16, 12, 12),
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                richText = true,
                alignment = TextAnchor.UpperCenter
            };

            _goldBannerStyle = new GUIStyle
            {
                normal = { background = _goldPlaqueTex, textColor = new Color(0.18f, 0.11f, 0.05f) },
                border = new RectOffset(14, 14, 14, 14),
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter
            };

            _actionBtnStyle = new GUIStyle
            {
                normal = { background = _goldPlaqueTex, textColor = new Color(0.18f, 0.11f, 0.05f) },
                border = new RectOffset(14, 14, 14, 14),
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter
            };

            MedievalTypographyProvider.ApplyToGUIStyle(_hudCardStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_goldBannerStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_actionBtnStyle);

            _hudStyle = _hudCardStyle;
            _cardStyle = _hudCardStyle;
        }
    }
}
