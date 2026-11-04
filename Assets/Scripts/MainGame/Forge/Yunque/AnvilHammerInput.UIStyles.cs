using UnityEngine;
using ForjaGame.UI;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Módulo de estilos y generador procedural de texturas GUI medievales para AnvilHammerInput.
    /// Unifica el HUD de forja con la estética de pergamino, cuero y latón del Horno y la Prensa.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public partial class AnvilHammerInput
    {
        private static Texture2D _guiParchmentTex;
        private static Texture2D _guiLeatherTex;
        private static Texture2D _guiGoldPlaqueTex;
        private static Texture2D _guiThermalBarTex;

        private static GUIStyle _parchmentCardStyle;
        private static GUIStyle _goldBannerStyle;
        private static GUIStyle _tabActiveStyle;
        private static GUIStyle _tabInactiveStyle;
        private static GUIStyle _badgeReadyStyle;
        private static GUIStyle _badgePendingStyle;
        private static GUIStyle _badgeFireStyle;

        private static void EnsureUIStyles()
        {
            if (_parchmentCardStyle != null) return;

            InitTextures();

            _parchmentCardStyle = new GUIStyle
            {
                normal = { background = _guiParchmentTex, textColor = ForgeCanvasThemeStyler.DarkInk },
                border = new RectOffset(16, 16, 16, 16),
                padding = new RectOffset(14, 14, 10, 10),
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                richText = true,
                alignment = TextAnchor.UpperLeft
            };

            _goldBannerStyle = new GUIStyle
            {
                normal = { background = _guiGoldPlaqueTex, textColor = new Color(0.99f, 0.90f, 0.64f) },
                border = new RectOffset(16, 16, 14, 14),
                padding = new RectOffset(12, 12, 6, 6),
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter
            };

            _tabActiveStyle = new GUIStyle
            {
                normal = { background = _guiGoldPlaqueTex, textColor = new Color(0.18f, 0.11f, 0.05f) },
                border = new RectOffset(16, 16, 14, 14),
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter
            };

            _tabInactiveStyle = new GUIStyle
            {
                normal = { background = _guiLeatherTex, textColor = new Color(0.85f, 0.78f, 0.68f) },
                border = new RectOffset(14, 14, 14, 14),
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                richText = true,
                alignment = TextAnchor.MiddleCenter
            };

            _badgeReadyStyle = new GUIStyle
            {
                normal = { textColor = new Color(0.12f, 0.75f, 0.45f) },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            _badgePendingStyle = new GUIStyle
            {
                normal = { textColor = new Color(0.85f, 0.52f, 0.12f) },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            _badgeFireStyle = new GUIStyle
            {
                normal = { textColor = new Color(1.0f, 0.42f, 0.15f) },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            MedievalTypographyProvider.ApplyToGUIStyle(_parchmentCardStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_goldBannerStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_tabActiveStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_tabInactiveStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_badgeReadyStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_badgePendingStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_badgeFireStyle);
        }

        private static void InitTextures()
        {
            if (_guiParchmentTex != null) return;

            // 1. Tarjeta de pergamino con reborde de cuero oscuro repujado
            int w = 64, h = 64;
            _guiParchmentTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_GUI_ParchmentCard" };
            Color cBorder = new Color(0.18f, 0.12f, 0.08f, 0.98f);
            Color cRim = new Color(0.72f, 0.58f, 0.32f, 0.90f);
            Color cPaper = new Color(0.95f, 0.92f, 0.85f, 0.96f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    Color c = (d <= 3) ? cBorder : (d <= 6) ? cRim : cPaper;
                    _guiParchmentTex.SetPixel(x, y, c);
                }
            }
            _guiParchmentTex.Apply();

            // 2. Placa de latón/bronce dorado con bisel
            _guiGoldPlaqueTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_GUI_GoldPlaque" };
            Color gRim = new Color(0.24f, 0.16f, 0.08f, 0.98f);
            Color gBevel = new Color(0.96f, 0.82f, 0.44f, 1f);
            Color gBody = new Color(0.78f, 0.60f, 0.26f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    Color c = (d <= 2) ? gRim : (d <= 5) ? gBevel : gBody;
                    _guiGoldPlaqueTex.SetPixel(x, y, c);
                }
            }
            _guiGoldPlaqueTex.Apply();

            // 3. Cuero oscuro encuadernado
            _guiLeatherTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_GUI_Leather" };
            Color lEdge = new Color(0.10f, 0.07f, 0.05f, 0.98f);
            Color lBody = new Color(0.22f, 0.15f, 0.11f, 0.96f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                    _guiLeatherTex.SetPixel(x, y, d <= 3 ? lEdge : lBody);
                }
            }
            _guiLeatherTex.Apply();

            // 4. Barra térmica para el gradiente de temperatura
            _guiThermalBarTex = new Texture2D(128, 16, TextureFormat.RGBA32, false) { name = "Tex_GUI_ThermalBar" };
            for (int x = 0; x < 128; x++)
            {
                float t = x / 127f;
                Color col = t < 0.35f
                    ? Color.Lerp(new Color(0.2f, 0.4f, 0.7f), new Color(0.85f, 0.45f, 0.12f), t / 0.35f)
                    : (t < 0.80f
                        ? Color.Lerp(new Color(0.85f, 0.45f, 0.12f), new Color(1.0f, 0.85f, 0.25f), (t - 0.35f) / 0.45f)
                        : Color.Lerp(new Color(1.0f, 0.85f, 0.25f), new Color(1.0f, 0.20f, 0.15f), (t - 0.80f) / 0.20f));
                for (int y = 0; y < 16; y++)
                {
                    bool edge = (y == 0 || y == 15 || x == 0 || x == 127);
                    _guiThermalBarTex.SetPixel(x, y, edge ? new Color(0.12f, 0.08f, 0.05f, 1f) : col);
                }
            }
            _guiThermalBarTex.Apply();
        }

        private static void DrawThermalGradientBar(Rect r, float currentTemp)
        {
            if (_guiThermalBarTex != null)
                GUI.DrawTexture(r, _guiThermalBarTex, ScaleMode.StretchToFill);

            // Marcador vertical de la temperatura actual (25°C a 1300°C)
            float frac = Mathf.Clamp01((currentTemp - 25f) / 1275f);
            float markerX = r.x + frac * (r.width - 4f);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(markerX, r.y - 3f, 4f, r.height + 6f), Texture2D.whiteTexture);
        }
    }
}

