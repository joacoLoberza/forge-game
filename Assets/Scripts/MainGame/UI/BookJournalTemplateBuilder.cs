using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    /// <summary>
    /// Plantilla compartida de libro abierto encuadernado (tapa de cuero, pila de hojas,
    /// página izquierda, página derecha, lomo central y solapa-marcapáginas de cierre).
    /// Reutilizada por Inventario, Menú de Pausa y Menú Principal para cohesión visual total.
    /// </summary>
    public static class BookJournalTemplateBuilder
    {
        public static readonly Vector2 BookSize = new Vector2(900f, 540f);
        public static readonly Vector2 PageSize = new Vector2(414f, 462f);
        public const float PageOffsetX = 216f;
        public const float PageOffsetY = -10f;

        public static (RectTransform leftPage, RectTransform rightPage) EnsureOpenBookShell(Transform root, bool resetRootRect = true)
        {
            if (root == null) return (null, null);

            if (resetRootRect && root.TryGetComponent<RectTransform>(out var rt))
            {
                rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = BookSize;
            }

            var rootImg = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            rootImg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
            rootImg.type = Image.Type.Sliced;
            rootImg.color = new Color(0.20f, 0.13f, 0.09f, 0.99f);
            rootImg.raycastTarget = true;

            // Pilas de papel detrás de las páginas abiertas
            EnsurePageStack(root, "BookPageStackLeft", new Vector2(-PageOffsetX - 2f, PageOffsetY - 3f));
            EnsurePageStack(root, "BookPageStackRight", new Vector2(PageOffsetX + 2f, PageOffsetY - 3f));

            RectTransform leftRt = EnsurePageSheet(root, "LeftPage", new Vector2(-PageOffsetX, PageOffsetY));
            RectTransform rightRt = EnsurePageSheet(root, "RightPage", new Vector2(PageOffsetX, PageOffsetY));

            EnsureBookSpine(root);
            return (leftRt, rightRt);
        }

        public static RectTransform EnsurePageStack(Transform root, string stackName, Vector2 pos)
        {
            Transform existing = root.Find(stackName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(stackName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = PageSize + new Vector2(12f, 10f);
            var img = go.GetComponent<Image>();
            img.sprite = BookTextureBuilder.GetPageStackSprite();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
            return rt;
        }

        private static RectTransform EnsurePageSheet(Transform root, string pageName, Vector2 pos)
        {
            Transform existing = root.Find(pageName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(pageName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = PageSize;
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = BookTextureBuilder.GetParchmentSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.97f, 0.94f, 0.88f, 1f);
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
            return rt;
        }

        public static void EnsureBookSpine(Transform root)
        {
            Transform existing = root.Find("BookSpine");
            GameObject spine = existing != null ? existing.gameObject : new GameObject("BookSpine", typeof(RectTransform), typeof(Image));
            spine.transform.SetParent(root, false);

            var rt = spine.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, PageOffsetY);
            rt.sizeDelta = new Vector2(26f, PageSize.y + 8f);

            var img = spine.GetComponent<Image>();
            img.sprite = BookTextureBuilder.GetSpineSprite();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
            spine.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Estiliza un botón como solapa-marcapáginas que asoma por detrás del borde superior derecho
        /// de la página derecha (coherente con las solapas de categoría del libro).
        /// </summary>
        public static void StyleCloseBookmarkTab(Button closeBtn, Transform root, string label = "Close", Color? tabTint = null)
        {
            if (closeBtn == null || root == null) return;
            closeBtn.transform.SetParent(root, false);

            // Detrás de pilas y hojas para que salga asomando del borde superior derecho
            Transform stackLeft = root.Find("BookPageStackLeft");
            if (stackLeft != null) closeBtn.transform.SetSiblingIndex(stackLeft.GetSiblingIndex());
            else closeBtn.transform.SetAsFirstSibling();

            var cRt = closeBtn.GetComponent<RectTransform>();
            cRt.localRotation = Quaternion.identity; cRt.localScale = Vector3.one;
            cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 1f);
            cRt.pivot = new Vector2(0.5f, 0f);
            cRt.anchoredPosition = new Vector2(342f, -52f);
            cRt.sizeDelta = new Vector2(106f, 44f);

            var img = closeBtn.GetComponent<Image>() ?? closeBtn.gameObject.AddComponent<Image>();
            img.sprite = BookTextureBuilder.GetRibbonTabSprite();
            img.type = Image.Type.Sliced;
            img.color = tabTint ?? new Color(0.58f, 0.16f, 0.14f, 1f);
            img.raycastTarget = true;

            closeBtn.transition = Selectable.Transition.ColorTint;
            var cb = closeBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.16f, 1.08f, 1.04f, 1f);
            cb.pressedColor = new Color(0.82f, 0.72f, 0.70f, 1f);
            closeBtn.colors = cb;

            var cTxt = closeBtn.GetComponentInChildren<TMP_Text>(true);
            if (cTxt != null)
            {
                MedievalTypographyProvider.ApplyToTMP(cTxt);
                cTxt.rectTransform.anchorMin = Vector2.zero; cTxt.rectTransform.anchorMax = Vector2.one;
                cTxt.rectTransform.offsetMin = Vector2.zero; cTxt.rectTransform.offsetMax = Vector2.zero;
                cTxt.margin = new Vector4(4f, 5f, 4f, 12f);
                cTxt.text = label;
                cTxt.fontSize = 12f;
                cTxt.enableAutoSizing = true;
                cTxt.fontSizeMin = 9f;
                cTxt.fontSizeMax = 12.5f;
                cTxt.color = ForgeCanvasThemeStyler.GoldTitleInk;
                cTxt.fontStyle = FontStyles.Bold;
                cTxt.alignment = TextAlignmentOptions.Top;
                cTxt.raycastTarget = false;
            }
        }

        public static Button CreateOrGetCloseBookmarkTab(Transform root, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            Transform tr = root.Find(name);
            GameObject go = tr != null ? tr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            if (tr == null)
            {
                var tGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.transform.SetParent(go.transform, false);
            }
            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            if (onClick != null) btn.onClick.AddListener(onClick);
            StyleCloseBookmarkTab(btn, root, label);
            return btn;
        }
    }
}

