using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI;

namespace ForjaGame.Speedrun
{
    /// <summary>
    /// Controlador para proyectar la tabla de Leaderboard y cronómetro en vivo en un segundo monitor
    /// (Pantalla Secundaria / Display 2) mediante la API multi-display nativa de Unity,
    /// permitiendo que el jugador continúe jugando sin interrupciones en la pantalla primaria (Display 1).
    /// </summary>
    public class SecondaryDisplayLeaderboardController : MonoBehaviour
    {
        public static SecondaryDisplayLeaderboardController Instance { get; private set; }

        private GameObject _secondaryCanvasRoot;
        private TextMeshProUGUI _tableText;
        private TextMeshProUGUI _liveStatusText;
        private bool _isDisplay2Active;

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
            if (UnityEngine.Input.GetKeyDown(KeyCode.F10))
            {
                ToggleSecondaryDisplay();
            }

            if (_secondaryCanvasRoot != null && _secondaryCanvasRoot.activeSelf && _liveStatusText != null)
            {
                UpdateLiveDelveHeader();
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
                    Debug.Log("<color=#33FFAA>[Multi-Display]</color> Pantalla secundaria (Display 2) activada con éxito para el Leaderboard.");
                }
                BuildSecondaryCanvas();
            }
        }

        public void ToggleSecondaryDisplay()
        {
            if (Display.displays.Length > 1)
            {
                if (!_isDisplay2Active) Display.displays[1].Activate();
                _isDisplay2Active = true;

                if (_secondaryCanvasRoot == null) BuildSecondaryCanvas();
                else _secondaryCanvasRoot.SetActive(!_secondaryCanvasRoot.activeSelf);
            }
            else
            {
                Debug.LogWarning("[Multi-Display] No se detectó un segundo monitor conectado físicamente.");
            }
        }

        private void BuildSecondaryCanvas()
        {
            if (_secondaryCanvasRoot != null) { Destroy(_secondaryCanvasRoot); }

            _secondaryCanvasRoot = new GameObject("LeaderboardSecondaryDisplayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(_secondaryCanvasRoot);

            var canvas = _secondaryCanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = 1; // Dirigido exclusivamente al Monitor 2

            var scaler = _secondaryCanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Fondo oscuro elegante
            var bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(_secondaryCanvasRoot.transform, false);
            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            bgObj.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f, 1f);

            // Gran pergamino central
            var card = ForgeCanvasThemeStyler.CreateOrGetParchmentCard(_secondaryCanvasRoot.transform, "LeaderboardBoard", Vector2.zero, new Vector2(1600f, 960f));

            ForgeCanvasThemeStyler.CreateOrGetTitleBanner(card, "TitleBanner", "CHRONICLES OF THE FORGE — HALL OF GLORY", new Vector2(0f, 410f), new Vector2(1300f, 70f), 30f);

            // Caja de Estado de Partida en Vivo
            var liveBox = ForgeCanvasThemeStyler.CreateOrGetParchmentCard(card, "LiveBox", new Vector2(0f, 320f), new Vector2(1300f, 70f));
            var liveTextGo = new GameObject("LiveStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            liveTextGo.transform.SetParent(liveBox, false);
            var lRt = liveTextGo.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = new Vector2(20f, 6f); lRt.offsetMax = new Vector2(-20f, -6f);
            _liveStatusText = liveTextGo.GetComponent<TextMeshProUGUI>();
            _liveStatusText.fontSize = 20f;
            _liveStatusText.alignment = TextAlignmentOptions.Center;
            MedievalTypographyProvider.ApplyToTMP(_liveStatusText);

            // Tabla de Récords
            var tableCard = ForgeCanvasThemeStyler.CreateOrGetParchmentCard(card, "TableCard", new Vector2(0f, -50f), new Vector2(1380f, 620f));
            var tableTextGo = new GameObject("TableContent", typeof(RectTransform), typeof(TextMeshProUGUI));
            tableTextGo.transform.SetParent(tableCard, false);
            var tRt = tableTextGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(30f, 30f); tRt.offsetMax = new Vector2(-30f, -30f);
            _tableText = tableTextGo.GetComponent<TextMeshProUGUI>();
            _tableText.fontSize = 17.5f;
            _tableText.alignment = TextAlignmentOptions.TopLeft;
            MedievalTypographyProvider.ApplyToTMP(_tableText);

            RefreshLeaderboardDisplay();
        }

        private void UpdateLiveDelveHeader()
        {
            var timerHud = DelveRunTimerHUD.Instance;
            if (timerHud != null && timerHud.IsTimerRunning)
            {
                string team = string.IsNullOrWhiteSpace(Multiplayer.GameSessionData.TeamName) ? "Iron Vanguard" : Multiplayer.GameSessionData.TeamName;
                float elapsed = timerHud.GetCurrentElapsedSeconds();
                string timeStr = DelveLeaderboardDataSO.FormatSeconds(elapsed);
                _liveStatusText.text = $"<color=#8B0000>● LIVE</color>  •  Party in Cavern: <b>{team}</b>  •  Elapsed Time: <color=#8B0000><b>{timeStr}</b></color>";
            }
            else
            {
                _liveStatusText.text = "<color=#2E5C1E>✓ SYSTEM READY</color>  •  Start an expedition to set a new speedrun record.";
            }
        }

        public void RefreshLeaderboardDisplay()
        {
            if (_tableText == null) return;
            var records = DelveLeaderboardDataSO.Instance.Records;
            if (records == null || records.Count == 0)
            {
                _tableText.text = "<size=22><b>No expeditions recorded yet.</b></size>\n\nConsecrate the Deep Sanctum Altar to immortalize your party in these chronicles.";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<color=#382010><b><pos=20>RANK<pos=150>PARTY<pos=480>BLACKSMITHS<pos=920>RECORD TIME<pos=1120>MODE</b></color>");
            sb.AppendLine("<color=#A08A75>------------------------------------------------------------------------------------------------------------------------</color>");

            int count = Mathf.Min(12, records.Count);
            for (int i = 0; i < count; i++)
            {
                var r = records[i];
                string medal = i switch
                {
                    0 => "<color=#B8860B><b>#1 GOLD</b></color>",
                    1 => "<color=#707070><b>#2 SILVER</b></color>",
                    2 => "<color=#8B5A2B><b>#3 BRONZE</b></color>",
                    _ => $"<b>#{i + 1}</b>"
                };
                string tName = r.teamName.Length <= 20 ? r.teamName : r.teamName.Substring(0, 19) + "..";
                string pNames = r.playerNames.Length <= 30 ? r.playerNames : r.playerNames.Substring(0, 29) + "..";
                string mode = r.isMultiplayer ? $"LAN ({r.playerCount}p)" : "Solo";
                sb.AppendLine($"<pos=20>{medal}<pos=150><b>{tName}</b><pos=480>{pNames}<pos=920><color=#8B0000><b>{r.formattedTime}</b></color><pos=1120>{mode}");
            }

            _tableText.text = sb.ToString();
        }
    }
}
