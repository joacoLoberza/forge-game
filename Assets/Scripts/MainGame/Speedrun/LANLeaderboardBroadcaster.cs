using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;

namespace ForjaGame.Speedrun
{
    /// <summary>
    /// Sincronizador UDP en red local (puerto 7788) para compartir tiempos de victoria entre las 3 PCs.
    /// Funciona en solitario o multijugador, descubriendo y fusionando récords automáticamente.
    /// </summary>
    public class LANLeaderboardBroadcaster : MonoBehaviour
    {
        public const ushort LeaderboardPort = 7788;
        private const string PrefixData = "FORGE_SPEEDRUN_DATA:";
        private const string PrefixQuery = "FORGE_SPEEDRUN_QUERY";

        public static LANLeaderboardBroadcaster Instance { get; private set; }

        private UdpClient _udpListener;
        private Thread _listenThread;
        private volatile bool _isRunning;
        private readonly ConcurrentQueue<string> _incomingJsonQueue = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureBroadcaster()
        {
            if (Instance == null)
            {
                var go = new GameObject("[LAN_LEADERBOARD_BROADCASTER]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<LANLeaderboardBroadcaster>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StartListener();
        }

        private void Start()
        {
            BroadcastSyncQuery();
            BroadcastLeaderboard();
        }

        private float _periodicBeaconTimer;

        private void Update()
        {
            while (_incomingJsonQueue.TryDequeue(out string json))
            {
                try
                {
                    var container = JsonUtility.FromJson<SpeedrunContainer>(json);
                    if (container?.records != null && container.records.Count > 0)
                    {
                        int merged = DelveLeaderboardDataSO.Instance.MergeRecords(container.records);
                        if (merged > 0) Debug.Log($"[LeaderboardLAN] Received {merged} new records from LAN peer.");
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[LeaderboardLAN] JSON parse error: {ex.Message}"); }
            }

            _periodicBeaconTimer += Time.unscaledDeltaTime;
            if (_periodicBeaconTimer >= 3.5f)
            {
                _periodicBeaconTimer = 0f;
                BroadcastSyncQuery();
            }
        }

        private void StartListener()
        {
            if (_isRunning) return;
            try
            {
                _udpListener = new UdpClient();
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, LeaderboardPort));
                _udpListener.EnableBroadcast = true;
                _isRunning = true;
                _listenThread = new Thread(ListenLoop) { IsBackground = true };
                _listenThread.Start();
            }
            catch (Exception ex) { Debug.LogWarning($"[LeaderboardLAN] Listener bind error: {ex.Message}"); }
        }

        private void ListenLoop()
        {
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            while (_isRunning && _udpListener != null)
            {
                try
                {
                    byte[] data = _udpListener.Receive(ref sender);
                    if (data == null || data.Length == 0) continue;
                    string msg = Encoding.UTF8.GetString(data);

                    if (msg.StartsWith(PrefixData))
                    {
                        string json = msg.Substring(PrefixData.Length);
                        _incomingJsonQueue.Enqueue(json);
                    }
                    else if (msg.StartsWith(PrefixQuery))
                    {
                        // Responder enviando la tabla local al peer que preguntó
                        SendLeaderboardToEndpoint(sender);
                    }
                }
                catch (SocketException) { break; }
                catch (Exception) { }
            }
        }

        public void BroadcastLeaderboard()
        {
            try
            {
                string json = DelveLeaderboardDataSO.Instance.ExportToJson();
                byte[] bytes = Encoding.UTF8.GetBytes(PrefixData + json);
                using var sender = new UdpClient();
                sender.EnableBroadcast = true;
                sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, LeaderboardPort));
            }
            catch (Exception ex) { Debug.LogWarning($"[LeaderboardLAN] Broadcast error: {ex.Message}"); }
        }

        public void BroadcastSyncQuery()
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(PrefixQuery);
                using var sender = new UdpClient();
                sender.EnableBroadcast = true;
                sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, LeaderboardPort));
            }
            catch (Exception ex) { Debug.LogWarning($"[LeaderboardLAN] Query error: {ex.Message}"); }
        }

        private void SendLeaderboardToEndpoint(IPEndPoint target)
        {
            try
            {
                string json = DelveLeaderboardDataSO.Instance.ExportToJson();
                byte[] bytes = Encoding.UTF8.GetBytes(PrefixData + json);
                using var sender = new UdpClient();
                sender.Send(bytes, bytes.Length, target);
            }
            catch { }
        }

        private void OnDestroy() => StopListener();
        private void OnApplicationQuit() => StopListener();

        private void StopListener()
        {
            _isRunning = false;
            try { _udpListener?.Close(); _udpListener = null; } catch { }
            if (_listenThread != null && _listenThread.IsAlive)
            {
                try { _listenThread.Abort(); } catch { }
                _listenThread = null;
            }
        }
    }
}

