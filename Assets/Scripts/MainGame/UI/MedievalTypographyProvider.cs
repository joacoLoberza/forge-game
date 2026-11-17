using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Proveedor centralizado y a prueba de fallos de tipografía serif medieval/renacentista.
    /// Resuelve fuentes reales de Windows (Georgia, Palatino Linotype, Constantia, Times New Roman)
    /// mediante el motor nativo de TextMeshPro (DynamicOS) y provee fallback automático a IMGUI.
    /// </summary>
    public static class MedievalTypographyProvider
    {
        private static readonly string[] PreferredSerifFamilies =
        {
            "Georgia",
            "Palatino Linotype",
            "Constantia",
            "Book Antiqua",
            "Times New Roman",
            "Cambria"
        };

        private static Font _cachedOsFont;
        private static TMP_FontAsset _cachedTmpFont;
        private static bool _initialized;

        public static Font GetIMGUIFont()
        {
            EnsureInitialized();
            return _cachedOsFont;
        }

        public static TMP_FontAsset GetTMPFont()
        {
            EnsureInitialized();
            return _cachedTmpFont;
        }

        public static string SanitizeForTMP(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            input = input
                .Replace("[🏆]", "")
                .Replace("[□]", "")
                .Replace("[\u25A1]", "")
                .Replace("[R]", "")
                .Replace("★", "*")
                .Replace("⏱", "")
                .Replace("«", "")
                .Replace("»", "")
                .Replace("▶", "")
                .Replace("—", "-")
                .Replace("…", "..")
                .Replace("🏆", "")
                .Replace("🥇", "1.")
                .Replace("🥈", "2.")
                .Replace("🔄", "")
                .Replace("□", "")
                .Replace("\u25A1", "")
                .Replace("¤", "");

            var sb = new System.Text.StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsSurrogate(c))
                {
                    if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                        i++;
                    continue;
                }
                if (c == '\u25A1' || c == '□' || (c >= 0x25A0 && c <= 0x25FF) || c >= 0xE000) continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public static void ApplyToTMP(TMP_Text text)
        {
            if (text == null || !Application.isPlaying) return;
            if (!string.IsNullOrEmpty(text.text)) text.text = SanitizeForTMP(text.text);
            TMP_FontAsset f = GetTMPFont();
            if (f != null && f.material != null && text.font != f)
            {
                text.font = f;
                text.UpdateMeshPadding();
            }
        }

        public static void ApplyToAllTMPInHierarchy(Transform root)
        {
            if (root == null || !Application.isPlaying) return;
            TMP_FontAsset f = GetTMPFont();
            if (f == null || f.material == null) return;

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t != null)
                {
                    if (!string.IsNullOrEmpty(t.text)) t.text = SanitizeForTMP(t.text);
                    if (t.font != f)
                    {
                        t.font = f;
                        t.UpdateMeshPadding();
                    }
                }
            }
        }

        public static void ApplyToAllSceneTMP()
        {
            if (!Application.isPlaying) return;
            TMP_FontAsset f = GetTMPFont();
            if (f == null || f.material == null) return;

            var all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                {
                    if (!string.IsNullOrEmpty(all[i].text)) all[i].text = SanitizeForTMP(all[i].text);
                    if (all[i].font != f)
                    {
                        all[i].font = f;
                        all[i].UpdateMeshPadding();
                    }
                }
            }
        }

        public static void ApplyToGUIStyle(GUIStyle style, int fontSize = 0, FontStyle fontStyle = FontStyle.Normal)
        {
            if (style == null) return;
            Font f = GetIMGUIFont();
            if (f != null) style.font = f;
            if (fontSize > 0) style.fontSize = fontSize;
            style.fontStyle = fontStyle;
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                _cachedOsFont = Font.CreateDynamicFontFromOSFont(PreferredSerifFamilies, 28);
                if (_cachedOsFont != null) _cachedOsFont.hideFlags = HideFlags.DontSave;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MedievalTypographyProvider] Error cargando fuente IMGUI: {ex.Message}");
            }

            for (int i = 0; i < PreferredSerifFamilies.Length; i++)
            {
                string family = PreferredSerifFamilies[i];
                try
                {
                    _cachedTmpFont = TMP_FontAsset.CreateFontAsset(family, "Regular", 90);
                    if (_cachedTmpFont != null)
                    {
                        _cachedTmpFont.name = $"ForjaSerif_{family}_TMP";
                        _cachedTmpFont.hideFlags = HideFlags.DontSave;
                        if (TMP_Settings.defaultFontAsset != null && _cachedTmpFont.fallbackFontAssetTable != null)
                        {
                            if (!_cachedTmpFont.fallbackFontAssetTable.Contains(TMP_Settings.defaultFontAsset))
                                _cachedTmpFont.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
                        }
                        break;
                    }
                }
                catch (System.Exception) { }
            }

            if (_cachedTmpFont == null || _cachedTmpFont.material == null)
                _cachedTmpFont = TMP_Settings.defaultFontAsset;
        }
    }
}
