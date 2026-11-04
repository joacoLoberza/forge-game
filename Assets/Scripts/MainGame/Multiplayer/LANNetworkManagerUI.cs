using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using ForjaGame.UI;

namespace ForjaGame.Multiplayer
{
    /// <summary>
    /// Gestiona el estado e insignia HUD de sesión cooperativa LAN activa con estética medieval.
    /// </summary>
    public class LANNetworkManagerUI : MonoBehaviour
    {
        public static bool IsLanSessionRequested { get; set; }

        [Header("Configuración de Conexión")]
        [SerializeField] private string targetIp = "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        [SerializeField] private bool autoDetectLocalIp = true;

        private string _detectedLocalIp = "127.0.0.1";
        private GUIStyle _plaqueStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        private void Awake()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.gameObject != gameObject)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var net = NetworkManager.Singleton;
            if (net == null) return;

            if (!net.IsServer && (clientId == net.LocalClientId || clientId == NetworkManager.ServerClientId))
            {
                IsLanSessionRequested = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                net.Shutdown();
                if (SceneManager.GetActiveScene().name != "MainMenu")
                    SceneManager.LoadScene("MainMenu");
            }
        }

        private void Start()
        {
            if (autoDetectLocalIp)
            {
                _detectedLocalIp = GetLocalIPv4();
                targetIp = _detectedLocalIp;
            }
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu")
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            var net = NetworkManager.Singleton;
            if (IsLanSessionRequested && (net == null || (!net.IsClient && !net.IsServer)))
            {
                IsLanSessionRequested = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SceneManager.LoadScene("MainMenu");
            }
        }

        private void OnGUI()
        {
            NetworkManager netManager = NetworkManager.Singleton;
            if (netManager == null || (!netManager.IsClient && !netManager.IsServer)) return;

            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "MainMenu") return;
            if (ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen || ForjaGame.Core.GameLoopController.IsEndScreenOpen) return;

            EnsureStyles();
            DrawActiveSessionHUD(netManager);
        }

        private void DrawActiveSessionHUD(NetworkManager netManager)
        {
            float w = 285f, h = 104f, x = Screen.width - w - 18f, y = 18f;

            GUI.DrawTexture(new Rect(x - 4f, y - 4f, w + 8f, h + 8f), BookTextureBuilder.GetLeatherCoverSprite().texture);
            GUILayout.BeginArea(new Rect(x, y, w, h), _plaqueStyle);

            string role = netManager.IsHost ? "ANFITRIÓN LAN" : (netManager.IsServer ? "SERVIDOR LAN" : "CLIENTE LAN");
            GUILayout.Label($"<b>[{role}]</b>", _headerStyle);

            int playerCount = netManager.ConnectedClientsIds != null ? netManager.ConnectedClientsIds.Count : 1;
            string hostInfo = netManager.IsHost ? $"IP: {_detectedLocalIp}:{port}" : $"Puerto: {port}";
            GUILayout.Label($"Herreros conectados: <b>{playerCount}</b>  •  {hostInfo}", _labelStyle);

            GUILayout.Space(4f);
            if (GUILayout.Button("Desconectar al Menú", _buttonStyle, GUILayout.Height(26f)))
            {
                IsLanSessionRequested = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                netManager.Shutdown();
                SceneManager.LoadScene("MainMenu");
            }

            GUILayout.EndArea();
        }

        private string GetLocalIPv4()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        private void EnsureStyles()
        {
            if (_plaqueStyle != null) return;

            _plaqueStyle = new GUIStyle
            {
                normal = { background = BookTextureBuilder.GetParchmentSprite().texture },
                border = new RectOffset(14, 14, 14, 14),
                padding = new RectOffset(12, 12, 8, 8)
            };

            _headerStyle = new GUIStyle
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true,
                normal = { textColor = ForgeCanvasThemeStyler.DarkInk }
            };

            _labelStyle = new GUIStyle
            {
                fontSize = 11, alignment = TextAnchor.MiddleCenter, richText = true,
                normal = { textColor = ForgeCanvasThemeStyler.SubInk }
            };

            _buttonStyle = new GUIStyle
            {
                normal = { background = BookTextureBuilder.GetTitlePlaqueSprite().texture, textColor = ForgeCanvasThemeStyler.GoldTitleInk },
                border = new RectOffset(12, 12, 10, 10),
                fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };

            MedievalTypographyProvider.ApplyToGUIStyle(_headerStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_labelStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_buttonStyle);
        }
    }
}
