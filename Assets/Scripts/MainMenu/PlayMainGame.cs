using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using ForjaGame.UI;

public enum NetworkModeSelection { None, Solo, Host, Client }
public enum MainMenuScreen { Title, ModeSelection, SingleplayerSetup, MultiplayerSetup, LobbyRoom, Leaderboard }

/// <summary>
/// Controlador principal del Menú de Inicio: Solitario, Sala de Espera LAN (Host) y Unirse por IP (Cliente).
/// </summary>
public partial class PlayMainGame : MonoBehaviour
{
    [Header("Referencias Opcionales")]
    [SerializeField] private TMP_Text playButtonText;
    [SerializeField] private ushort port = 7777;

    private NetworkModeSelection _selectedMode = NetworkModeSelection.Solo;
    private MainMenuScreen _currentScreen = MainMenuScreen.Title;
    private string _targetIp = "127.0.0.1", _detectedLocalIp = "127.0.0.1";
    private string _statusMessage = "Select singleplayer or gather your team for LAN multiplayer.";
    private bool _isConnecting;
    private float _connectStartedAt;

    public NetworkModeSelection SelectedMode => _selectedMode;
    public MainMenuScreen CurrentScreen => _currentScreen;
    public string TargetIp { get => _targetIp; set => _targetIp = value; }
    public ushort Port => port;
    public string StatusMessage => _statusMessage;
    public string DetectedLocalIp => _detectedLocalIp;
    public bool IsConnecting => _isConnecting;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        _detectedLocalIp = GetLocalIPv4(); _targetIp = _detectedLocalIp;
        if (playButtonText == null) playButtonText = GetComponentInChildren<TMP_Text>(true);
        MainMenuBackdropBuilder.EnsureBackdrop();
        ForjaGame.Audio.ForgeAudioSystem.EnsureInstance();
        BuildMedievalMainMenuUI(); RefreshMainMenuVisuals();
    }

    private void Update()
    {
        if (_isConnecting && _selectedMode == NetworkModeSelection.Client && Time.unscaledTime - _connectStartedAt > 10.5f)
        {
            CancelConnection();
            _statusMessage = "Connection timed out: no Host found at that IP address.";
            RefreshMainMenuVisuals();
        }
        UpdateLobbyTick();
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkConnect;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkConnect;
        }
    }

    private void OnNetworkDisconnect(ulong clientId)
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        _isConnecting = false;
        _statusMessage = "Disconnected from Host. Check the IP address and port 7777.";
        RefreshMainMenuVisuals();
        if (_currentScreen == MainMenuScreen.LobbyRoom) SwitchScreen(MainMenuScreen.MultiplayerSetup);
    }

    private void OnNetworkConnect(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            _isConnecting = false;
            _statusMessage = "Connected to the expedition! Waiting to start...";
            RefreshMainMenuVisuals();
            OpenLobbyRoom();
        }
    }

    public void PlayGame() => OpenModeSelection();
    public void OpenModeSelection() => SwitchScreen(MainMenuScreen.ModeSelection);
    public void OpenSingleplayerSetup() => SwitchScreen(MainMenuScreen.SingleplayerSetup);
    public void OpenMultiplayerSetup() => SwitchScreen(MainMenuScreen.MultiplayerSetup);
    public void ReturnToTitleScreen() => SwitchScreen(MainMenuScreen.Title);

    public void StartSoloGame()
    {
        if (_isConnecting) CancelConnection();
        _selectedMode = NetworkModeSelection.Solo;
        ForjaGame.Multiplayer.LANNetworkManagerUI.IsLanSessionRequested = false;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        _statusMessage = "Starting singleplayer journey...";
        RefreshMainMenuVisuals();
        SceneManager.LoadScene("Furnace", LoadSceneMode.Single);
    }

    public void StartHostLobby()
    {
        if (_isConnecting) CancelConnection();
        _selectedMode = NetworkModeSelection.Host;
        ForjaGame.Multiplayer.LANNetworkManagerUI.IsLanSessionRequested = true;

        var net = EnsureNetworkManager();
        if (net == null) { _statusMessage = "Error: Could not initialize LAN network manager."; RefreshMainMenuVisuals(); return; }

        ConfigureTransport(net, "0.0.0.0", port);
        _isConnecting = false;
        _statusMessage = $"Opening waiting lobby at {_detectedLocalIp}:{port}...";
        RefreshMainMenuVisuals();

        if (net.StartHost()) OpenLobbyRoom();
        else { _statusMessage = "Error opening port 7777 as Host (port already in use?)."; RefreshMainMenuVisuals(); }
    }

    public void StartClientAndConnect()
    {
        if (_ipInputField != null && !string.IsNullOrWhiteSpace(_ipInputField.text))
            _targetIp = _ipInputField.text.Trim();

        _selectedMode = NetworkModeSelection.Client;
        ForjaGame.Multiplayer.LANNetworkManagerUI.IsLanSessionRequested = true;

        var net = EnsureNetworkManager();
        if (net == null) { _statusMessage = "Error: Could not initialize LAN network manager."; RefreshMainMenuVisuals(); return; }

        string cleanIp = string.IsNullOrWhiteSpace(_targetIp) ? "127.0.0.1" : _targetIp.Trim();
        ConfigureTransport(net, cleanIp, port);
        net.OnClientDisconnectCallback -= OnNetworkDisconnect;
        net.OnClientDisconnectCallback += OnNetworkDisconnect;
        net.OnClientConnectedCallback -= OnNetworkConnect;
        net.OnClientConnectedCallback += OnNetworkConnect;

        _isConnecting = true;
        _connectStartedAt = Time.unscaledTime;
        _statusMessage = $"Connecting to Host at {cleanIp}:{port}...";
        RefreshMainMenuVisuals();

        if (!net.StartClient())
        {
            _isConnecting = false;
            _statusMessage = "Could not start network client.";
            RefreshMainMenuVisuals();
        }
    }

    public void CancelConnection()
    {
        ForjaGame.Multiplayer.LANNetworkManagerUI.IsLanSessionRequested = false;
        var net = NetworkManager.Singleton;
        if (net != null)
        {
            net.OnClientDisconnectCallback -= OnNetworkDisconnect;
            net.OnClientConnectedCallback -= OnNetworkConnect;
            net.Shutdown();
        }
        _isConnecting = false;
        _statusMessage = "Connection cancelled.";
        RefreshMainMenuVisuals();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private NetworkManager EnsureNetworkManager()
    {
        var net = NetworkManager.Singleton ?? FindFirstObjectByType<NetworkManager>();
        if (net != null) { DontDestroyOnLoad(net.gameObject); return net; }

        var prefab = Resources.Load<GameObject>("NetworkManager_LAN");
        if (prefab != null)
        {
            var inst = Instantiate(prefab); inst.name = "NetworkManager_LAN";
            DontDestroyOnLoad(inst); return inst.GetComponent<NetworkManager>();
        }

        GameObject netGo = new GameObject("NetworkManager_LAN");
        var nm = netGo.AddComponent<NetworkManager>();
        nm.NetworkConfig ??= new NetworkConfig();
        var transport = netGo.AddComponent<UnityTransport>();
        nm.NetworkConfig.NetworkTransport = transport;
        ConfigureTransport(nm, "127.0.0.1", port);

        var playerPrefab = Resources.Load<GameObject>("NetworkPlayer");
        if (playerPrefab != null) nm.NetworkConfig.PlayerPrefab = playerPrefab;
        var prefabsList = Resources.Load<NetworkPrefabsList>("DefaultNetworkPrefabs");
        if (prefabsList != null) { nm.NetworkConfig.Prefabs ??= new NetworkPrefabs(); nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList); }

        nm.NetworkConfig.EnableSceneManagement = true;
        nm.NetworkConfig.AutoSpawnPlayerPrefabClientSide = true;
        nm.RunInBackground = true;
        netGo.AddComponent<ForjaGame.Multiplayer.LANNetworkManagerUI>();
        DontDestroyOnLoad(netGo);
        return nm;
    }

    private static void ConfigureTransport(NetworkManager nm, string address, ushort targetPort)
    {
        var t = nm.GetComponent<UnityTransport>();
        if (t == null) return;
        t.ConnectionData.Address = address; t.ConnectionData.Port = targetPort; t.ConnectionData.ServerListenAddress = "0.0.0.0";
    }

    private static string GetLocalIPv4()
    {
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
}
