using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Audio;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    public enum TutorialStage
    {
        ControlsQTE = 0,
        FurnaceSmelt = 1,
        PressCompact = 2,
        AnvilForge = 3,
        CombatTraining = 4,
        MineFirstEncounter = 5,
        FreePlayDelve = 6
    }

    /// <summary>
    /// Director de Onboarding y Progresión Guiada:
    /// 1) QTE interactivo de controles (WASD -> Shift+Space+Q -> Inventario [E] -> Horno [F]).
    /// 2) Forjado guiado a prueba de errores en Horno, Prensa y Yunque.
    /// 3) Entrenamiento con Dummies (Golpe, Bloqueo y Parry perfecto) frente a la mina.
    /// 4) Primer encuentro guiado en la mina (Araña, Carbón y Pared de Telaraña -> Libertad total).
    /// </summary>
    public partial class ForgeTutorialDirector : MonoBehaviour
    {
        public static ForgeTutorialDirector Instance { get; private set; }

        private TutorialStage _stage = TutorialStage.ControlsQTE;
        private FurnacePanel _furnacePanel; private PressPanel _pressPanel; private AnvilPanel _anvilPanel;
        private FurnaceController _furnaceCtrl; private PressController _pressCtrl; private AnvilController _anvilCtrl;
        private FurnaceStationController _furnaceStation; private PressStationController _pressStation; private AnvilStationController _anvilStation;

        private Light _beaconSpotLight, _floorWarmPoolLight;
        private Transform _stationBeaconRing;
        private Material _beaconRingMat;
        private float _freePlayTipUntil = -1f, _mineBlockedTipUntil = -1f;
        private bool _hasShownFreePlayTip;
        private int _dummyHits, _coalBeforeFirstVeinBreak;
        private bool _dummyBlocked, _dummyParried, _dummyRolled, _hasMinedFirstCoal, _firstCoalVeinBroken;
        private DestructibleTunnelWall _firstTunnelWall;
        private string _activeSpotlightLabel = string.Empty;
        private RectTransform _activeSpotlightTarget;
        private GUIStyle _tipBoxStyle, _tipTitleStyle, _tipBodyStyle, _stepLeftStyle;
        private Texture2D _goldBorderTex, _darkBoxTex;

        public TutorialStage CurrentStage => _stage;
        public bool IsTutorialActive => ForjaGame.Core.TutorialSettings.IsTutorialEnabled && _stage != TutorialStage.FreePlayDelve;
        public bool ShouldBlockMineEntrance => IsTutorialActive && _stage < TutorialStage.MineFirstEncounter;
        public bool IsDummySparringRequested => IsTutorialActive && _stage == TutorialStage.CombatTraining && _dummyHits >= 3 && (!_dummyBlocked || !_dummyParried || !_dummyRolled);
        public bool IsDummyParryLessonActive => IsTutorialActive && _stage == TutorialStage.CombatTraining && _dummyHits >= 3 && _dummyBlocked && !_dummyParried;
        public bool IsDummyRollLessonActive => IsTutorialActive && _stage == TutorialStage.CombatTraining && _dummyHits >= 3 && _dummyBlocked && _dummyParried && !_dummyRolled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureDirectorForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureDirectorForActiveScene();
        public static void EnsureDirectorForActiveScene()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                if (Instance != null) { Destroy(Instance.gameObject); Instance = null; }
                return;
            }
            if (Instance == null) Instance = new GameObject("[FORGE_TUTORIAL_DIRECTOR]").AddComponent<ForgeTutorialDirector>();
        }
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; CreateDiegeticBeaconLights(); }
        private void OnDestroy() { RestoreAllLockedButtons(); if (Instance == this) Instance = null; }

        public bool CanInteractWithStation(BaseForgeStationController station, out string blockMessage)
        {
            blockMessage = string.Empty;
            if (!IsTutorialActive || station == null) return true;
            if (_stage == TutorialStage.ControlsQTE)
            {
                if (_qteStep < 3) { blockMessage = "Complete the Controls QTE on screen first!"; return false; }
                if (station is FurnaceStationController) return true;
                blockMessage = "Go to the illuminated FURNACE and press [F] first!"; return false;
            }
            if (_stage == TutorialStage.FurnaceSmelt && station is not FurnaceStationController) { blockMessage = "Finish smelting in the FURNACE first!"; return false; }
            if (_stage == TutorialStage.PressCompact && station is not PressStationController) { blockMessage = "Compact the Bloom in the PRESS first!"; return false; }
            if (_stage == TutorialStage.AnvilForge && station is not AnvilStationController) { blockMessage = "Forge your first Sword at the ANVIL first!"; return false; }
            return true;
        }

        public void NotifyDummyHit(TrainingDummyController dummy)
        {
            if (!IsTutorialActive || _stage != TutorialStage.CombatTraining || _dummyHits >= 3) return;
            _dummyHits++;
            if (_dummyHits >= 3) ForgeAudioSystem.PlayProcessComplete(0.88f);
            else ForgeAudioSystem.PlayItemSelect();
        }

        public void NotifyDummyBlocked()
        {
            if (!IsTutorialActive || _stage != TutorialStage.CombatTraining || _dummyHits < 3 || _dummyBlocked) return;
            _dummyBlocked = true;
            PlayerHealthController.LocalInstance?.RestoreHealthFull();
            ForgeAudioSystem.PlayProcessComplete(0.90f);
        }

        public void NotifyDummyParried()
        {
            if (!IsTutorialActive || _stage != TutorialStage.CombatTraining || _dummyHits < 3 || !_dummyBlocked || _dummyParried) return;
            _dummyParried = true;
            PlayerHealthController.LocalInstance?.RestoreHealthFull();
            ForgeAudioSystem.PlayProcessComplete(0.94f);
        }

        public void NotifyDummyRolled()
        {
            if (!IsTutorialActive || _stage != TutorialStage.CombatTraining || _dummyHits < 3 || !_dummyBlocked || !_dummyParried || _dummyRolled) return;
            _dummyRolled = true;
            PlayerHealthController.LocalInstance?.RestoreHealthFull();
            ForgeAudioSystem.PlayProcessComplete(0.98f);
            EnterMineFirstEncounterStage();
        }

        private void EnterMineFirstEncounterStage()
        {
            _stage = TutorialStage.MineFirstEncounter;
            var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
            _coalBeforeFirstVeinBreak = inv != null ? inv.GetCount("Coal") : 0;
            _hasMinedFirstCoal = false;
            _firstCoalVeinBroken = false;
            TrainingDummyArenaBuilder.SyncMineEntranceBarrier(false);
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            TrainingDummyArenaBuilder.EnsureTrainingArenaBuilt();
            if (!ForjaGame.Core.TutorialSettings.IsTutorialEnabled)
            {
                TrainingDummyArenaBuilder.SyncMineEntranceBarrier(false);
                HideBeaconLights(); RestoreAllLockedButtons(); return;
            }

            ResolveReferences();
            EvaluateTutorialStageProgression();
            TrainingDummyArenaBuilder.SyncMineEntranceBarrier(ShouldBlockMineEntrance);
            UpdateMineGateBumpNotice();
            UpdateDiegeticStationBeacon();
            UpdateStationUISpotlights();
        }

        private void UpdateMineGateBumpNotice()
        {
            if (!ShouldBlockMineEntrance) return;
            var pl = PlayerMovement.LocalInstance;
            if (pl != null && pl.transform.position.z >= CaveDungeonBuilder.CaveEntranceZ - 2.3f)
                _mineBlockedTipUntil = Time.time + 1.8f;
        }

        public void HideBeaconLights()
        {
            if (_beaconSpotLight != null) _beaconSpotLight.enabled = false;
            if (_floorWarmPoolLight != null) _floorWarmPoolLight.enabled = false;
            if (_stationBeaconRing != null) _stationBeaconRing.gameObject.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (_furnacePanel == null) _furnacePanel = FindFirstObjectByType<FurnacePanel>(FindObjectsInactive.Include);
            if (_pressPanel == null) _pressPanel = FindFirstObjectByType<PressPanel>(FindObjectsInactive.Include);
            if (_anvilPanel == null) _anvilPanel = FindFirstObjectByType<AnvilPanel>(FindObjectsInactive.Include);
            if (_furnaceCtrl == null) _furnaceCtrl = FindFirstObjectByType<FurnaceController>(FindObjectsInactive.Include);
            if (_pressCtrl == null) _pressCtrl = FindFirstObjectByType<PressController>(FindObjectsInactive.Include);
            if (_anvilCtrl == null) _anvilCtrl = FindFirstObjectByType<AnvilController>(FindObjectsInactive.Include);
            if (_furnaceStation == null) _furnaceStation = FindFirstObjectByType<FurnaceStationController>(FindObjectsInactive.Include);
            if (_pressStation == null) _pressStation = FindFirstObjectByType<PressStationController>(FindObjectsInactive.Include);
            if (_anvilStation == null) _anvilStation = FindFirstObjectByType<AnvilStationController>(FindObjectsInactive.Include);
            if (_firstTunnelWall == null)
                foreach (var w in FindObjectsByType<DestructibleTunnelWall>(FindObjectsSortMode.None))
                    if (w != null && w.transform.position.z < 50f) { _firstTunnelWall = w; break; }
        }

        private void EvaluateTutorialStageProgression()
        {
            if (_stage == TutorialStage.FreePlayDelve) return;
            if (_stage == TutorialStage.ControlsQTE) { UpdateControlsQTE(); return; }

            var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
            var equip = PlayerEquipmentController.LocalInstance ?? PlayerEquipmentController.Instance;

            if (_stage == TutorialStage.MineFirstEncounter)
            {
                bool spiderDead = ForjaGame.Resources.MineSpawnerController.FirstDoorSpider != null && !ForjaGame.Resources.MineSpawnerController.FirstDoorSpider.IsAlive;
                var coalVein = ForjaGame.Resources.MineSpawnerController.FirstCoalVein;
                int curCoal = inv != null ? inv.GetCount("Coal") : 0;
                if (!_firstCoalVeinBroken)
                {
                    if (coalVein != null && !coalVein.IsAlive && spiderDead) _firstCoalVeinBroken = true;
                    else _coalBeforeFirstVeinBreak = curCoal;
                }
                else if (curCoal > _coalBeforeFirstVeinBreak)
                {
                    _hasMinedFirstCoal = true;
                }
                if (spiderDead && _hasMinedFirstCoal && (_firstTunnelWall == null || !_firstTunnelWall.IsAlive))
                {
                    _stage = TutorialStage.FreePlayDelve; RestoreAllLockedButtons();
                    ForgeAudioSystem.PlayProcessComplete(1.0f);
                    if (!_hasShownFreePlayTip) { _hasShownFreePlayTip = true; _freePlayTipUntil = Time.time + 18.0f; }
                }
                return;
            }

            if (_stage == TutorialStage.CombatTraining) { if (_dummyHits >= 3 && _dummyBlocked && _dummyParried && _dummyRolled) EnterMineFirstEncounterStage(); return; }

            bool hasForgedEquipped = equip?.CurrentWeapon != null && equip.CurrentWeapon.weaponType != WeaponType.Pickaxe;
            bool hasForgedInInv = false;
            if (inv?.Weapons != null)
                for (int i = 0; i < inv.Weapons.Count; i++)
                    if (inv.Weapons[i] != null && inv.Weapons[i].weaponType != WeaponType.Pickaxe) { hasForgedInInv = true; break; }

            if (hasForgedEquipped || (hasForgedInInv && _stage == TutorialStage.AnvilForge && (_anvilCtrl == null || !_anvilCtrl.HasIngotOnAnvil)))
            {
                if (!hasForgedEquipped && inv != null && equip != null)
                    for (int i = 0; i < inv.Weapons.Count; i++)
                        if (inv.Weapons[i] != null && inv.Weapons[i].weaponType != WeaponType.Pickaxe) { equip.EquipWeapon(inv.Weapons[i]); break; }
                _stage = TutorialStage.CombatTraining;
                RestoreAllLockedButtons();
                return;
            }

            bool hasIngot = (inv?.Ingots != null && inv.Ingots.Count > 0) || (_anvilCtrl != null && (_anvilCtrl.HasIngotOnAnvil || _anvilCtrl.IsCarvingHandle));
            bool hasBloom = (inv?.Blooms != null && inv.Blooms.Count > 0) || (_pressCtrl != null && (_pressCtrl.HasBloom || _pressCtrl.IsCompleted));
            if (hasIngot) _stage = TutorialStage.AnvilForge;
            else if (hasBloom) _stage = TutorialStage.PressCompact;
            else _stage = TutorialStage.FurnaceSmelt;
        }
    }
}
