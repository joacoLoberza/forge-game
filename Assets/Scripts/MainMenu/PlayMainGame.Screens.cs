using UnityEngine;
using TMPro;
using ForjaGame.UI;
using ForjaGame.Multiplayer;

public partial class PlayMainGame
{
    private void BuildModeSelectionSpread()
    {
        _screenModeLeft = CreateSpreadLayer(_bookLeftPage, "ModeLeft");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenModeLeft.transform, "ModeLeftBanner", "PATHS TO THE DEEP",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17.5f);
        CreateMenuLabel(_screenModeLeft.transform, "ModeGuide",
            "<b>Singleplayer</b>\nEnter the depths alone, craft every weapon yourself, and climb the solo speedrun ledger.\n\n" +
            "<b>Multiplayer (LAN)</b>\nConnect via local IP (up to 4 smiths), share the workshop stations, and battle cave creatures side by side.",
            new Vector2(0f, 6f), new Vector2(344f, 240f), 13.5f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Normal).alignment = TextAlignmentOptions.TopLeft;
        CreateMenuButton(_screenModeLeft.transform, "BtnBackTitle", "« BACK TO TITLE",
            new Vector2(0f, -166f), new Vector2(220f, 40f), false, ReturnToTitleScreen);

        _screenModeRight = CreateSpreadLayer(_bookRightPage, "ModeRight");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenModeRight.transform, "ModeTitleBanner", "SELECT GAME MODE",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17.5f);
        CreateMenuButton(_screenModeRight.transform, "BtnSingleplayer",
            "<b>SINGLEPLAYER</b>\n<size=11.5>Master the anvil alone • Slay the Titan</size>",
            new Vector2(0f, 70f), new Vector2(336f, 76f), true, OpenSingleplayerSetup);
        CreateSectionDivider(_screenModeRight.transform, "DivMulti", "OR CO-OP", new Vector2(0f, 6f));
        CreateMenuButton(_screenModeRight.transform, "BtnMultiplayer",
            "<b>MULTIPLAYER (LAN)</b>\n<size=11.5>Host or join a team session by IP</size>",
            new Vector2(0f, -60f), new Vector2(336f, 76f), true, OpenMultiplayerSetup);
    }

    private void BuildSingleplayerSetupSpread()
    {
        _screenSingleLeft = CreateSpreadLayer(_bookLeftPage, "SingleLeft");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenSingleLeft.transform, "SingleLeftHdr", "APPRENTICE LEDGER",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);
        CreateMenuLabel(_screenSingleLeft.transform, "SingleLore",
            "Every forged blade carries the mark of its creator.\n\nEnter your blacksmith name on the right page so it appears in your workshop codex, combat health bar, and speedrun hall of heroes.",
            new Vector2(0f, 12f), new Vector2(340f, 210f), 13.5f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Normal).alignment = TextAlignmentOptions.TopLeft;
        CreateMenuButton(_screenSingleLeft.transform, "BtnBackMode", "« BACK",
            new Vector2(0f, -166f), new Vector2(200f, 40f), false, OpenModeSelection);

        _screenSingleRight = CreateSpreadLayer(_bookRightPage, "SingleRight");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenSingleRight.transform, "SingleTitleBanner", "SINGLEPLAYER PROFILE",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);
        CreateMenuLabel(_screenSingleRight.transform, "LabelPlayerName", "<b>CHARACTER NAME:</b>",
            new Vector2(0f, 78f), new Vector2(320f, 26f), 14f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Bold);
        _singlePlayerNameInput = CreateMedievalInputField(
            _screenSingleRight.transform, "InputSingleName", GameSessionData.PlayerName, "Enter your name...",
            new Vector2(0f, 32f), new Vector2(316f, 46f), 20);
        CreateMenuButton(_screenSingleRight.transform, "BtnStartSingle", "START JOURNEY",
            new Vector2(0f, -56f), new Vector2(316f, 52f), true, () =>
            {
                string pName = _singlePlayerNameInput != null ? _singlePlayerNameInput.text : "";
                GameSessionData.SaveSession(pName, "Solo Blacksmith", false);
                StartSoloGame();
            });
    }

    private void BuildMultiplayerSetupSpread()
    {
        _screenMultiLeft = CreateSpreadLayer(_bookLeftPage, "MultiLeft");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenMultiLeft.transform, "MultiLeftBanner", "LAN EXPEDITION GUIDE",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);
        CreateMenuLabel(_screenMultiLeft.transform, "MultiGuideText",
            $"<b>1. Host:</b> Share your local IP with teammates:\n<color=#6D3F12><b>{_detectedLocalIp}</b></color> (Port <b>{port}</b>)\n\n" +
            "<b>2. Client:</b> Enter the Host IP address on the facing page and click Join.\n\n" +
            "<i>All teammates share the workshop and delve progression simultaneously.</i>",
            new Vector2(0f, 10f), new Vector2(344f, 230f), 13.5f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Normal).alignment = TextAlignmentOptions.TopLeft;
        _statusLabel = CreateMenuLabel(_screenMultiLeft.transform, "StatusBanner", _statusMessage,
            new Vector2(0f, -110f), new Vector2(344f, 44f), 12.5f, ForgeCanvasThemeStyler.SubInk, FontStyles.Bold);
        CreateMenuButton(_screenMultiLeft.transform, "BtnBackMulti", "« BACK",
            new Vector2(0f, -166f), new Vector2(200f, 40f), false, OpenModeSelection);

        _screenMultiRight = CreateSpreadLayer(_bookRightPage, "MultiRight");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenMultiRight.transform, "MultiTitleBanner", "MULTIPLAYER LAN",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);
        CreateMenuLabel(_screenMultiRight.transform, "LblPName", "<b>WARRIOR NAME:</b>",
            new Vector2(0f, 120f), new Vector2(320f, 22f), 13f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Bold);
        _multiPlayerNameInput = CreateMedievalInputField(
            _screenMultiRight.transform, "InputMultiPlayerName", GameSessionData.PlayerName, "Warrior name...",
            new Vector2(0f, 84f), new Vector2(320f, 42f), 20);
        CreateMenuButton(_screenMultiRight.transform, "BtnHostLAN", "CREATE LAN SERVER (HOST)",
            new Vector2(0f, 20f), new Vector2(320f, 46f), true, () =>
            {
                GameSessionData.PlayerName = _multiPlayerNameInput != null ? _multiPlayerNameInput.text : "";
                GameSessionData.IsMultiplayerSession = true;
                StartHostLobby();
            });
        CreateSectionDivider(_screenMultiRight.transform, "DivJoin", "OR JOIN", new Vector2(0f, -26f));
        _ipInputField = CreateMedievalInputField(_screenMultiRight.transform, "InputHostIP", _targetIp, "Host IP (192.168.1.X)...",
            new Vector2(0f, -68f), new Vector2(320f, 42f), 32);
        if (_ipInputField != null) _ipInputField.onValueChanged.AddListener(v => _targetIp = v);
        var clientBtn = CreateMenuButton(_screenMultiRight.transform, "BtnJoinLAN", "JOIN BY IP",
            new Vector2(0f, -126f), new Vector2(260f, 44f), false, () =>
            {
                GameSessionData.PlayerName = _multiPlayerNameInput != null ? _multiPlayerNameInput.text : "";
                GameSessionData.IsMultiplayerSession = true;
                OnClientActionClicked();
            });
        _clientBtnLabel = clientBtn.GetComponentInChildren<TMP_Text>(true);
    }

    public void SwitchScreen(MainMenuScreen screen)
    {
        MainMenuScreen prev = _currentScreen;
        _currentScreen = screen;

        _tabChronicle?.SetIndicatorActive(screen == MainMenuScreen.Title);
        _tabModes?.SetIndicatorActive(screen == MainMenuScreen.ModeSelection || screen == MainMenuScreen.SingleplayerSetup || screen == MainMenuScreen.MultiplayerSetup || screen == MainMenuScreen.LobbyRoom);
        _tabHeroes?.SetIndicatorActive(screen == MainMenuScreen.Leaderboard);

        if (_menuBookInitialized && screen != prev && _menuPageFlipper != null && gameObject.activeInHierarchy)
        {
            int dir = (int)screen > (int)prev ? 1 : -1;
            _menuPageFlipper.PlayPageTurn(dir, () => ApplyScreenSpread(screen));
            return;
        }
        ApplyScreenSpread(screen);
    }

    private void ApplyScreenSpread(MainMenuScreen screen)
    {
        SetSpread(_screenTitleLeft, _screenTitleRight, screen == MainMenuScreen.Title);
        SetSpread(_screenModeLeft, _screenModeRight, screen == MainMenuScreen.ModeSelection);
        SetSpread(_screenSingleLeft, _screenSingleRight, screen == MainMenuScreen.SingleplayerSetup);
        SetSpread(_screenMultiLeft, _screenMultiRight, screen == MainMenuScreen.MultiplayerSetup);
        SetSpread(_screenLobbyLeft, _screenLobbyRight, screen == MainMenuScreen.LobbyRoom);
        SetSpread(_screenLdLeft, _screenLdRight, screen == MainMenuScreen.Leaderboard);
        RefreshMainMenuVisuals();
    }

    private static void SetSpread(GameObject l, GameObject r, bool active)
    {
        if (l != null) l.SetActive(active);
        if (r != null) r.SetActive(active);
    }

    private void OnClientActionClicked()
    {
        if (_isConnecting) CancelConnection();
        else StartClientAndConnect();
    }

    private void RefreshMainMenuVisuals()
    {
        if (_statusLabel != null) _statusLabel.text = _statusMessage;
        if (_clientBtnLabel != null)
            _clientBtnLabel.text = _isConnecting ? "CANCEL" : "JOIN BY IP";
    }
}
