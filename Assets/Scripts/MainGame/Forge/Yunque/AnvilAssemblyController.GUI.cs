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
            if (_bladeObj == null) return;
            EnsureStyles();

            if (_stage == AssemblyStage.Positioning)
            {
                float w = 780f, h = 112f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 20f;
                string wTitle = _anvil?.SelectedWeaponType == WeaponType.Hammer ? "MARTILLO" : (_anvil?.SelectedWeaponType == WeaponType.Sword ? "ESPADA" : "HACHA");
                string status = !IsContactingEye
                    ? (_anvil?.SelectedWeaponType == WeaponType.Sword ? "<color=#C84210><b>ESPIGA FUERA DE RANGO:</b> La espiga debe entrar en el mango sin sobresalir al aire.</color>" : "<color=#C84210><b>ALINEACIÓN PENDIENTE:</b> El mango debe estar contenido dentro del lingote.</color>")
                    : "<color=#168A48><b>¡ENCASTRE ALINEADO!</b> Encastre firme sin holguras. Haz [Click Izquierdo] para deslizar la pieza.</color>";
                GUI.Box(new Rect(x, y, w, h), $"<b>ENSAMBLAJE DE {wTitle}:</b> [Q]/[E]/[R]: Rotar ({_rotationStep * 90}°) • [A]/[D]/[W]/[S]: Ajustar posición ({_tangLength * 100f:F0}cm)\n{status}", _hudCardStyle);
            }
            else if (_stage == AssemblyStage.Sliding)
            {
                string part = _anvil?.SelectedWeaponType == WeaponType.Sword ? "HOJA DE LA ESPADA" : $"CABEZA DEL {(_anvil?.SelectedWeaponType == WeaponType.Hammer ? "MARTILLO" : "HACHA")}";
                GUI.Box(new Rect((Screen.width - 560f) * 0.5f, Screen.height - 78f, 560f, 48f), $"<b>DESLIZANDO {part} POR EL MANGO...</b>", _goldBannerStyle);
            }
            else if (_stage == AssemblyStage.Inspection)
            {
                float w = 800f, h = 212f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 20f;
                string def = _anvil?.SelectedWeaponType == WeaponType.Hammer ? "Martillo Artesanal" : (_anvil?.SelectedWeaponType == WeaponType.Sword ? "Espada Artesanal" : "Hacha Artesanal");
                string nm = _previewStats?.weaponName ?? def, q = _previewStats?.qualityPrefix ?? "Standard";
                float d = _previewStats?.baseDamage ?? 30f, s = _previewStats?.attackSpeed ?? 1.0f, sh = _previewStats?.maxSharpness ?? 80f, r = _previewStats?.attackRange ?? 1.2f;

                var style = _previewStats != null ? _previewStats.GetAnimationStyle() : WeaponAnimationStyle.Axe1H;
                string comboDesc = style switch
                {
                    WeaponAnimationStyle.Axe1H => "Hacha 1H (Combo 5 Golpes: Tajos Laterales + Hachazos)",
                    WeaponAnimationStyle.Hammer1H => "Martillo 1H (Combo 5 Golpes: Aplastamientos + Impactos)",
                    WeaponAnimationStyle.Sword1H => "Espada 1H (Combo 5 Golpes: Tajos Cruzados + Estocadas)",
                    WeaponAnimationStyle.Dagger1H => "Daga 1H (Combo 5 Golpes: Estocadas Rápidas y Cortes)",
                    WeaponAnimationStyle.Heavy2H => "Arma Pesada 2 Manos (Combo 5 Golpes Pesados de Gran Inercia)",
                    WeaponAnimationStyle.Polearm => "Arma de Asta (Combo 5 Golpes: Barridos Largos + Estocadas)",
                    _ => "Combo 5 Golpes"
                };

                string card = $"<b>★ ARMA ARTESANAL FORJADA CON ÉXITO ★</b>\n" +
                              $"<color=#845512><size=17><b>{nm}</b> [{q}]</size></color>\n" +
                              $"Daño: <b>{d:F1} DMG</b>  •  Velocidad: <b>{s:F2}x</b>  •  Filo: <b>{sh:F0}</b>  •  Alcance: <b>{r:F2}m</b>\n" +
                              $"Estilo de Combate: <color=#966014><b>{comboDesc}</b></color>";
                GUI.Box(new Rect(x, y, w, h), card, _hudCardStyle);
                if (GUI.Button(new Rect((Screen.width - 320f) * 0.5f, y + h - 44f, 320f, 34f), "<b>★ TOMAR ARMA [ESPACIO] ★</b>", _actionBtnStyle))
                {
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
