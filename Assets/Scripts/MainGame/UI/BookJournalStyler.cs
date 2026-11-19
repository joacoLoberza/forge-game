using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForjaGame.UI
{
    public static partial class BookJournalStyler
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitBookStyling() => ApplyStyleToAll();

        public static void ApplyStyleToAll()
        {
            StyleInventoryJournal();
            StylePauseMenuPanel();
        }

        public static void ApplyBookTheme() => ApplyStyleToAll();

        public static void StyleInventoryJournal()
        {
            var inv = Object.FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            if (inv == null) return;

            Transform root = inv.transform;
            var (leftRt, rightRt) = BookJournalTemplateBuilder.EnsureOpenBookShell(root, resetRootRect: true, bookScale: 1.22f);

            Transform leftPage = leftRt != null ? leftRt : root.Find("LeftPage");
            if (leftPage != null) StyleLeftPage(leftPage);

            Transform rightPage = rightRt != null ? rightRt : root.Find("RightPage");
            if (rightPage != null) StyleRightPage(rightPage);

            // Solapas de categoría detrás del borde superior de la Página Izquierda (sin cruzar el lomo central)
            StyleBookmarkTabs(root);
            // Botón de cierre como otra solapa-marcapáginas detrás del borde superior derecho de la Página Derecha
            StyleCloseButton(root);
            StyleTexts(root);

            var flipper = inv.GetComponent<InventoryPageFlipper>() ?? inv.gameObject.AddComponent<InventoryPageFlipper>();
            flipper.BindTargets(root, leftPage, rightPage);
        }

        private static void StyleCloseButton(Transform root)
        {
            Button closeBtn = null;
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                string n = b.name.ToLowerInvariant();
                if (n.Contains("close") || n.Contains("cerrar")) { closeBtn = b; break; }
            }
            if (closeBtn != null)
                BookJournalTemplateBuilder.StyleCloseBookmarkTab(closeBtn, root, "Close [E]");
        }

        private static void StyleBookmarkTabs(Transform root)
        {
            Transform tabs = root.Find("Tabs");
            if (tabs == null) return;

            // Situar detrás de pilas de papel y páginas para asomar por la Página Izquierda únicamente
            Transform stackLeft = root.Find("BookPageStackLeft");
            if (stackLeft != null) tabs.SetSiblingIndex(stackLeft.GetSiblingIndex());
            else tabs.SetAsFirstSibling();

            var tabsRt = tabs.GetComponent<RectTransform>();
            if (tabsRt != null)
            {
                tabsRt.localRotation = Quaternion.identity; tabsRt.localScale = Vector3.one;
                tabsRt.anchorMin = tabsRt.anchorMax = new Vector2(0.5f, 1f);
                tabsRt.pivot = new Vector2(0.5f, 0f);
                tabsRt.anchoredPosition = new Vector2(-BookJournalTemplateBuilder.PageOffsetX, -52f);
                tabsRt.sizeDelta = new Vector2(394f, 52f);
            }

            var hlg = tabs.GetComponent<HorizontalLayoutGroup>() ?? tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.LowerCenter;
            hlg.childControlWidth = hlg.childControlHeight = hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            hlg.spacing = 4f;

            var buttons = tabs.GetComponentsInChildren<Button>(true);
            Transform rightPage = root.Find("RightPage");
            foreach (var btn in buttons)
            {
                string n = btn.name.ToLowerInvariant();
                if (n.Contains("equip") && rightPage != null)
                {
                    btn.transform.SetParent(rightPage, false);
                    continue;
                }
                if (n.Contains("close") || n.Contains("cerrar"))
                {
                    btn.transform.SetParent(root, false);
                    continue;
                }

                var btnRt = btn.GetComponent<RectTransform>();
                if (btnRt != null)
                {
                    btnRt.localRotation = Quaternion.identity; btnRt.localScale = Vector3.one;
                    btnRt.pivot = new Vector2(0.5f, 0f);
                    btnRt.sizeDelta = new Vector2(74f, 40f);
                }

                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };

                var img = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
                img.sprite = BookTextureBuilder.GetRibbonTabSprite();
                img.type = Image.Type.Sliced;
                img.raycastTarget = true;

                var txt = btn.GetComponentInChildren<TMP_Text>(true);
                if (txt != null)
                {
                    MedievalTypographyProvider.ApplyToTMP(txt);
                    txt.margin = new Vector4(3f, 5f, 3f, 12f);
                    txt.raycastTarget = false;
                    txt.enableAutoSizing = true;
                    txt.fontSizeMin = 8.5f;
                    txt.fontSizeMax = 12f;
                    txt.fontStyle = FontStyles.Bold;
                    txt.alignment = TextAlignmentOptions.Top;
                }

                var ind = btn.GetComponent<BookTabIndicator>() ?? btn.gameObject.AddComponent<BookTabIndicator>();
                if (n.Contains("mineral") || n.Contains("raw")) { ind.Setup(new Color(0.68f, 0.24f, 0.18f), new Color(0.38f, 0.26f, 0.21f), "•", 10f); if (txt != null) txt.text = "Ores"; }
                else if (n.Contains("bloom")) { ind.Setup(new Color(0.82f, 0.46f, 0.16f), new Color(0.39f, 0.28f, 0.20f), "•", 10f); if (txt != null) txt.text = "Blooms"; }
                else if (n.Contains("ingot")) { ind.Setup(new Color(0.28f, 0.44f, 0.58f), new Color(0.27f, 0.30f, 0.33f), "•", 10f); if (txt != null) txt.text = "Ingots"; }
                else if (n.Contains("handle")) { ind.Setup(new Color(0.48f, 0.33f, 0.19f), new Color(0.32f, 0.24f, 0.18f), "•", 10f); if (txt != null) txt.text = "Hilts"; }
                else if (n.Contains("weapon")) { ind.Setup(new Color(0.76f, 0.58f, 0.16f), new Color(0.36f, 0.31f, 0.20f), "•", 10f); if (txt != null) txt.text = "Weapons"; }
                else ind.Setup(new Color(0.56f, 0.42f, 0.30f), new Color(0.32f, 0.25f, 0.20f), "•", 10f);
            }
        }
    }
}
