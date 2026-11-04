using UnityEngine;

namespace ForjaGame.UI
{
    /// <summary>
    /// Generador procedural de texturas y sprites con temática de libro medieval / pergamino antiguo.
    /// Crea texturas optimizadas en memoria con 9-slicing para páginas, cubiertas y marcapáginas.
    /// </summary>
    public static partial class BookTextureBuilder
    {
        private static Sprite _parchmentSprite;
        private static Sprite _leatherCoverSprite;
        private static Sprite _spineSprite;
        private static Sprite _ribbonTabSprite;
        private static Sprite _bannerSprite;

        public static Sprite GetParchmentSprite()
        {
            if (_parchmentSprite != null) return _parchmentSprite;

            int w = 128, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_ParchmentPage" };
            Color baseCol = new Color(0.96f, 0.93f, 0.86f, 1f); // Pergamino crema suave
            Color edgeCol = new Color(0.85f, 0.79f, 0.70f, 1f); // Sombra sepia en bordes

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float edgeDist = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) / 14f;
                    float t = Mathf.Clamp01(edgeDist);
                    float noise = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 0.04f;
                    Color col = Color.Lerp(edgeCol, baseCol, t) + new Color(noise, noise, noise, 0f);
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            _parchmentSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            return _parchmentSprite;
        }

        public static Sprite GetLeatherCoverSprite()
        {
            if (_leatherCoverSprite != null) return _leatherCoverSprite;

            int w = 128, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_LeatherCover" };
            Color leatherCol = new Color(0.16f, 0.10f, 0.07f, 0.98f); // Cuero oscuro encuadernado
            Color stitchCol = new Color(0.72f, 0.58f, 0.32f, 0.95f);  // Pespunte dorado/latón

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 5 || x >= w - 6 || y <= 5 || y >= h - 6);
                    bool isStitch = ((x == 9 || x == w - 10) && (y % 6 < 4)) || ((y == 9 || y == h - 10) && (x % 6 < 4));

                    Color c = isStitch ? stitchCol : isBorder ? new Color(0.12f, 0.07f, 0.05f, 1f) : leatherCol;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _leatherCoverSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16, 16, 16, 16));
            return _leatherCoverSprite;
        }

        public static Sprite GetSpineSprite()
        {
            if (_spineSprite != null) return _spineSprite;

            int w = 32, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_BookSpine" };
            Color deepShadow = new Color(0.08f, 0.05f, 0.03f, 0.85f);
            Color highlight = new Color(0.78f, 0.65f, 0.40f, 0.90f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float centerDist = Mathf.Abs(x - w * 0.5f) / (w * 0.5f);
                    Color col = Color.Lerp(deepShadow, new Color(0.18f, 0.12f, 0.08f, 0.5f), centerDist);

                    // Costuras del lomo cada 24 píxeles
                    if (y % 24 < 3 && Mathf.Abs(x - w * 0.5f) < 8)
                        col = highlight;

                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            _spineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 8, 4, 8));
            return _spineSprite;
        }

        public static Sprite GetRibbonTabSprite()
        {
            if (_ribbonTabSprite != null) return _ribbonTabSprite;

            int w = 64, h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_BookmarkRibbon" };
            Color fill = Color.white;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Esquinas superiores redondeadas
                    if (y > h - 8 && (x < 6 || x > w - 7))
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Borde sutil
                    bool isEdge = (x == 0 || x == w - 1 || y == h - 1);
                    tex.SetPixel(x, y, isEdge ? new Color(0.85f, 0.85f, 0.85f, 1f) : fill);
                }
            }
            tex.Apply();
            _ribbonTabSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
            return _ribbonTabSprite;
        }

        public static Sprite GetParchmentBannerSprite()
        {
            if (_bannerSprite != null) return _bannerSprite;

            int w = 96, h = 48;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_ParchmentBanner" };
            Color bg = new Color(0.92f, 0.87f, 0.78f, 1f);
            Color border = new Color(0.52f, 0.40f, 0.28f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x <= 2 || x >= w - 3 || y <= 2 || y >= h - 3);
                    tex.SetPixel(x, y, isBorder ? border : bg);
                }
            }
            tex.Apply();
            _bannerSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            return _bannerSprite;
        }
    }
}
