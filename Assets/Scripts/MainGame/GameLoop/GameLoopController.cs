using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ForjaGame.Forge;
using ForjaGame.Audio;

namespace ForjaGame.Core
{
    /// <summary>
    /// Controla el ciclo de incursión a la caverna: Consagración en el Altar de las Profundidades (Victoria con recompensa)
    /// y Caída en Combate (Derrota con pérdida del botín recolectado en la incursión y regreso al taller).
    /// </summary>
    public class GameLoopController : MonoBehaviour
    {
        public static GameLoopController Instance { get; private set; }
        public static bool IsEndScreenOpen { get; private set; }
        public static string LastOutcomeSummary { get; private set; } = string.Empty;

        [Header("Eventos de Interfaz")]
        public UnityEvent onVictory;
        public UnityEvent onDefeat;

        [Header("Paneles de Fin de Partida")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;

        [Header("Reaparición (Punto Inicial del Mapa)")]
        public Transform playerTransform;
        public Transform respawnPoint;

        public GameObject VictoryPanel => victoryPanel;
        public GameObject DefeatPanel => defeatPanel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            IsEndScreenOpen = false;
            LastOutcomeSummary = string.Empty;
        }

        private void Awake() { Instance = this; IsEndScreenOpen = false; ResolveEndPanelsAndButtons(); }

        private void Start()
        {
            Time.timeScale = 1f;
            IsEndScreenOpen = false;
            ResolveEndPanelsAndButtons();
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
            ForjaGame.UI.ForgeCanvasThemeStyler.StyleEndScreensAndMainMenu();
            ForjaGame.Environment.GreenPlateauTerrainController.EnsurePlateauInvisibleBoundaries();
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; IsEndScreenOpen = false; Time.timeScale = 1f; ForjaGame.UI.VictoryCreditsOverlay.HideAll(); }
        }

        private void ResolveEndPanelsAndButtons()
        {
            if (victoryPanel != null && defeatPanel != null) return;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var tr in c.GetComponentsInChildren<Transform>(true))
                {
                    if (victoryPanel == null && (tr.name == "PanelVictory" || tr.name == "VictoryPanel")) victoryPanel = tr.gameObject;
                    else if (defeatPanel == null && (tr.name == "PanelDefeat" || tr.name == "DefeatPanel")) defeatPanel = tr.gameObject;
                }
            }
        }

        public void TriggerVictory() => Multiplayer.PlayerNetworkBridge.BroadcastPartyVictoryFromLocal();

        public void ReceiveNetworkVictory()
        {
            if (IsEndScreenOpen && GameStateController.State == GameState.Victory) return;
            if (defeatPanel != null) defeatPanel.SetActive(false);
            foreach (var p in Combat.PlayerHealthController.AllPlayers) if (p != null) p.RestoreHealthFull();

            ForjaGame.Speedrun.DelveRunTimerHUD.Instance?.StopAndRecordRun();
            ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzVictoriaVerde();
            ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzVictoriaAltar();
            ReleaseAllOccupiedStations();
            ResolveEndPanelsAndButtons();

            var inv = Inventory.InventoryController.Instance;
            if (inv != null && string.IsNullOrEmpty(LastOutcomeSummary)) LastOutcomeSummary = inv.ClaimDeepAltarReward();
            ForgeAudioSystem.PlayAltarConsecrate();

            IsEndScreenOpen = true;
            GameStateController.ChangeState(GameState.Victory);
            ForjaGame.UI.ForgeCanvasThemeStyler.StyleEndScreensAndMainMenu();
            if (victoryPanel != null) victoryPanel.SetActive(false);

            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; Time.timeScale = 0f;
            ForjaGame.UI.VictoryCreditsOverlay.BeginCredits(ShowVictoryOptionsAfterCredits);
        }

        private void ShowVictoryOptionsAfterCredits()
        {
            if (GameStateController.State != GameState.Victory || !IsEndScreenOpen) return;
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                if (!victoryPanel.TryGetComponent<Canvas>(out var subCanvas) || subCanvas == null)
                    subCanvas = victoryPanel.AddComponent<Canvas>();
                subCanvas.overrideSorting = true; subCanvas.sortingOrder = 960;
                if (!victoryPanel.TryGetComponent<GraphicRaycaster>(out var ray) || ray == null)
                    victoryPanel.AddComponent<GraphicRaycaster>();
                Canvas.ForceUpdateCanvases();
            }
            onVictory?.Invoke();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void TriggerDefeat()
        {
            if (IsEndScreenOpen) return;
            ReleaseAllOccupiedStations();
            ResolveEndPanelsAndButtons();

            var inv = Inventory.InventoryController.Instance;
            LastOutcomeSummary = inv != null ? inv.ForfeitCurrentDelveLoot() : string.Empty;

            IsEndScreenOpen = true;
            GameStateController.ChangeState(GameState.Defeat);
            ForjaGame.UI.ForgeCanvasThemeStyler.StyleEndScreensAndMainMenu();
            if (defeatPanel != null) defeatPanel.SetActive(true);
            onDefeat?.Invoke();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }

        public void AcceptVictory() => CompleteEndScreenAndTeleportToInitialSpawn();
        public void AcceptDefeat() => CompleteEndScreenAndTeleportToInitialSpawn();

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f; IsEndScreenOpen = false;
            ForjaGame.UI.VictoryCreditsOverlay.HideAll();
            ForjaGame.Speedrun.LANLeaderboardBroadcaster.Instance?.BroadcastLeaderboard();
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void CompleteEndScreenAndTeleportToInitialSpawn()
        {
            Time.timeScale = 1f;
            IsEndScreenOpen = false;
            ForjaGame.UI.VictoryCreditsOverlay.HideAll();

            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);

            GameStateController.ChangeState(GameState.Exploration);
            ReleaseAllOccupiedStations();
            ForjaGame.Interaction.InteractionController.ResetAllInteractionControllers();
            ForjaGame.UI.Core.UIPanelBase.CloseAllPanels();

            TeleportAllActivePlayersToMapOrigin();
            Environment.ConsecrationAltarInteractable.LockAndHideSanctumAltar();
            ForjaGame.Resources.MineSpawnerController.Instance?.ResetAndRespawnCaveEnemies();

            if (!ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void TeleportAllActivePlayersToMapOrigin()
        {
            List<Transform> activePlayers = CollectActivePlayers();
            Vector2 baseXZ = (respawnPoint != null && Mathf.Abs(respawnPoint.position.z) < 18f)
                ? new Vector2(respawnPoint.position.x, respawnPoint.position.z)
                : Vector2.zero;

            for (int i = 0; i < activePlayers.Count; i++)
            {
                Transform p = activePlayers[i];
                if (p == null) continue;

                float offsetX = (i == 0) ? 0f : ((i % 2 == 1 ? 1f : -1f) * ((i + 1) / 2) * 2.2f);
                float spawnX = baseXZ.x + offsetX;
                float spawnZ = baseXZ.y;
                float groundY = ProbeInitialGroundY(spawnX, spawnZ, p);

                Vector3 targetPos = new Vector3(spawnX, groundY + 1.08f, spawnZ);
                Quaternion targetRot = Quaternion.identity;

                if (p.TryGetComponent<Unity.Netcode.Components.NetworkTransform>(out var netTransform)
                    && netTransform.IsSpawned && netTransform.CanCommitToTransform)
                {
                    netTransform.Teleport(targetPos, targetRot, p.localScale);
                }

                if (p.TryGetComponent<Rigidbody>(out var rb))
                {
                    if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    rb.position = targetPos;
                    rb.rotation = targetRot;
                }

                p.SetPositionAndRotation(targetPos, targetRot);
                p.GetComponent<Combat.PlayerHealthController>()?.RestoreHealthFull();
                var pm = p.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    pm.SetMovementEnabled(true);
                    if (pm.PlayerCamera != null && pm.PlayerCamera.TryGetComponent<PlayerCameraFollow>(out var splitCam))
                        splitCam.SnapBehindTarget(p);
                }
            }

            Physics.SyncTransforms();
            Transform camTarget = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.transform : (activePlayers.Count > 0 ? activePlayers[0] : null);
            if (camTarget != null) PlayerCameraFollow.Instance?.SnapBehindTarget(camTarget);
        }

        private List<Transform> CollectActivePlayers()
        {
            var list = new List<Transform>();
            void TryAdd(Transform t) { if (t != null && t.gameObject.activeInHierarchy && !list.Contains(t)) list.Add(t); }

            if (PlayerMovement.LocalInstance != null) TryAdd(PlayerMovement.LocalInstance.transform);
            if (Combat.PlayerHealthController.LocalInstance != null) TryAdd(Combat.PlayerHealthController.LocalInstance.transform);
            foreach (var h in Combat.PlayerHealthController.AllPlayers) if (h != null) TryAdd(h.transform);
            foreach (var m in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) if (m != null) TryAdd(m.transform);
            if (list.Count == 0 && playerTransform != null) list.Add(playerTransform);
            return list;
        }

        private static float ProbeInitialGroundY(float x, float z, Transform ignoreRoot) =>
            UnitySafeUtility.ProbeGroundY(new Vector3(x, 0f, z), ignoreRoot, 12f, 28f);

        private static void ReleaseAllOccupiedStations()
        {
            foreach (var st in FindObjectsByType<BaseForgeStationController>(FindObjectsSortMode.None))
                if (st != null && st.IsOccupied) st.ReleaseStation();
        }
    }
}
