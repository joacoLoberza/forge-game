using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI;
using ForjaGame.Multiplayer;

namespace ForjaGame.Speedrun
{
    /// <summary>
    /// Controlador para proyectar el tablero Hall of Heroes y cronometro en vivo en un segundo monitor
    /// (Pantalla Secundaria / Display 2) mediante la API multi-display nativa de Unity,
    /// permitiendo jugar sin interrupciones en la pantalla primaria (Display 1).
    /// </summary>
    public partial class SecondaryDisplayLeaderboardController : MonoBehaviour
    {
        public static SecondaryDisplayLeaderboardController Instance { get; private set; }

        private struct PodiumCardRefs
        {
            public TextMeshProUGUI teamText;
            public TextMeshProUGUI playersText;
            public TextMeshProUGUI timeText;
            public TextMeshProUGUI metaText;
        }

        private struct LedgerRowRefs
        {
            public TextMeshProUGUI rankText;
            public TextMeshProUGUI teamText;
            public TextMeshProUGUI playersText;
            public TextMeshProUGUI modeText;
            public TextMeshProUGUI dateText;
            public TextMeshProUGUI timeText;
        }

        private GameObject _secondaryCanvasRoot;
        private GameObject _secondaryCameraObj;
        private bool _isDisplay2Active;
        private LeaderboardFilterMode _activeFilter = LeaderboardFilterMode.All;

        // Referencias de UI dinamica (cabecera, telemetria, podio, tabla y pie)
        private TextMeshProUGUI _filterLabelText;
        private Image _statusBadgeImage;
        private TextMeshProUGUI _statusBadgeText;
        private TextMeshProUGUI _livePartyText;
        private TextMeshProUGUI _liveTimerText;
        private TextMeshProUGUI _liveTargetText;
        private TextMeshProUGUI _livePaceText;
        private TextMeshProUGUI _footerTotalsText;
        private TextMeshProUGUI _footerBestSoloText;
        private TextMeshProUGUI _footerBestLanText;

        private readonly PodiumCardRefs[] _podiumCards = new PodiumCardRefs[3];
        private readonly LedgerRowRefs[] _ledgerRows = new LedgerRowRefs[7];
        private float _cachedTopRecordSeconds = -1f;
        private float _nextTelemetryRefreshTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null && Application.isPlaying)
            {
                var go = new GameObject("[SECONDARY_DISPLAY_LEADERBOARD]");
                go.AddComponent<SecondaryDisplayLeaderboardController>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            TryActivateSecondaryDisplay();
            DelveLeaderboardDataSO.Instance.OnLeaderboardChanged += RefreshLeaderboardDisplay;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (DelveLeaderboardDataSO.Instance != null)
                DelveLeaderboardDataSO.Instance.OnLeaderboardChanged -= RefreshLeaderboardDisplay;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f10Key.wasPressedThisFrame) ToggleSecondaryDisplay();
                if (kb.f11Key.wasPressedThisFrame) CycleFilterMode();
            }

            if (_secondaryCanvasRoot != null && _secondaryCanvasRoot.activeSelf && _liveTimerText != null && Time.unscaledTime >= _nextTelemetryRefreshTime)
            {
                bool running = DelveRunTimerHUD.Instance != null && DelveRunTimerHUD.Instance.IsTimerRunning;
                _nextTelemetryRefreshTime = Time.unscaledTime + (running ? 0.10f : 0.45f);
                UpdateLiveTelemetry();
            }
        }

        public void TryActivateSecondaryDisplay()
        {
            if (Display.displays.Length > 1)
            {
                if (!_isDisplay2Active)
                {
                    Display.displays[1].Activate();
                    _isDisplay2Active = true;
                    Debug.Log("<color=#33FFAA>[Multi-Display]</color> Pantalla secundaria (Display 2) activada para el Leaderboard.");
                }
                BuildSecondaryCanvas(targetDisplayIndex: 1);
                return;
            }
#if UNITY_EDITOR
            BuildSecondaryCanvas(targetDisplayIndex: 1);
#endif
        }

        public void ToggleSecondaryDisplay()
        {
            if (Display.displays.Length > 1)
            {
                if (!_isDisplay2Active) { Display.displays[1].Activate(); _isDisplay2Active = true; }
                SetSecondaryVisible(_secondaryCanvasRoot == null || !_secondaryCanvasRoot.activeSelf, 1);
                return;
            }

            int previewDisplay = Application.isEditor ? 1 : 0;
            SetSecondaryVisible(_secondaryCanvasRoot == null || !_secondaryCanvasRoot.activeSelf, previewDisplay);
        }

        public void CycleFilterMode()
        {
            _activeFilter = (LeaderboardFilterMode)(((int)_activeFilter + 1) % 3);
            RefreshLeaderboardDisplay();
        }

        private void SetSecondaryVisible(bool visible, int targetDisplayIndex)
        {
            if (visible && _secondaryCanvasRoot == null) BuildSecondaryCanvas(targetDisplayIndex);
            if (_secondaryCanvasRoot != null) _secondaryCanvasRoot.SetActive(visible);
            if (_secondaryCameraObj != null) _secondaryCameraObj.SetActive(visible);
            if (visible) RefreshLeaderboardDisplay();
            Debug.Log($"<color=#33FFAA>[Multi-Display]</color> Leaderboard Display {targetDisplayIndex + 1}: {(visible ? "VISIBLE" : "OCULTO")} (F10)");
        }

        private void UpdateLiveTelemetry()
        {
            var timerHud = DelveRunTimerHUD.Instance;
            string team = string.IsNullOrWhiteSpace(GameSessionData.TeamName) ? "Iron Vanguard" : GameSessionData.TeamName;
            string player = string.IsNullOrWhiteSpace(GameSessionData.PlayerName) ? "Blacksmith" : GameSessionData.PlayerName;
            string sessionMode = GameSessionData.IsMultiplayerSession ? "LAN CO-OP" : "SOLO RUN";

            _livePartyText.text = MedievalTypographyProvider.SanitizeForTMP(
                $"<color=#FFF3D4><b>{Truncate(team, 22)}</b></color>\n<size=14><color=#CBB592>{Truncate(player, 20)}  |  {sessionMode}</color></size>");

            bool isRunning = timerHud != null && timerHud.IsTimerRunning;
            bool hasWon = timerHud != null && timerHud.HasWon;

            if (isRunning)
            {
                float pulse = 0.80f + 0.20f * Mathf.Sin(Time.unscaledTime * 5f);
                _statusBadgeImage.color = new Color(0.68f * pulse, 0.11f * pulse, 0.09f * pulse, 1f);
                _statusBadgeText.text = "LIVE RUN";

                float elapsed = timerHud.GetCurrentElapsedSeconds();
                _liveTimerText.color = new Color(1f, 0.94f, 0.74f, 1f);
                _liveTimerText.text = DelveLeaderboardDataSO.FormatSeconds(elapsed);
                UpdatePaceDisplay(elapsed, true);
            }
            else if (hasWon)
            {
                _statusBadgeImage.color = new Color(0.72f, 0.52f, 0.12f, 1f);
                _statusBadgeText.text = "VICTORY";

                _liveTimerText.color = new Color(1f, 0.86f, 0.38f, 1f);
                _liveTimerText.text = DelveLeaderboardDataSO.FormatSeconds(timerHud.FinalTime);
                UpdatePaceDisplay(timerHud.FinalTime, false);
            }
            else
            {
                _statusBadgeImage.color = new Color(0.18f, 0.38f, 0.20f, 1f);
                _statusBadgeText.text = "STANDBY";

                _liveTimerText.color = new Color(0.74f, 0.67f, 0.56f, 1f);
                _liveTimerText.text = "00:00.0";
                UpdatePaceDisplay(-1f, false);
            }
        }

        private void UpdatePaceDisplay(float currentSeconds, bool isLive)
        {
            if (_cachedTopRecordSeconds <= 0f)
            {
                _liveTargetText.text = "TARGET RECORD #1:  <color=#CBB592>UNCLAIMED</color>";
                _livePaceText.text = isLive
                    ? "<color=#7CE88C><b>ON PACE FOR FIRST WORLD RECORD!</b></color>"
                    : "<color=#BBA688>Start an expedition to set the first time</color>";
                return;
            }

            _liveTargetText.text = $"RECORD #1 TO BEAT:  <color=#FFD97D><b>{DelveLeaderboardDataSO.FormatSeconds(_cachedTopRecordSeconds)}</b></color>";
            if (currentSeconds < 0f)
            {
                _livePaceText.text = "<color=#BBA688>Enter the delve to race the #1 champion</color>";
                return;
            }

            float delta = currentSeconds - _cachedTopRecordSeconds;
            string diffStr = DelveLeaderboardDataSO.FormatSeconds(Mathf.Abs(delta));
            _livePaceText.text = delta <= 0f
                ? $"<color=#7CE88C><b>-{diffStr}  AHEAD OF RECORD</b></color>"
                : $"<color=#FF887A><b>+{diffStr}  BEHIND RECORD</b></color>";
        }
    }
}
