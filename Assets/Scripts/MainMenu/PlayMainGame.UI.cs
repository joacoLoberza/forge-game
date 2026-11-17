using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI;
using ForjaGame.Audio;

public partial class PlayMainGame
{
    private Transform _mainBookRoot, _bookLeftPage, _bookRightPage;
    private GameObject _screenTitleLeft, _screenTitleRight;
    private GameObject _screenModeLeft, _screenModeRight;
    private GameObject _screenSingleLeft, _screenSingleRight;
    private GameObject _screenMultiLeft, _screenMultiRight;

    private TMP_InputField _singlePlayerNameInput, _multiPlayerNameInput, _ipInputField;
    private TMP_Text _statusLabel, _clientBtnLabel, _volValueLabel;
    private InventoryPageFlipper _menuPageFlipper;
    private BookTabIndicator _tabChronicle, _tabModes, _tabHeroes;
    private bool _menuBookInitialized;

    private void BuildMedievalMainMenuUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        ForgeCanvasThemeStyler.ConfigureAllCanvasScalers();
        if (TryGetComponent<Button>(out var selfBtn)) selfBtn.enabled = false;
        foreach (var g in GetComponents<Graphic>()) g.enabled = false;
        if (playButtonText != null) playButtonText.gameObject.SetActive(false);

        Transform settingsLegacy = canvas.transform.Find("Settings");
        if (settingsLegacy != null) settingsLegacy.gameObject.SetActive(false);

        Transform oldRoot = canvas.transform.Find("MedievalMainMenuRoot");
        if (oldRoot != null) { oldRoot.name = "MedievalMainMenuRoot_Old"; Destroy(oldRoot.gameObject); }

        var rootGo = new GameObject("MedievalMainMenuRoot", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(canvas.transform, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one; rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        var bgImg = rootGo.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.03f, 0.02f, 0.16f);
        bgImg.raycastTarget = false;

        var bookGo = new GameObject("MainMenuBookShell", typeof(RectTransform));
        bookGo.transform.SetParent(rootGo.transform, false);
        _mainBookRoot = bookGo.transform;

        var (leftRt, rightRt) = BookJournalTemplateBuilder.EnsureOpenBookShell(_mainBookRoot);
        _bookLeftPage = leftRt;
        _bookRightPage = rightRt;

        BuildBookNavigationTabs();
        BookJournalTemplateBuilder.CreateOrGetCloseBookmarkTab(_mainBookRoot, "BtnMenuQuitTab", "Quit", QuitGame);

        BuildTitleScreenSpread();
        BuildModeSelectionSpread();
        BuildSingleplayerSetupSpread();
        BuildMultiplayerSetupSpread();
        BuildLobbyScreenSpread();
        BuildLeaderboardScreenSpread();

        _menuPageFlipper = bookGo.AddComponent<InventoryPageFlipper>();
        _menuPageFlipper.BindTargets(_mainBookRoot, _bookLeftPage, _bookRightPage);

        MedievalTypographyProvider.ApplyToAllTMPInHierarchy(rootGo.transform);
        _menuBookInitialized = false;
        SwitchScreen(MainMenuScreen.Title);
        _menuBookInitialized = true;
    }

    private void BuildBookNavigationTabs()
    {
        var tabsGo = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabsGo.transform.SetParent(_mainBookRoot, false);
        Transform stackLeft = _mainBookRoot.Find("BookPageStackLeft");
        if (stackLeft != null) tabsGo.transform.SetSiblingIndex(stackLeft.GetSiblingIndex());

        var tabsRt = tabsGo.GetComponent<RectTransform>();
        tabsRt.anchorMin = tabsRt.anchorMax = new Vector2(0.5f, 1f); tabsRt.pivot = new Vector2(0.5f, 0f);
        tabsRt.anchoredPosition = new Vector2(-BookJournalTemplateBuilder.PageOffsetX, -52f);
        tabsRt.sizeDelta = new Vector2(360f, 52f);

        var hlg = tabsGo.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.LowerCenter;
        hlg.childControlWidth = hlg.childControlHeight = hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.spacing = 6f;

        _tabChronicle = CreateBookNavTab(tabsGo.transform, "TabChronicle", "Chronicle", new Color(0.68f, 0.24f, 0.18f), () => SwitchScreen(MainMenuScreen.Title));
        _tabModes = CreateBookNavTab(tabsGo.transform, "TabModes", "Modes", new Color(0.76f, 0.54f, 0.16f), OpenModeSelection);
        _tabHeroes = CreateBookNavTab(tabsGo.transform, "TabHeroes", "Heroes", new Color(0.28f, 0.44f, 0.58f), OpenLeaderboardScreen);
    }

    private static BookTabIndicator CreateBookNavTab(Transform parent, string name, string label, Color activeCol, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(BookTabIndicator));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(106f, 40f);
        var img = go.GetComponent<Image>(); img.sprite = BookTextureBuilder.GetRibbonTabSprite(); img.type = Image.Type.Sliced;
        var tGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        tGo.transform.SetParent(go.transform, false);
        var tRt = tGo.GetComponent<RectTransform>(); tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        var tmp = tGo.GetComponent<TextMeshProUGUI>();
        MedievalTypographyProvider.ApplyToTMP(tmp); tmp.text = label; tmp.fontSize = 11.5f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Top; tmp.margin = new Vector4(4f, 5f, 4f, 12f); tmp.raycastTarget = false;
        var btn = go.GetComponent<Button>(); btn.onClick.AddListener(onClick);
        var ind = go.GetComponent<BookTabIndicator>();
        ind.Setup(activeCol, new Color(0.36f, 0.26f, 0.20f), "•", 10f);
        return ind;
    }

    private static GameObject CreateSpreadLayer(Transform pageParent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(pageParent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    private void BuildTitleScreenSpread()
    {
        _screenTitleLeft = CreateSpreadLayer(_bookLeftPage, "TitleLeft");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenTitleLeft.transform, "TitleBanner", "CHRONICLES OF THE FORGE",
            new Vector2(0f, 178f), new Vector2(354f, 48f), fontSize: 18.5f);
        CreateMenuLabel(_screenTitleLeft.transform, "Subtitle",
            "<i>Underground Mining  •  Artisan Blacksmithing  •  LAN Co-op</i>",
            new Vector2(0f, 128f), new Vector2(350f, 26f), 13.5f, ForgeCanvasThemeStyler.SubInk, FontStyles.Italic);
        CreateMenuLabel(_screenTitleLeft.transform, "TitleLore",
            "Light the smelting furnace, balance metal alloys under the mechanical press, shape sharp blades upon the anvil, and descend into the cavern to face the Titan.\n\n• <b>Singleplayer:</b> Forge at your own pace.\n• <b>Multiplayer LAN:</b> Gather up to 4 smiths together.",
            new Vector2(0f, -10f), new Vector2(342f, 200f), 13.5f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Normal).alignment = TextAlignmentOptions.TopLeft;
        CreateMenuLabel(_screenTitleLeft.transform, "Credits", "Antigravity Forge Engine • Version 1.0",
            new Vector2(0f, -190f), new Vector2(340f, 24f), 11.5f, ForgeCanvasThemeStyler.SubInk, FontStyles.Italic);

        _screenTitleRight = CreateSpreadLayer(_bookRightPage, "TitleRight");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenTitleRight.transform, "TitleActionHdr", "EXPEDITION LEDGER",
            new Vector2(0f, 178f), new Vector2(344f, 42f), fontSize: 17f);
        CreateMenuButton(_screenTitleRight.transform, "BtnPlay", "PLAY", new Vector2(0f, 102f), new Vector2(316f, 52f), true, OpenModeSelection);
        CreateMenuButton(_screenTitleRight.transform, "BtnLeaderboard", "HALL OF HEROES (SPEEDRUN)", new Vector2(0f, 38f), new Vector2(316f, 44f), false, OpenLeaderboardScreen);
        CreateSectionDivider(_screenTitleRight.transform, "DivSettings", "WORKSHOP SETTINGS", new Vector2(0f, -20f));
        BuildVolumeControlRow(_screenTitleRight.transform, new Vector2(0f, -68f));
        CreateMenuButton(_screenTitleRight.transform, "BtnQuit", "QUIT GAME", new Vector2(0f, -158f), new Vector2(220f, 40f), false, QuitGame);
    }

    public static TMP_Text CreateMenuLabel(
        Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        MedievalTypographyProvider.ApplyToTMP(tmp);
        tmp.text = text; tmp.fontSize = fontSize; tmp.color = color; tmp.fontStyle = style;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 9.5f; tmp.fontSizeMax = fontSize;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        return tmp;
    }

    public static GameObject CreateMenuButton(
        Transform parent, string name, string text, Vector2 pos, Vector2 size, bool primary, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var btn = go.GetComponent<Button>(); btn.targetGraphic = go.GetComponent<Image>();
        var lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGo.transform.SetParent(go.transform, false);
        var lRt = lblGo.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
        lRt.offsetMin = new Vector2(8f, 4f); lRt.offsetMax = new Vector2(-8f, -4f);
        lblGo.GetComponent<TextMeshProUGUI>().text = text;
        ForgeCanvasThemeStyler.StyleForgeButton(btn, primary, parent, pos, size);
        if (action != null) btn.onClick.AddListener(action);
        return go;
    }

    public static void CreateSectionDivider(Transform parent, string name, string title, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(340f, 22f);
        var tGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        tGo.transform.SetParent(go.transform, false);
        var tRt = tGo.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        var tmp = tGo.GetComponent<TextMeshProUGUI>();
        MedievalTypographyProvider.ApplyToTMP(tmp);
        tmp.text = $"<color=#8C7051>—</color>  <b>{title}</b>  <color=#8C7051>—</color>";
        tmp.fontSize = 12f; tmp.color = ForgeCanvasThemeStyler.DarkInk;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
    }

    public static TMP_InputField CreateMedievalInputField(
        Transform parent, string name, string initialValue, string placeholderText, Vector2 pos, Vector2 size, int charLimit)
    {
        var boxGo = new GameObject(name, typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(parent, false);
        var bRt = boxGo.GetComponent<RectTransform>();
        bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.anchoredPosition = pos; bRt.sizeDelta = size;
        var bg = boxGo.GetComponent<Image>();
        bg.sprite = BookTextureBuilder.GetSliderTrackSprite(); bg.type = Image.Type.Sliced; bg.color = new Color(0.14f, 0.10f, 0.08f, 0.96f);

        var aGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        aGo.transform.SetParent(boxGo.transform, false);
        var aRt = aGo.GetComponent<RectTransform>(); aRt.anchorMin = Vector2.zero; aRt.anchorMax = Vector2.one;
        aRt.offsetMin = new Vector2(10f, 4f); aRt.offsetMax = new Vector2(-10f, -4f);

        var phTmp = CreateMenuLabel(aGo.transform, "Placeholder", placeholderText, Vector2.zero, size, 14f, new Color(0.65f, 0.58f, 0.50f, 0.65f), FontStyles.Italic);
        phTmp.rectTransform.anchorMin = Vector2.zero; phTmp.rectTransform.anchorMax = Vector2.one; phTmp.rectTransform.offsetMin = phTmp.rectTransform.offsetMax = Vector2.zero;
        phTmp.alignment = TextAlignmentOptions.MidlineLeft;
        var txtTmp = CreateMenuLabel(aGo.transform, "Text", "", Vector2.zero, size, 15f, ForgeCanvasThemeStyler.GoldTitleInk, FontStyles.Bold);
        txtTmp.rectTransform.anchorMin = Vector2.zero; txtTmp.rectTransform.anchorMax = Vector2.one; txtTmp.rectTransform.offsetMin = txtTmp.rectTransform.offsetMax = Vector2.zero;
        txtTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var input = boxGo.AddComponent<TMP_InputField>();
        input.targetGraphic = bg; input.textViewport = aRt; input.textComponent = txtTmp;
        input.placeholder = phTmp; input.fontAsset = txtTmp.font; input.pointSize = 15f;
        input.characterLimit = charLimit; input.text = initialValue;
        return input;
    }

    private void BuildVolumeControlRow(Transform parent, Vector2 pos)
    {
        float vol = ForgeAudioSystem.GetMasterVolume();
        var (_, pctLbl) = ForgeCanvasThemeStyler.EnsureStyledVolumeChassis(
            parent, "VolumeChassis", pos, 344f, vol, v => ForgeAudioSystem.SetMasterVolume(v));
        _volValueLabel = pctLbl;
    }
}
