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
            var rt = root.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(880f, 540f);
            }

            var rootImg = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
                rootImg.type = Image.Type.Sliced;
                rootImg.color = new Color(0.18f, 0.12f, 0.08f, 0.98f);
            }

            Transform leftPage = root.Find("LeftPage");
            if (leftPage != null) StyleLeftPage(leftPage);

            Transform rightPage = root.Find("RightPage");
            if (rightPage != null) StyleRightPage(rightPage);

            EnsureBookSpine(root);
            StyleBookmarkTabs(root);
            StyleTexts(root);
        }

        private static void EnsureBookSpine(Transform root)
        {
            Transform existing = root.Find("BookSpine");
            GameObject spine = existing != null ? existing.gameObject : new GameObject("BookSpine", typeof(RectTransform), typeof(Image));
            spine.transform.SetParent(root, false);

            var rt = spine.GetComponent<RectTransform>();
            rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -10f);
            rt.sizeDelta = new Vector2(26f, 470f);

            var img = spine.GetComponent<Image>();
            img.sprite = BookTextureBuilder.GetSpineSprite();
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            spine.transform.SetSiblingIndex(Mathf.Max(0, root.childCount - 2));
        }

        private static void StyleBookmarkTabs(Transform root)
        {
            Transform tabs = root.Find("Tabs");
            if (tabs == null) return;
            tabs.SetAsLastSibling();

            var tabsRt = tabs.GetComponent<RectTransform>();
            if (tabsRt != null)
            {
                tabsRt.localRotation = Quaternion.identity; tabsRt.localScale = Vector3.one;
                tabsRt.anchorMin = tabsRt.anchorMax = new Vector2(0.5f, 1f);
                tabsRt.pivot = new Vector2(0.5f, 0f);
                tabsRt.anchoredPosition = new Vector2(0f, -8f);
                tabsRt.sizeDelta = new Vector2(680f, 40f);
            }

            var hlg = tabs.GetComponent<HorizontalLayoutGroup>() ?? tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.LowerCenter;
            hlg.childControlWidth = hlg.childControlHeight = hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            hlg.spacing = 8f;

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
                    var cRt = btn.GetComponent<RectTransform>();
                    cRt.localRotation = Quaternion.identity; cRt.localScale = Vector3.one;
                    cRt.anchorMin = cRt.anchorMax = cRt.pivot = new Vector2(1f, 1f);
                    cRt.anchoredPosition = new Vector2(-14f, -12f);
                    cRt.sizeDelta = new Vector2(108f, 32f);
                    var cImg = btn.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
                    cImg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
                    cImg.type = Image.Type.Sliced;
                    cImg.color = Color.white;
                    var cTxt = btn.GetComponentInChildren<TMP_Text>(true);
                    if (cTxt != null)
                    {
                        MedievalTypographyProvider.ApplyToTMP(cTxt);
                        cTxt.text = "Cerrar [I]";
                        cTxt.fontSize = 12.5f;
                        cTxt.color = new Color(0.95f, 0.90f, 0.80f, 1f);
                        cTxt.fontStyle = FontStyles.Bold;
                        cTxt.alignment = TextAlignmentOptions.Center;
                    }
                    continue;
                }

                var btnRt = btn.GetComponent<RectTransform>();
                if (btnRt != null)
                {
                    btnRt.localRotation = Quaternion.identity; btnRt.localScale = Vector3.one;
                    btnRt.pivot = new Vector2(0.5f, 0f);
                    btnRt.sizeDelta = new Vector2(118f, 36f);
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
                    txt.margin = Vector4.zero;
                    txt.raycastTarget = false;
                    txt.enableAutoSizing = true;
                    txt.fontSizeMin = 10f;
                    txt.fontSizeMax = 13.5f;
                    txt.fontStyle = FontStyles.Bold;
                    txt.alignment = TextAlignmentOptions.Center;
                }

                var ind = btn.GetComponent<BookTabIndicator>() ?? btn.gameObject.AddComponent<BookTabIndicator>();
                if (n.Contains("mineral") || n.Contains("raw"))
                    ind.Setup(new Color(0.72f, 0.22f, 0.18f), new Color(0.38f, 0.24f, 0.20f), "•", 10f);
                else if (n.Contains("bloom"))
                    ind.Setup(new Color(0.85f, 0.48f, 0.14f), new Color(0.38f, 0.26f, 0.18f), "•", 10f);
                else if (n.Contains("ingot"))
                    ind.Setup(new Color(0.26f, 0.42f, 0.56f), new Color(0.24f, 0.26f, 0.28f), "•", 10f);
                else if (n.Contains("handle"))
                    ind.Setup(new Color(0.46f, 0.32f, 0.18f), new Color(0.30f, 0.22f, 0.16f), "•", 10f);
                else if (n.Contains("weapon"))
                    ind.Setup(new Color(0.82f, 0.64f, 0.16f), new Color(0.36f, 0.30f, 0.18f), "•", 10f);
                else
                    ind.Setup(new Color(0.58f, 0.44f, 0.32f), new Color(0.32f, 0.25f, 0.20f), "•", 10f);
            }
        }
    }
}
