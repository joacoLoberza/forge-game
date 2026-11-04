using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.UI
{
    /// <summary>
    /// Generador procedural de iconos medievales nítidos (64x64) para el inventario, hornos,
    /// prensas y yunques. Garantiza que ningún objeto se muestre como un cuadro blanco roto.
    /// </summary>
    public static class ProceduralItemIconProvider
    {
        private static readonly Dictionary<string, Sprite> _cachedSprites = new();

        public static Sprite GetItemIcon(string itemId, string category = "")
        {
            if (string.IsNullOrEmpty(itemId)) itemId = "Iron";
            string key = $"{category}_{itemId}".ToLowerInvariant();
            if (_cachedSprites.TryGetValue(key, out var sprite) && sprite != null) return sprite;

            Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Icon_{key}"
            };

            Color[] pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            string idLower = itemId.ToLowerInvariant();
            string catLower = category.ToLowerInvariant();

            if (catLower.Contains("weapon") || idLower.Contains("sword") || idLower.Contains("axe") ||
                idLower.Contains("hammer") || idLower.Contains("pick") || idLower.Contains("dagger") || idLower.Contains("spear"))
            {
                DrawWeaponIcon(pixels, idLower);
            }
            else if (catLower.Contains("handle") || idLower.Contains("handle") || idLower.Contains("mango") || idLower.Contains("wood"))
            {
                DrawHandleIcon(pixels);
            }
            else if (catLower.Contains("bloom") || idLower.Contains("bloom") || idLower.Contains("lupa"))
            {
                DrawBloomIcon(pixels, ResolveMetalColor(idLower));
            }
            else if (catLower.Contains("ingot") || idLower.Contains("ingot") || idLower.Contains("lingote") || idLower.Contains("bar"))
            {
                DrawIngotIcon(pixels, ResolveMetalColor(idLower));
            }
            else // Minerales u otros recursos
            {
                DrawMineralOreIcon(pixels, ResolveMetalColor(idLower), idLower.Contains("coal") || idLower.Contains("carbon"));
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite result = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            result.name = $"Sprite_{key}";
            _cachedSprites[key] = result;
            return result;
        }

        private static Color ResolveMetalColor(string id)
        {
            if (id.Contains("coal") || id.Contains("carbon")) return new Color(0.20f, 0.20f, 0.22f);
            if (id.Contains("copper") || id.Contains("cobre") || id.Contains("bronze")) return new Color(0.86f, 0.46f, 0.24f);
            if (id.Contains("gold") || id.Contains("oro")) return new Color(0.96f, 0.82f, 0.22f);
            return new Color(0.72f, 0.74f, 0.78f); // Hierro / Acero por defecto
        }

        private static void DrawMineralOreIcon(Color[] px, Color baseCol, bool isCoal)
        {
            Color edge = isCoal ? new Color(0.10f, 0.10f, 0.10f) : baseCol * 0.45f;
            Color gleam = isCoal ? new Color(0.40f, 0.40f, 0.45f) : Color.white;
            for (int y = 14; y <= 50; y++)
            {
                for (int x = 14; x <= 50; x++)
                {
                    float dx = (x - 32) * 1.05f, dy = (y - 32) * 1.15f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float crag = Mathf.Sin(x * 0.35f) * 2.2f + Mathf.Cos(y * 0.45f) * 2.4f;
                    if (dist + crag <= 16f)
                    {
                        float light = Mathf.Clamp01((x - 20 + (50 - y)) / 44f);
                        Color c = Color.Lerp(edge, baseCol, light);
                        if (x > 24 && x < 32 && y > 34 && y < 42) c = Color.Lerp(c, gleam, 0.65f);
                        px[y * 64 + x] = c;
                    }
                }
            }
        }

        private static void DrawIngotIcon(Color[] px, Color baseCol)
        {
            Color shadow = baseCol * 0.42f;
            Color top = Color.Lerp(baseCol, Color.white, 0.28f);
            for (int y = 20; y <= 44; y++)
            {
                for (int x = 12; x <= 52; x++)
                {
                    int inset = (y - 20) / 2;
                    if (x >= 14 + inset && x <= 50 - inset)
                    {
                        Color c = y >= 36 ? top : (x <= 18 + inset ? shadow : baseCol);
                        if (x == 14 + inset || x == 50 - inset || y == 20 || y == 44) c = shadow * 0.8f;
                        px[y * 64 + x] = c;
                    }
                }
            }
        }

        private static void DrawBloomIcon(Color[] px, Color baseCol)
        {
            Color slag = new Color(0.16f, 0.14f, 0.12f);
            Color incandescent = new Color(1.0f, 0.52f, 0.16f);
            for (int y = 16; y <= 48; y++)
            {
                for (int x = 16; x <= 48; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                    float noise = Mathf.Sin(x * 0.6f + y * 0.4f) * 2.8f;
                    if (dist + noise <= 14.5f)
                    {
                        bool isCrevice = Mathf.Sin(x * 0.9f) * Mathf.Cos(y * 0.9f) > 0.45f;
                        px[y * 64 + x] = isCrevice ? incandescent : Color.Lerp(slag, baseCol, 0.45f);
                    }
                }
            }
        }

        private static void DrawHandleIcon(Color[] px)
        {
            Color wood = new Color(0.54f, 0.35f, 0.18f);
            Color wrap = new Color(0.32f, 0.22f, 0.14f);
            for (int t = 12; t <= 52; t++)
            {
                int x = t, y = t;
                for (int ox = -2; ox <= 2; ox++)
                {
                    for (int oy = -2; oy <= 2; oy++)
                    {
                        if (ox * ox + oy * oy <= 4)
                        {
                            bool isGrip = t >= 16 && t <= 30 && ((x + y) % 6 < 3);
                            px[(y + oy) * 64 + (x + ox)] = isGrip ? wrap : wood;
                        }
                    }
                }
            }
        }

        private static void DrawWeaponIcon(Color[] px, string id)
        {
            Color steel = new Color(0.85f, 0.88f, 0.92f);
            Color darkSteel = new Color(0.35f, 0.38f, 0.42f);
            Color gold = new Color(0.85f, 0.68f, 0.22f);
            Color wood = new Color(0.50f, 0.32f, 0.16f);

            if (id.Contains("axe") || id.Contains("hacha"))
            {
                // Mango diagonal
                for (int t = 14; t <= 50; t++) SetThickDot(px, t, t, wood, 1);
                // Hoja de hacha curva
                for (int y = 32; y <= 52; y++)
                    for (int x = 20; x <= 46; x++)
                        if (x < y && Vector2.Distance(new Vector2(x, y), new Vector2(30, 42)) <= 9.5f)
                            SetDot(px, x, y, steel);
            }
            else if (id.Contains("hammer") || id.Contains("martillo"))
            {
                for (int t = 14; t <= 50; t++) SetThickDot(px, t, t, wood, 1);
                for (int y = 38; y <= 52; y++)
                    for (int x = 26; x <= 48; x++)
                        if (Mathf.Abs((x - 37) - (y - 45)) <= 6) SetDot(px, x, y, steel);
            }
            else if (id.Contains("pick") || id.Contains("pico"))
            {
                for (int t = 14; t <= 50; t++) SetThickDot(px, t, t, wood, 1);
                // Cabeza curva de pico
                for (int a = -10; a <= 10; a++)
                {
                    int hx = 42 - a, hy = 42 + a - (a * a) / 14;
                    SetThickDot(px, hx, hy, steel, 1);
                }
            }
            else // Espada o Daga por defecto
            {
                // Hoja vertical central
                for (int y = 22; y <= 54; y++)
                {
                    int w = y >= 50 ? (54 - y) : 2;
                    for (int x = 32 - w; x <= 32 + w; x++)
                        SetDot(px, x, y, x == 32 ? Color.white : (x < 32 ? steel : darkSteel));
                }
                // Guarda
                for (int x = 23; x <= 41; x++) SetDot(px, x, 22, gold);
                // Empuñadura y pomo
                for (int y = 14; y <= 21; y++) SetDot(px, 32, y, wood);
                SetThickDot(px, 32, 12, gold, 1);
            }
        }

        private static void SetDot(Color[] px, int x, int y, Color c)
        {
            if (x >= 0 && x < 64 && y >= 0 && y < 64) px[y * 64 + x] = c;
        }

        private static void SetThickDot(Color[] px, int cx, int cy, Color c, int r)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r + 1) SetDot(px, cx + dx, cy + dy, c);
        }
    }
}

