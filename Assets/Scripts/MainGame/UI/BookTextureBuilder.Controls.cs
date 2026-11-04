using UnityEngine;

namespace ForjaGame.UI
{
    /// <summary>
    /// Sprites procedurales 9-sliced para placas de título, sliders de forja, perillas de latón,
    /// agujas térmicas y botones de acción principales en todo el Canvas.
    /// </summary>
    public static partial class BookTextureBuilder
    {
        private static Sprite _titlePlaqueSprite;
        private static Sprite _sliderTrackSprite;
        private static Sprite _sliderFillSprite;
        private static Sprite _sliderKnobSprite;
        private static Sprite _thermalNeedleSprite;
        private static Sprite _actionButtonSprite;

        public static Sprite GetTitlePlaqueSprite()
        {
            if (_titlePlaqueSprite != null) return _titlePlaqueSprite;
            int w = 160, h = 56;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_TitlePlaque" };
            Color ironDark = new Color(0.13f, 0.09f, 0.07f, 0.98f);
            Color ironMid = new Color(0.21f, 0.14f, 0.10f, 0.98f);
            Color brass = new Color(0.83f, 0.66f, 0.31f, 1f);
            Color brassBright = new Color(0.96f, 0.83f, 0.46f, 1f);

            for (int y = 0; y < h; y++)
            {
                float vy = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    bool outerRim = x <= 2 || x >= w - 3 || y <= 2 || y >= h - 3;
                    bool goldFrame = x == 4 || x == 5 || x == w - 5 || x == w - 6 || y == 4 || y == 5 || y == h - 5 || y == h - 6;
                    bool rivet = ((x >= 10 && x <= 13) || (x >= w - 14 && x <= w - 11)) && ((y >= 10 && y <= 13) || (y >= h - 14 && y <= h - 11));

                    Color c = Color.Lerp(ironDark, ironMid, Mathf.Sin(vy * Mathf.PI));
                    if (outerRim) c = new Color(0.07f, 0.05f, 0.04f, 1f);
                    else if (goldFrame) c = y > h / 2 ? brassBright : brass;
                    else if (rivet) c = brassBright;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _titlePlaqueSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 14, 18, 14));
            return _titlePlaqueSprite;
        }

        public static Sprite GetSliderTrackSprite()
        {
            if (_sliderTrackSprite != null) return _sliderTrackSprite;
            int w = 128, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_SliderTrack" };
            Color recessTop = new Color(0.10f, 0.08f, 0.07f, 0.98f);
            Color recessBot = new Color(0.18f, 0.14f, 0.11f, 0.98f);
            Color rimBrass = new Color(0.68f, 0.53f, 0.28f, 1f);

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    bool border = x <= 2 || x >= w - 3 || y <= 2 || y >= h - 3;
                    Color c = border ? rimBrass : Color.Lerp(recessBot, recessTop, t);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _sliderTrackSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
            return _sliderTrackSprite;
        }

        public static Sprite GetSliderFillSprite()
        {
            if (_sliderFillSprite != null) return _sliderFillSprite;
            int w = 128, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_SliderFill" };
            Color deepAmber = new Color(0.76f, 0.36f, 0.08f, 1f);
            Color brightGold = new Color(0.98f, 0.78f, 0.24f, 1f);
            Color specular = new Color(1.0f, 0.93f, 0.62f, 1f);

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.Lerp(deepAmber, brightGold, Mathf.Sin(t * Mathf.PI * 0.85f));
                    if (y >= h - 6 && y <= h - 4) c = Color.Lerp(c, specular, 0.55f);
                    if (x == w - 1 || y == 0 || y == h - 1) c *= 0.82f;
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _sliderFillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            return _sliderFillSprite;
        }

        public static Sprite GetSliderKnobSprite()
        {
            if (_sliderKnobSprite != null) return _sliderKnobSprite;
            int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Tex_SliderKnob" };
            float half = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    if (dist > 1f) { tex.SetPixel(x, y, Color.clear); continue; }
                    Color c = dist > 0.82f
                        ? new Color(0.18f, 0.12f, 0.07f, 1f)
                        : dist > 0.64f
                            ? new Color(0.95f, 0.80f, 0.40f, 1f)
                            : Color.Lerp(new Color(0.98f, 0.86f, 0.50f, 1f), new Color(0.68f, 0.48f, 0.18f, 1f), dist);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _sliderKnobSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _sliderKnobSprite;
        }

        public static Sprite GetThermalNeedleSprite()
        {
            if (_thermalNeedleSprite != null) return _thermalNeedleSprite;
            int w = 20, h = 56;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_ThermalNeedle" };
            float cx = (w - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    bool cap = (y <= 7 || y >= h - 8) && dx <= 7f;
                    bool shaft = dx <= 2.5f;
                    if (!cap && !shaft) { tex.SetPixel(x, y, Color.clear); continue; }
                    bool rim = dx >= 2f && !cap || (cap && (y == 0 || y == h - 1 || dx >= 6f));
                    tex.SetPixel(x, y, rim ? new Color(0.10f, 0.07f, 0.05f, 1f) : new Color(1f, 0.93f, 0.68f, 1f));
                }
            }
            tex.Apply();
            _thermalNeedleSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _thermalNeedleSprite;
        }

        public static Sprite GetPrimaryActionButtonSprite()
        {
            if (_actionButtonSprite != null) return _actionButtonSprite;
            int w = 128, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_PrimaryActionBtn" };
            Color darkRim = new Color(0.24f, 0.13f, 0.06f, 1f);
            Color goldBorder = new Color(0.95f, 0.78f, 0.36f, 1f);
            Color warmTop = new Color(0.84f, 0.52f, 0.16f, 1f);
            Color warmBot = new Color(0.58f, 0.30f, 0.09f, 1f);

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    bool outer = x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2;
                    bool inner = x == 3 || x == w - 4 || y == 3 || y == h - 4;
                    Color c = outer ? darkRim : inner ? goldBorder : Color.Lerp(warmBot, warmTop, t);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _actionButtonSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
            return _actionButtonSprite;
        }
    }
}

