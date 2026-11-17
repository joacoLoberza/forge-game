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
        private static Sprite _speakerIconSprite;

        public static Sprite GetTitlePlaqueSprite()
        {
            if (_titlePlaqueSprite != null) return _titlePlaqueSprite;
            int w = 128, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_TitlePlaque" };
            Color woodDark = new Color(0.17f, 0.11f, 0.08f, 0.98f);
            Color woodMid = new Color(0.24f, 0.16f, 0.11f, 0.98f);
            Color rimDark = new Color(0.09f, 0.06f, 0.04f, 1f);
            Color brassLine = new Color(0.70f, 0.56f, 0.28f, 0.92f);

            for (int y = 0; y < h; y++)
            {
                float vy = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    bool outer = x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2;
                    bool innerLine = x == 4 || x == w - 5 || y == 4 || y == h - 5;
                    Color c = outer ? rimDark : (innerLine ? brassLine : Color.Lerp(woodDark, woodMid, Mathf.Sin(vy * Mathf.PI)));
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _titlePlaqueSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
            return _titlePlaqueSprite;
        }

        public static Sprite GetSliderTrackSprite()
        {
            if (_sliderTrackSprite != null) return _sliderTrackSprite;
            int w = 96, h = 18;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_SliderTrack" };
            Color recess = new Color(0.15f, 0.10f, 0.07f, 0.96f);
            Color groove = new Color(0.24f, 0.17f, 0.12f, 0.96f);
            Color rim = new Color(0.46f, 0.36f, 0.24f, 0.95f);

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    tex.SetPixel(x, y, border ? rim : Color.Lerp(recess, groove, t));
                }
            }
            tex.Apply();
            _sliderTrackSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 5, 6, 5));
            return _sliderTrackSprite;
        }

        public static Sprite GetSliderFillSprite()
        {
            if (_sliderFillSprite != null) return _sliderFillSprite;
            int w = 128, h = 28;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Tex_SliderFill" };
            Color bronze = new Color(0.66f, 0.28f, 0.06f, 1f);
            Color amber = new Color(0.92f, 0.58f, 0.14f, 1f);
            Color gold = new Color(0.99f, 0.84f, 0.32f, 1f);
            Color glint = new Color(1.0f, 0.96f, 0.72f, 1f);

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                float profile = Mathf.Sin(t * Mathf.PI);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    Color baseCol = Color.Lerp(amber, gold, u * 0.55f + profile * 0.45f);
                    Color c = Color.Lerp(bronze, baseCol, Mathf.Clamp01(profile * 1.15f));
                    if (y >= h - 6 && y <= h - 4) c = Color.Lerp(c, glint, 0.62f);
                    if (y <= 1 || y >= h - 2 || x == 0 || x == w - 1) c *= 0.78f;
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _sliderFillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 5, 6, 5));
            return _sliderFillSprite;
        }

        public static Sprite GetSliderKnobSprite()
        {
            if (_sliderKnobSprite != null) return _sliderKnobSprite;
            int s = 36;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Tex_SliderKnob" };
            float half = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            {
                float dy = (y - half) / half;
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r > 0.94f) { tex.SetPixel(x, y, Color.clear); continue; }
                    float shade = Mathf.Clamp01(0.55f + 0.35f * (dy - dx * 0.35f));
                    Color c = r > 0.78f
                        ? new Color(0.18f, 0.11f, 0.06f, 1f)
                        : Color.Lerp(new Color(0.66f, 0.48f, 0.18f, 1f), new Color(0.95f, 0.82f, 0.44f, 1f), shade);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _sliderKnobSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _sliderKnobSprite;
        }

        public static Sprite GetSpeakerIconSprite()
        {
            if (_speakerIconSprite != null) return _speakerIconSprite;
            int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Tex_SpeakerIcon" };
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - 15.5f, dy = Mathf.Abs(y - 15.5f);
                    bool body = x >= 4 && x <= 9 && dy <= 4.5f;
                    bool cone = x > 9 && x <= 17 && dy <= 4.5f + (x - 9) * 0.85f;
                    float r = Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 15.5f) * (y - 15.5f));
                    bool arc1 = x >= 19 && Mathf.Abs(r - 6.5f) <= 1.1f && dy <= 6.2f;
                    bool arc2 = x >= 22 && Mathf.Abs(r - 10.5f) <= 1.1f && dy <= 9.2f;
                    tex.SetPixel(x, y, (body || cone || arc1 || arc2) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            _speakerIconSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _speakerIconSprite;
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

