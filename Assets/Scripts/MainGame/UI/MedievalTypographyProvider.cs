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

        public static void ApplyToTMP(TMP_Text text)
        {
            if (text == null) return;
            TMP_FontAsset f = GetTMPFont();
            if (f != null && text.font != f)
            {
                text.font = f;
                text.UpdateMeshPadding();
            }
        }

        public static void ApplyToAllTMPInHierarchy(Transform root)
        {
            if (root == null) return;
            TMP_FontAsset f = GetTMPFont();
            if (f == null) return;

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != f)
                {
                    texts[i].font = f;
                    texts[i].UpdateMeshPadding();
                }
            }
        }

        public static void ApplyToAllSceneTMP()
        {
            TMP_FontAsset f = GetTMPFont();
            if (f == null) return;

            var all = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].font != f)
                {
                    all[i].font = f;
                    all[i].UpdateMeshPadding();
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

            if (_cachedTmpFont == null)
                _cachedTmpFont = TMP_Settings.defaultFontAsset;
        }
    }
}
