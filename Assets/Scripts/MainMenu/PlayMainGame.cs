using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public enum NetworkModeSelection { None, Solo, Host, Client }

/// <summary>
/// Controlador principal del Menú de Inicio: Solitario, Crear Partida LAN (Host) y Unirse por IP (Cliente).
/// </summary>
public partial class PlayMainGame : MonoBehaviour
{
    [Header("Referencias Opcionales")]
    [SerializeField] private TMP_Text playButtonText;
    [SerializeField] private ushort port = 7777;

    private NetworkModeSelection _selectedMode = NetworkModeSelection.Solo;
    private string _targetIp = "127.0.0.1", _detectedLocalIp = "127.0.0.1";
    private string _statusMessage = "Elige explorar en solitario o abrir las puertas de tu taller en red local (LAN).";
    private bool _isConnecting;
    private float _connectStartedAt;

    public NetworkModeSelection SelectedMode => _selectedMode;
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
        BuildMedievalMainMenuUI(); RefreshMainMenuVisuals();
    }

    private void Update()
    {
        if (_isConnecting && _selectedMode == NetworkModeSelection.Client && Time.unscaledTime - _connectStartedAt > 10.5f)
        {
            CancelConnection();
            _statusMessage = "Tiempo agotado: no se encontró ningún Anfitrión en la IP indicada.";
            RefreshMainMenuVisuals();
        }
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _isConnecting = false;
        _statusMessage = "No se pudo establecer conexión con el Anfitrión. Verifica la IP y el puerto 7777.";
        RefreshMainMenuVisuals();
    }

    private void OnNetworkConnect(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            _statusMessage = "¡Conexión establecida con la Fragua! Entrando al mundo...";
            RefreshMainMenuVisuals();
        }
    }

    public void SelectHostMode()
    {
        _selectedMode = NetworkModeSelection.Host;
        StartHostAndLoadGame();
    }

    public void SelectClientMode()
    {
        _selectedMode = NetworkModeSelection.Client;
        StartClientAndConnect();
    }

    public void PlayGame()
    {
        if (_selectedMode == NetworkModeSelection.Host) StartHostAndLoadGame();
        else if (_selectedMode == NetworkModeSelection.Client) StartClientAndConnect();
        else StartSoloGame();
    }

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

        _statusMessage = "Encendiendo la fragua en solitario...";
        RefreshMainMenuVisuals();
        SceneManager.LoadScene("Furnace", LoadSceneMode.Single);
    }

    public void StartHostAndLoadGame()
    {
        if (_isConnecting) CancelConnection();
        _selectedMode = NetworkModeSelection.Host;
        ForjaGame.Multiplayer.LANNetworkManagerUI.IsLanSessionRequested = true;

        var net = EnsureNetworkManager();
        if (net == null) { _statusMessage = "Error: No se pudo inicializar el gestor de red LAN."; RefreshMainMenuVisuals(); return; }

        ConfigureTransport(net, "127.0.0.1", port);
        _isConnecting = true;
        _connectStartedAt = Time.unscaledTime;
        _statusMessage = $"Abriendo taller LAN en {_detectedLocalIp}:{port}...";
        RefreshMainMenuVisuals();

        if (net.StartHost()) net.SceneManager.LoadScene("Furnace", LoadSceneMode.Single);
        else { _isConnecting = false; _statusMessage = "Error al abrir el puerto 7777 como Anfitrión (¿puerto en uso?)."; RefreshMainMenuVisuals(); }
    }

    public void StartClientAndConnect()
    {
        if (_ipInputField != null && !string.IsNullOrWhiteSpace(_ipInputField.text))
            _targetIp = _ipInputField.text.Trim();

        _selectedMode = NetworkModeSelection.Client;
        ForjaGame.Multiplayer.LANNetworkManagerUI.IsLanSessionRequested = true;

        var net = EnsureNetworkManager();
        if (net == null) { _statusMessage = "Error: No se pudo inicializar el gestor de red LAN."; RefreshMainMenuVisuals(); return; }

        string cleanIp = string.IsNullOrWhiteSpace(_targetIp) ? "127.0.0.1" : _targetIp.Trim();
        ConfigureTransport(net, cleanIp, port);
        net.OnClientDisconnectCallback -= OnNetworkDisconnect;
        net.OnClientDisconnectCallback += OnNetworkDisconnect;
        net.OnClientConnectedCallback -= OnNetworkConnect;
        net.OnClientConnectedCallback += OnNetworkConnect;

        _isConnecting = true;
        _connectStartedAt = Time.unscaledTime;
        _statusMessage = $"Conectando al taller del Anfitrión en {cleanIp}:{port}...";
        RefreshMainMenuVisuals();

        if (!net.StartClient())
        {
            _isConnecting = false;
            _statusMessage = "No fue posible iniciar el cliente de red.";
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
        _statusMessage = "Conexión cancelada.";
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
