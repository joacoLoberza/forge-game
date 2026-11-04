using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI;
using ForjaGame.Audio;

public partial class PlayMainGame
{
    private TMP_InputField _ipInputField;
    private TMP_Text _statusLabel;
    private TMP_Text _clientBtnLabel;
    private TMP_Text _volValueLabel;
    private Button _soloButton;
    private Button _hostButton;
    private Button _clientButton;

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
        if (canvas.transform.Find("MedievalMainMenuRoot") != null) return;

        var rootGo = new GameObject("MedievalMainMenuRoot", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(canvas.transform, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
        rootGo.GetComponent<Image>().color = new Color(0.05f, 0.03f, 0.02f, 0.42f);

        RectTransform cardBody = ForgeCanvasThemeStyler.CreateOrGetParchmentCard(
            rootGo.transform, "MainMenuCard", Vector2.zero, new Vector2(760f, 690f));

        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(
            cardBody, "MainTitleBanner", "CRÓNICAS DE LA FRAGUA",
            new Vector2(0f, 268f), new Vector2(640f, 64f), fontSize: 27f);

        CreateMenuLabel(cardBody, "Subtitle",
            "<i>Extracción Subterránea  •  Herrería Artesanal  •  Cooperativo en Red Local</i>",
            new Vector2(0f, 218f), new Vector2(640f, 28f), 15.5f, ForgeCanvasThemeStyler.SubInk, FontStyles.Italic);

        _soloButton = CreateMenuButton(cardBody, "BtnSolo", "JUGAR EN SOLITARIO", new Vector2(0f, 152f), new Vector2(540f, 58f), true, StartSoloGame);
        CreateSectionDivider(cardBody, "DivLAN", "— COOPERATIVO EN RED LOCAL (LAN) —", new Vector2(0f, 92f));
        _hostButton = CreateMenuButton(cardBody, "BtnHostLAN", "CREAR PARTIDA (HOST)", new Vector2(0f, 40f), new Vector2(540f, 54f), true, StartHostAndLoadGame);

        CreateMenuLabel(cardBody, "HostIpHint",
            $"Tu IP Local detectada para compartir:  <b>{_detectedLocalIp}</b>   (Puerto: <b>{port}</b>)",
            new Vector2(0f, -4f), new Vector2(580f, 26f), 14f, ForgeCanvasThemeStyler.SubInk, FontStyles.Normal);

        _ipInputField = CreateMedievalIpInput(cardBody, new Vector2(-125f, -58f), new Vector2(280f, 50f));

        _clientButton = CreateMenuButton(cardBody, "BtnJoinLAN", "UNIRSE POR IP",
            new Vector2(148f, -58f), new Vector2(244f, 50f), primary: false, OnClientActionClicked);
        _clientBtnLabel = _clientButton.GetComponentInChildren<TMP_Text>(true);

        CreateSectionDivider(cardBody, "DivAudio", "— AJUSTES DEL TALLER —", new Vector2(0f, -118f));
        BuildVolumeControlRow(cardBody, new Vector2(0f, -158f));

        _statusLabel = CreateMenuLabel(cardBody, "StatusBanner", _statusMessage,
            new Vector2(0f, -214f), new Vector2(640f, 42f), 14.5f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Bold);

        CreateMenuButton(cardBody, "BtnQuit", "SALIR DEL JUEGO",
            new Vector2(0f, -276f), new Vector2(260f, 44f), primary: false, QuitGame);

        MedievalTypographyProvider.ApplyToAllTMPInHierarchy(rootGo.transform);
    }

    private void OnClientActionClicked()
    {
        if (_isConnecting) CancelConnection();
        else StartClientAndConnect();
    }

    private void RefreshMainMenuVisuals()
    {
        if (_statusLabel != null) _statusLabel.text = _statusMessage;
        if (_soloButton != null) _soloButton.interactable = !_isConnecting;
        if (_hostButton != null) _hostButton.interactable = !_isConnecting;
        if (_ipInputField != null) _ipInputField.interactable = !_isConnecting;
        if (_clientBtnLabel != null)
            _clientBtnLabel.text = _isConnecting ? "CANCELAR CONEXIÓN" : "UNIRSE POR IP";
    }

    private static TMP_Text CreateMenuLabel(
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
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 11f;
        tmp.fontSizeMax = fontSize;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        return tmp;
    }

    private static void CreateSectionDivider(Transform parent, string name, string title, Vector2 pos) =>
        CreateMenuLabel(parent, name, title, pos, new Vector2(540f, 24f), 13.5f,
            new Color(0.46f, 0.28f, 0.12f, 0.95f), FontStyles.Bold | FontStyles.SmallCaps);

    private static Button CreateMenuButton(
        Transform parent, string name, string label, Vector2 pos, Vector2 size, bool primary, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var btn = go.GetComponent<Button>();

        var txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(go.transform, false);
        var tRt = txtGo.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(10f, 4f); tRt.offsetMax = new Vector2(-10f, -4f);

        var tmp = txtGo.GetComponent<TextMeshProUGUI>();
        MedievalTypographyProvider.ApplyToTMP(tmp);
        tmp.text = label;
        tmp.fontSize = primary ? 18f : 16f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = primary ? 18.5f : 16.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        ForgeCanvasThemeStyler.StyleForgeButton(btn, primary, parent, pos, size);
        tmp.color = primary ? ForgeCanvasThemeStyler.GoldTitleInk : ForgeCanvasThemeStyler.DarkInk;
        btn.onClick.AddListener(onClick);
        return btn;
    }

    private TMP_InputField CreateMedievalIpInput(Transform parent, Vector2 pos, Vector2 size)
    {
        var boxGo = new GameObject("InputHostIP", typeof(RectTransform), typeof(Image));
        boxGo.SetActive(false);
        boxGo.transform.SetParent(parent, false);

        var rt = boxGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        var bg = boxGo.GetComponent<Image>();
        bg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.18f, 0.12f, 0.08f, 0.96f);

        var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        areaGo.transform.SetParent(boxGo.transform, false);
        var aRt = areaGo.GetComponent<RectTransform>();
        aRt.anchorMin = Vector2.zero; aRt.anchorMax = Vector2.one;
        aRt.offsetMin = new Vector2(14f, 6f); aRt.offsetMax = new Vector2(-14f, -6f);

        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        phGo.transform.SetParent(areaGo.transform, false);
        var phRt = phGo.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one; phRt.offsetMin = phRt.offsetMax = Vector2.zero;
        var phTmp = phGo.GetComponent<TextMeshProUGUI>();
        MedievalTypographyProvider.ApplyToTMP(phTmp);
        phTmp.text = "IP del Host (ej. 192.168.1.10)"; phTmp.fontSize = 15f;
        phTmp.fontStyle = FontStyles.Italic; phTmp.color = new Color(0.75f, 0.68f, 0.56f, 0.65f);
        phTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGo.transform.SetParent(areaGo.transform, false);
        var tRt = txtGo.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.offsetMin = tRt.offsetMax = Vector2.zero;
        var txtTmp = txtGo.GetComponent<TextMeshProUGUI>();
        MedievalTypographyProvider.ApplyToTMP(txtTmp);
        txtTmp.fontSize = 17f; txtTmp.fontStyle = FontStyles.Bold;
        txtTmp.color = ForgeCanvasThemeStyler.GoldTitleInk;
        txtTmp.alignment = TextAlignmentOptions.MidlineLeft;

        var input = boxGo.AddComponent<TMP_InputField>();
        input.targetGraphic = bg; input.textViewport = aRt; input.textComponent = txtTmp;
        input.placeholder = phTmp; input.fontAsset = txtTmp.font; input.pointSize = 17f;
        input.characterLimit = 32; input.text = _targetIp;
        input.onValueChanged.AddListener(v => _targetIp = v);
        boxGo.SetActive(true);
        return input;
    }

    private void BuildVolumeControlRow(Transform parent, Vector2 pos)
    {
        float vol = ForgeAudioSystem.GetMasterVolume();
        CreateMenuLabel(parent, "VolTitle", "<b>Volumen Maestro:</b>", pos + new Vector2(-175f, 0f),
            new Vector2(170f, 28f), 15f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Bold);

        var slGo = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider));
        slGo.transform.SetParent(parent, false);
        var sRt = slGo.GetComponent<RectTransform>();
        sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = pos + new Vector2(20f, 0f); sRt.sizeDelta = new Vector2(240f, 30f);

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(slGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(slGo.transform, false);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one; faRt.offsetMin = new Vector2(6f, 0f); faRt.offsetMax = new Vector2(-6f, 0f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillArea.transform, false);
        var fRt = fillGo.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.offsetMin = fRt.offsetMax = Vector2.zero;

        var handleSlide = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlide.transform.SetParent(slGo.transform, false);
        var hsRt = handleSlide.GetComponent<RectTransform>();
        hsRt.anchorMin = Vector2.zero; hsRt.anchorMax = Vector2.one; hsRt.offsetMin = new Vector2(10f, 0f); hsRt.offsetMax = new Vector2(-10f, 0f);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(handleSlide.transform, false);
        var hRt = handleGo.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(28f, 28f);

        var sl = slGo.GetComponent<Slider>();
        sl.fillRect = fRt; sl.handleRect = hRt; sl.targetGraphic = handleGo.GetComponent<Image>();
        sl.minValue = 0f; sl.maxValue = 1f; sl.SetValueWithoutNotify(vol);
        ForgeCanvasThemeStyler.StyleSliderControl(sl, interactive: true);

        _volValueLabel = CreateMenuLabel(parent, "VolPct", $"<b>{Mathf.RoundToInt(vol * 100f)}%</b>",
            pos + new Vector2(175f, 0f), new Vector2(76f, 28f), 15.5f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Bold);

        sl.onValueChanged.AddListener(v =>
        {
            ForgeAudioSystem.SetMasterVolume(v);
            if (_volValueLabel != null) _volValueLabel.text = $"<b>{Mathf.RoundToInt(v * 100f)}%</b>";
        });
    }
}
