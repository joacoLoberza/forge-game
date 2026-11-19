using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ForjaGame.UI;
using ForjaGame.Multiplayer;

namespace ForjaGame.Speedrun
{
    /// <summary>
    /// HUD Delve run stopwatch timer, synchronized across network clients, placed on the top-right.
    /// Clean display only (no interactive buttons or keybinds during gameplay).
    /// </summary>
    public class DelveRunTimerHUD : MonoBehaviour
    {
        public static DelveRunTimerHUD Instance { get; private set; }

        private double _networkStartTime;
        private float _localStartTime;
        private bool _isTimerRunning;
        private float _cachedElapsed;
        private bool _hasWon, _isMainMenuScene;
        private float _finalTime, _nextCurtainCheckTime;
        private ForjaGame.UI.ShaderWarmupCurtain _cachedCurtain;
        private GUIStyle _plaqueStyle, _headerStyle, _timerStyle, _subStyle;

        public bool IsTimerRunning => _isTimerRunning;
        public bool HasWon => _hasWon;
        public double NetworkStartTime => _networkStartTime;
        public float FinalTime => _finalTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoEnsureTimerHUD()
        {
            if (Instance != null) return;
            var go = new GameObject("[DELVE_RUN_TIMER_HUD]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DelveRunTimerHUD>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResetTimer();
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _isMainMenuScene = scene.name == "MainMenu";
            _cachedCurtain = null;
            _nextCurtainCheckTime = 0f;
            if (_isMainMenuScene)
            {
                _isTimerRunning = false; _hasWon = false; _finalTime = 0f;
                return;
            }
            if (!_hasWon) ResetTimer();
        }

        public void ResetTimer()
        {
            _localStartTime = Time.time;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                _networkStartTime = NetworkManager.Singleton.ServerTime.Time;
            _cachedElapsed = 0f; _isTimerRunning = false; _hasWon = false; _finalTime = 0f;
        }

        public void StartTimer()
        {
            if (_isTimerRunning || _hasWon) return;
            bool isMulti = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isMulti && !NetworkManager.Singleton.IsServer)
            {
                PlayerNetworkBridge.LocalBridge?.RequestSessionStateFromHost();
                return;
            }

            _localStartTime = Time.time;
            if (isMulti)
                _networkStartTime = NetworkManager.Singleton.ServerTime.Time;
            _cachedElapsed = 0f; _isTimerRunning = true; _hasWon = false; _finalTime = 0f;
            Debug.Log("<color=green>[DelveRunTimerHUD] ¡Cronómetro de speedrun INICIADO por el Host!</color>");

            if (isMulti && NetworkManager.Singleton.IsServer)
                PlayerNetworkBridge.ServerBroadcastTimerState(_networkStartTime, true, false, 0f);
        }

        public void ApplyServerTimerSync(double serverStartTime, bool isRunning, bool hasWon, float finalTime)
        {
            _networkStartTime = serverStartTime;
            _isTimerRunning = isRunning;
            _hasWon = hasWon;
            _finalTime = finalTime;
            if (_hasWon) _cachedElapsed = _finalTime;
            else if (_isTimerRunning && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                _cachedElapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - _networkStartTime));
            else _cachedElapsed = 0f;
        }

        public float GetCurrentElapsedSeconds()
        {
            if (_hasWon) return _finalTime;
            if (!_isTimerRunning) return 0f;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && _networkStartTime > 0.001)
                _cachedElapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - _networkStartTime));
            else
                _cachedElapsed = Mathf.Max(0f, Time.time - _localStartTime);
            return _cachedElapsed;
        }

        public void StopAndRecordRun()
        {
            if (_hasWon) return;
            _finalTime = GetCurrentElapsedSeconds();
            _hasWon = true; _isTimerRunning = false;
            bool isMulti = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (isMulti && NetworkManager.Singleton.IsServer)
                PlayerNetworkBridge.ServerBroadcastTimerState(_networkStartTime, false, true, _finalTime);

            string teamName = GameSessionData.TeamName;
            var playerNamesList = new List<string>();

            if (isMulti)
            {
                var bridges = FindObjectsByType<PlayerNetworkBridge>(FindObjectsSortMode.None);
                foreach (var b in bridges)
                {
                    string p = b.NetworkPlayerName.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(p) && !playerNamesList.Contains(p)) playerNamesList.Add(p);
                }
            }
            if (playerNamesList.Count == 0) playerNamesList.Add(GameSessionData.PlayerName);

            string players = string.Join(", ", playerNamesList);
            DelveLeaderboardDataSO.Instance.RegisterRun(teamName, players, _finalTime, isMulti, playerNamesList.Count);
            LANLeaderboardBroadcaster.Instance?.BroadcastLeaderboard();
        }

        private void Update()
        {
            if (_isMainMenuScene || _hasWon) return;

            // Autoinicio robusto: cuando la cortina de shaders haya terminado (o si no existe), iniciar el cronómetro
            if (!_isTimerRunning && Time.unscaledTime >= _nextCurtainCheckTime)
            {
                _nextCurtainCheckTime = Time.unscaledTime + 0.15f;
                if (_cachedCurtain == null) _cachedCurtain = FindFirstObjectByType<ForjaGame.UI.ShaderWarmupCurtain>();
                if (_cachedCurtain == null || !_cachedCurtain.IsWarming)
                    StartTimer();
            }
        }

        private void OnGUI()
        {
            if (_isMainMenuScene || ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen
                || ForjaGame.Core.GameLoopController.IsEndScreenOpen || VictoryCreditsOverlay.IsBackdropActive) return;
            EnsureStyles();

            float w = 260f, h = 68f;
            float x = Screen.width - w - 18f;
            bool hasLan = NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);
            float y = hasLan ? 122f : 18f;

            GUI.DrawTexture(new Rect(x - 4f, y - 4f, w + 8f, h + 8f), BookTextureBuilder.GetLeatherCoverSprite().texture);
            GUILayout.BeginArea(new Rect(x, y, w, h), _plaqueStyle);

            string title = _hasWon ? "VICTORY RECORD" : "DELVE TIMER";
            GUILayout.Label($"<b>{title}</b>", _headerStyle);
            GUILayout.Label(DelveLeaderboardDataSO.FormatSeconds(_hasWon ? _finalTime : GetCurrentElapsedSeconds()), _timerStyle);

            string team = string.IsNullOrWhiteSpace(GameSessionData.TeamName) ? "Iron Vanguard" : GameSessionData.TeamName;
            GUILayout.Label($"Party: <b>{team}</b>", _subStyle);

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_plaqueStyle != null) return;
            _plaqueStyle = new GUIStyle { normal = { background = BookTextureBuilder.GetParchmentSprite().texture }, border = new RectOffset(14, 14, 14, 14), padding = new RectOffset(8, 8, 5, 5) };
            _headerStyle = new GUIStyle { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = ForgeCanvasThemeStyler.DarkInk } };
            _timerStyle = new GUIStyle { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = new Color(0.65f, 0.12f, 0.08f) } };
            _subStyle = new GUIStyle { fontSize = 10, alignment = TextAnchor.MiddleCenter, richText = true, normal = { textColor = ForgeCanvasThemeStyler.SubInk } };
            MedievalTypographyProvider.ApplyToGUIStyle(_headerStyle, 11, FontStyle.Bold);
            MedievalTypographyProvider.ApplyToGUIStyle(_timerStyle, 19, FontStyle.Bold);
            MedievalTypographyProvider.ApplyToGUIStyle(_subStyle, 10, FontStyle.Normal);
        }
    }
}
