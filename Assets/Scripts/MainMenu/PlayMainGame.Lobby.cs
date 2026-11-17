using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ForjaGame.UI;
using ForjaGame.Multiplayer;

public partial class PlayMainGame
{
    private GameObject _screenLobbyLeft, _screenLobbyRight;
    private TMP_InputField _lobbyTeamInput;
    private TMP_Text _lobbyTeamClientDisplay, _lobbyRosterText, _lobbyHeaderInfoText;
    private Button _lobbyStartBtn;
    private float _lastLobbyRefreshTime;

    private void BuildLobbyScreenSpread()
    {
        _screenLobbyLeft = CreateSpreadLayer(_bookLeftPage, "LobbyLeft");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenLobbyLeft.transform, "LobbyRosterHdr", "ASSEMBLED WARRIORS",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);

        CreateMenuLabel(_screenLobbyLeft.transform, "LblLobbyTeam", "<b>EXPEDITION TEAM NAME:</b>",
            new Vector2(0f, 122f), new Vector2(330f, 22f), 13f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Bold);
        _lobbyTeamInput = CreateMedievalInputField(
            _screenLobbyLeft.transform, "InputLobbyTeam", GameSessionData.TeamName, "Enter Team name...",
            new Vector2(0f, 84f), new Vector2(320f, 42f), 24);
        if (_lobbyTeamInput != null)
        {
            _lobbyTeamInput.onValueChanged.AddListener(v =>
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    PlayerNetworkBridge.ServerBroadcastTeamName(v);
                else GameSessionData.TeamName = v;
            });
        }
        _lobbyTeamClientDisplay = CreateMenuLabel(_screenLobbyLeft.transform, "DisplayLobbyTeamClient", "",
            new Vector2(0f, 84f), new Vector2(330f, 40f), 16f, new Color(0.65f, 0.12f, 0.08f), FontStyles.Bold);

        RectTransform rosterBox = ForgeCanvasThemeStyler.CreateOrGetParchmentCard(
            _screenLobbyLeft.transform, "RosterInsetBox", new Vector2(0f, -58f), new Vector2(348f, 200f));
        _lobbyRosterText = CreateMenuLabel(rosterBox, "RosterListText", "Connecting...",
            Vector2.zero, new Vector2(320f, 176f), 13.5f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Normal);
        _lobbyRosterText.alignment = TextAlignmentOptions.TopLeft;

        _screenLobbyRight = CreateSpreadLayer(_bookRightPage, "LobbyRight");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenLobbyRight.transform, "LobbyTitleBanner", "WAR EXPEDITION LOBBY",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);
        _lobbyHeaderInfoText = CreateMenuLabel(_screenLobbyRight.transform, "LobbySubtitle",
            "<i>Gather your blacksmith brothers before plunging into the depths</i>",
            new Vector2(0f, 112f), new Vector2(340f, 44f), 13.5f, ForgeCanvasThemeStyler.SubInk, FontStyles.Italic);
        var startBtnObj = CreateMenuButton(_screenLobbyRight.transform, "BtnStartExpedition", "START EXPEDITION (PLAY)",
            new Vector2(0f, 16f), new Vector2(324f, 52f), true, HostStartGameFromLobby);
        _lobbyStartBtn = startBtnObj.GetComponent<Button>();
        CreateMenuButton(_screenLobbyRight.transform, "BtnLeaveLobby", "LEAVE LOBBY",
            new Vector2(0f, -150f), new Vector2(240f, 42f), false, LeaveLobbyAndDisconnect);
    }

    public void OpenLobbyRoom()
    {
        SwitchScreen(MainMenuScreen.LobbyRoom);
        RefreshLobbyUI();
    }

    public void HostStartGameFromLobby()
    {
        var net = NetworkManager.Singleton;
        if (net == null || !net.IsServer) return;
        string finalTeam = _lobbyTeamInput != null ? _lobbyTeamInput.text : GameSessionData.TeamName;
        PlayerNetworkBridge.ServerBroadcastTeamName(finalTeam);
        _statusMessage = "Starting expedition for all connected warriors...";
        RefreshMainMenuVisuals();
        net.SceneManager.LoadScene("Furnace", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void LeaveLobbyAndDisconnect()
    {
        CancelConnection();
        OpenModeSelection();
    }

    public void UpdateLobbyTick()
    {
        if (_currentScreen != MainMenuScreen.LobbyRoom) return;
        if (Time.unscaledTime - _lastLobbyRefreshTime > 0.5f)
        {
            _lastLobbyRefreshTime = Time.unscaledTime;
            RefreshLobbyUI();
        }
    }

    private void RefreshLobbyUI()
    {
        var net = NetworkManager.Singleton;
        bool isHost = net != null && net.IsServer;
        bool isConnected = net != null && (net.IsServer || net.IsClient);

        if (_lobbyHeaderInfoText != null)
        {
            string hostIp = isHost ? _detectedLocalIp : _targetIp;
            string role = isHost ? "Party Leader (Host)" : "Warrior (Client)";
            _lobbyHeaderInfoText.text = $"LAN Server: <b>{hostIp}:{port}</b>\nRole: <b>{role}</b>";
        }

        if (_lobbyTeamInput != null && _lobbyTeamClientDisplay != null)
        {
            _lobbyTeamInput.gameObject.SetActive(isHost);
            _lobbyTeamClientDisplay.gameObject.SetActive(!isHost);
            if (!isHost)
            {
                string team = string.IsNullOrWhiteSpace(GameSessionData.TeamName) ? "Iron Vanguard" : GameSessionData.TeamName;
                _lobbyTeamClientDisplay.text = $"<b><color=#8B5A00>[{team}]</color></b> <size=11>(Set by Host)</size>";
            }
        }

        if (_lobbyStartBtn != null)
        {
            _lobbyStartBtn.gameObject.SetActive(isHost);
            _lobbyStartBtn.interactable = isConnected;
        }
        if (_lobbyRosterText != null) _lobbyRosterText.text = BuildConnectedRosterString(net, isHost);
    }

    private string BuildConnectedRosterString(NetworkManager net, bool isHost)
    {
        var lines = new List<string>();
        var addedClients = new HashSet<ulong>();
        var bridges = FindObjectsByType<PlayerNetworkBridge>(FindObjectsSortMode.None);

        foreach (var b in bridges)
        {
            if (addedClients.Contains(b.OwnerClientId)) continue;
            addedClients.Add(b.OwnerClientId);
            string pName = b.NetworkPlayerName.Value.ToString();
            if (string.IsNullOrWhiteSpace(pName)) pName = b.IsOwner ? GameSessionData.PlayerName : "Warrior";
            string badge = b.OwnerClientId == NetworkManager.ServerClientId ? "<color=#8B5A00><b>[LEADER]</b></color>" : "<color=#1B6634><b>+ [READY]</b></color>";
            lines.Add($"  {badge}  <color=#1A0F08><b>{pName}</b></color>");
        }
        if (lines.Count == 0)
        {
            string selfBadge = isHost ? "<color=#8B5A00><b>[LEADER]</b></color>" : "<color=#1B6634><b>+ [READY]</b></color>";
            lines.Add($"  {selfBadge}  <color=#1A0F08><b>{GameSessionData.PlayerName}</b></color>");
        }
        return $"<color=#2A1808><b>WARRIORS ({lines.Count}/4):</b></color>\n\n" + string.Join("\n", lines);
    }
}
