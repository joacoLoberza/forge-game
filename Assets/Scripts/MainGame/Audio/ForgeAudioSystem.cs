using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ForjaGame.Audio
{
    public enum CreatureSoundType { Aggro, Attack, Hurt, Feed }

    /// <summary>
    /// Sistema centralizado de sonido y música para Forge Game.
    /// Gestiona efectos de estaciones (Horno, Prensa, Yunque), fanfarrias de completado,
    /// combate (tajos, impactos en carne/piedra/madera, minería, criaturas y Jefe Titán)
    /// y música medieval dinámica (Forjado vs Pelea).
    /// </summary>
    public partial class ForgeAudioSystem : MonoBehaviour
    {
        private const string VolumePrefKey = "Forja_MasterVolume_v2";
        private static ForgeAudioSystem _instance;
        private static bool _quitting;

        [Header("Optional Custom AudioClips (Overrides Procedural)")]
        [SerializeField] private AudioClip uiClickClip, uiTabClip, itemPickupClip, itemSelectClip;
        [SerializeField] private AudioClip hammerStrikeClip, chiselCutClip, polishScrapeClip, quenchSizzleClip;
        [SerializeField] private AudioClip pressCrushClip, furnaceStokeClip, swingLightClip, swingHeavyClip;
        [SerializeField] private AudioClip hitFleshClip, hitStoneClip, pickaxeHitClip, altarVictoryClip;

        private AudioSource _uiSource, _forgeHearthLoop, _caveAmbienceLoop;
        private readonly List<AudioSource> _pool = new List<AudioSource>(16);
        private readonly HashSet<int> _hookedButtons = new HashSet<int>();
        private float _masterVolume = 0.85f, _nextButtonScanTime;
        private int _titanRoarToggle, _titanSlamToggle;

        public static ForgeAudioSystem Instance => EnsureInstance();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap() => EnsureInstance();

        public static ForgeAudioSystem EnsureInstance()
        {
            if (_quitting) return null;
            if (_instance != null) return _instance;
            _instance = FindFirstObjectByType<ForgeAudioSystem>();
            if (_instance == null)
            {
                var go = new GameObject("[ForgeAudioSystem]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ForgeAudioSystem>();
            }
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _masterVolume = PlayerPrefs.GetFloat(VolumePrefKey, 0.85f);
            AudioListener.volume = _masterVolume;
            BuildProceduralBank();
            SetupSources();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnApplicationQuit() => _quitting = true;
        private void OnDestroy() { if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AudioListener.volume = _masterVolume;
            _hookedButtons.Clear();
            UpdateAmbienceForScene(scene.name);
            HookSceneButtons();
        }

        private void SetupSources()
        {
            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;
            _uiSource.ignoreListenerPause = true;

            var hearthObj = new GameObject("ForgeHearthAmbience");
            hearthObj.transform.SetParent(transform);
            hearthObj.transform.position = new Vector3(-9.60f, 1.15f, 6.20f);
            _forgeHearthLoop = hearthObj.AddComponent<AudioSource>();
            _forgeHearthLoop.clip = _procFireLoop;
            _forgeHearthLoop.loop = true;
            _forgeHearthLoop.spatialBlend = 0.25f;
            _forgeHearthLoop.minDistance = 8.5f;
            _forgeHearthLoop.maxDistance = 32f;
            _forgeHearthLoop.volume = 0.42f;

            _caveAmbienceLoop = gameObject.AddComponent<AudioSource>();
            _caveAmbienceLoop.clip = _procCaveLoop;
            _caveAmbienceLoop.loop = true;
            _caveAmbienceLoop.spatialBlend = 0f;
            _caveAmbienceLoop.volume = 0f;
            _caveAmbienceLoop.Play();

            SetupMusicAndStationFireSources();

            for (int i = 0; i < 16; i++)
            {
                var chObj = new GameObject($"AudioChannel_{i}");
                chObj.transform.SetParent(transform);
                var s = chObj.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.ignoreListenerPause = true;
                _pool.Add(s);
            }
            UpdateAmbienceForScene(SceneManager.GetActiveScene().name);
        }

        private void UpdateAmbienceForScene(string sceneName)
        {
            bool isMainMenu = !string.IsNullOrEmpty(sceneName) && sceneName.ToLower().Contains("menu");
            if (_forgeHearthLoop != null)
            {
                if (isMainMenu) _forgeHearthLoop.Stop();
                else if (!_forgeHearthLoop.isPlaying) _forgeHearthLoop.Play();
            }
        }

        private void Update()
        {
            UpdateCaveSpatialBlend();
            UpdateDynamicMusicAndStationFires();
            if (Time.unscaledTime >= _nextButtonScanTime)
            {
                _nextButtonScanTime = Time.unscaledTime + 0.35f;
                HookSceneButtons();
            }
        }

        private void HookSceneButtons()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b != null && _hookedButtons.Add(b.GetInstanceID()) && b.GetComponent<ForgeButtonPointerHook>() == null)
                    b.gameObject.AddComponent<ForgeButtonPointerHook>();
            }
        }

        private void UpdateCaveSpatialBlend()
        {
            if (_caveAmbienceLoop == null) return;
            Camera cam = Camera.main;
            bool inCave = cam != null && cam.transform.position.z > 26.5f;
            _caveAmbienceLoop.volume = Mathf.MoveTowards(_caveAmbienceLoop.volume, inCave ? 0.14f : 0f, Time.unscaledDeltaTime * 0.15f);
        }

        public static void SetMasterVolume(float volume)
        {
            var sys = EnsureInstance(); if (sys == null) return;
            sys._masterVolume = Mathf.Clamp01(volume); AudioListener.volume = sys._masterVolume;
            PlayerPrefs.SetFloat(VolumePrefKey, sys._masterVolume);
        }

        public static float GetMasterVolume() => EnsureInstance() != null ? _instance._masterVolume : 0.85f;
        private static AudioClip PickClip(AudioClip custom, AudioClip fallback) => custom != null ? custom : fallback;
        public static void PlayUIClick() { var sys = EnsureInstance(); if (sys != null && Time.frameCount != sys._lastUiFrame) { sys._lastUiFrame = Time.frameCount; sys.Play2D(sys._procUiClick, 0.78f, Random.Range(0.97f, 1.04f)); } }
        public static void PlayUITab() { var sys = EnsureInstance(); if (sys != null && Time.frameCount != sys._lastUiFrame) { sys._lastUiFrame = Time.frameCount; sys.Play2D(sys._procUiTab, 0.82f, Random.Range(0.97f, 1.04f)); } }
        public static void PlayPageTurn() { var sys = EnsureInstance(); sys?.PlayPageTurnInternal(); }
        public static void PlayItemSelect() { var sys = EnsureInstance(); if (sys != null) { sys._lastUiFrame = Time.frameCount; sys.Play2D(PickClip(sys.itemSelectClip, sys._procItemSelect), 0.90f, Random.Range(0.96f, 1.05f)); } }
        public static void PlayItemPickup(Vector3 pos) => EnsureInstance()?.Play2D(PickClip(_instance.itemPickupClip, _instance._procItemPickup), 0.92f, Random.Range(0.96f, 1.04f));
        public static void PlayBladeMountedOnHandle(Vector3 pos) => EnsureInstance()?.PlayStation2D(PickClip(_instance._extBladeMountFit, _instance._procHammerGreat), 0.96f, 1.0f);
        public static void PlayWeaponClaimed(Vector3 pos) => EnsureInstance()?.PlayStation2D(PickClip(_instance._extWeaponClaimed, _instance._procFanfareGreat), 0.98f, 1.0f);
        public static void PlayAltarConsecrate() => EnsureInstance()?.Play2D(PickClip(_instance.altarVictoryClip, _instance._procAltarChord), 0.95f, 1.0f);
        public static void PlayPlayerHurt(Vector3 pos) => EnsureInstance()?.Play3D(_instance._procPlayerHurt, pos, 0.92f, Random.Range(0.93f, 1.04f));

        public static void PlayProcessComplete(float quality01 = 0.85f)
        {
            var sys = EnsureInstance(); if (sys == null) return;
            sys.PlayStation2D(quality01 >= 0.78f ? sys._procFanfareGreat : (quality01 >= 0.48f ? sys._procFanfareGood : sys._procFanfarePoor), 0.95f, 1.0f);
        }

        public static void PlayHammerStrike(Vector3 pos, float timingQuality = 0.7f, float tempNormalized = 0.7f)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            float eff = Mathf.Clamp01(timingQuality);
            AudioClip fallback = eff >= 0.75f ? sys._procHammerGreat : (eff >= 0.42f ? sys._procHammerStrike : sys._procHammerPoor);
            AudioClip clip = PickClip(sys.hammerStrikeClip, fallback);
            float pitch = Mathf.Lerp(1.05f, 0.93f, Mathf.Clamp01(tempNormalized)) + (eff >= 0.75f ? 0.03f : (eff < 0.42f ? -0.06f : 0f));
            sys.PlayStation2D(clip, Mathf.Lerp(0.84f, 1.0f, eff), pitch);
        }

        public static void PlayChiselCut(Vector3 pos, bool isWood = false) =>
            EnsureInstance()?.PlayStation2D(isWood ? PickClip(_instance._procWoodCut, PickClip(_instance._procWallWood, _instance._procChiselCut)) : PickClip(_instance.chiselCutClip, _instance._procChiselCut), 0.95f, Random.Range(0.95f, 1.05f));
        public static void PlayPolishScrape(Vector3 pos) => EnsureInstance()?.PlayStation2D(PickClip(_instance.polishScrapeClip, _instance._procPolishScrape), 0.88f, Random.Range(0.94f, 1.06f));
        public static void PlayQuenchSizzle(Vector3 pos, float intensity = 1f) => EnsureInstance()?.PlayStation2D(PickClip(_instance.quenchSizzleClip, _instance._procQuenchSizzle), Mathf.Clamp(0.96f * intensity, 0.68f, 1.0f), Random.Range(0.95f, 1.04f));
        public static void PlayPressCrush(Vector3 pos)
        {
            var sys = EnsureInstance(); if (sys == null) return;
            sys._pressLoopActiveUntil = Time.unscaledTime + 0.65f;
            if (Time.unscaledTime >= sys._nextPressImpactTime)
            {
                sys._nextPressImpactTime = Time.unscaledTime + 0.14f;
                sys.PlayStation2D(PickClip(sys.pressCrushClip, sys._procPressCrush), 0.85f, Random.Range(0.95f, 1.04f));
            }
        }
        public static void StopPressLoop() { if (_instance != null) _instance._pressLoopActiveUntil = 0f; }
        public static void PlayFurnaceStoke(Vector3 pos) => EnsureInstance()?.PlayStation2D(PickClip(_instance.furnaceStokeClip, _instance._procFurnaceStoke), 0.90f, Random.Range(0.96f, 1.04f));
        public static void PlayCoalAction(Vector3 pos) => EnsureInstance()?.PlayStation2D(PickClip(_instance._procCoalStoke, _instance._procHitStone), 0.92f, Random.Range(0.94f, 1.05f));

        public static void PlayWeaponSwing(Vector3 pos, bool isHeavy2H, bool isPickaxe = false) { var s = EnsureInstance(); if (s != null) s.Play3D(isPickaxe ? (s._procSwingPickaxe ?? s._procSwingHeavy) : (isHeavy2H ? PickClip(s.swingHeavyClip, s._procSwingHeavy) : PickClip(s.swingLightClip, s._procSwingLight)), pos, isHeavy2H || isPickaxe ? 0.86f : 0.74f, Random.Range(0.94f, 1.06f)); }
        public static void PlayHitImpact(Vector3 pos, bool isStoneOrArmor, bool isHeavy) { var s = EnsureInstance(); if (s != null) s.Play3D(isStoneOrArmor ? PickClip(s.hitStoneClip, s._procHitStone) : PickClip(s.hitFleshClip, s._procHitFlesh), pos, isHeavy ? 1.0f : 0.90f, Random.Range(0.92f, 1.06f)); }
        public static void PlayWallHit(Vector3 pos, int wallType, bool isEffective) { var s = EnsureInstance(); if (s != null) s.PlayStation2D(wallType == 0 ? s._procChiselCut : (wallType == 1 ? s._procWallWood : s._procHitStone), isEffective ? 1.0f : 0.85f, isEffective ? Random.Range(0.96f, 1.04f) : Random.Range(0.84f, 0.92f)); }

        public static void PlayPickaxeStrike(Vector3 pos, bool depleted = false) =>
            EnsureInstance()?.PlayStation2D(PickClip(_instance.pickaxeHitClip, _instance._procPickaxe), 0.96f, depleted ? 0.84f : Random.Range(0.95f, 1.08f));

        public static void PlaySpiderSound(Vector3 pos, CreatureSoundType type)
        {
            var sys = EnsureInstance(); if (sys == null) return;
            AudioClip clip = type switch { CreatureSoundType.Attack => sys._procSpiderAttack, CreatureSoundType.Hurt => sys._procSpiderHurt, _ => sys._procSpiderAggro };
            sys.Play3D(clip, pos, type == CreatureSoundType.Feed ? 0.48f : 0.92f, type == CreatureSoundType.Feed ? 0.88f : Random.Range(0.93f, 1.08f));
        }

        public static void PlayGolemSound(Vector3 pos, CreatureSoundType type)
        {
            var sys = EnsureInstance(); if (sys == null) return;
            AudioClip voice = type switch { CreatureSoundType.Attack => sys._procGolemAttack, CreatureSoundType.Hurt => sys._procGolemHurt, _ => sys._procGolemAggro };
            sys.Play3D(voice, pos, type == CreatureSoundType.Feed ? 0.62f : 1.0f, type == CreatureSoundType.Feed ? 0.92f : Random.Range(0.88f, 1.00f));
            if (sys._procGolemStone != null) sys.Play3D(sys._procGolemStone, pos, type == CreatureSoundType.Attack ? 0.82f : 0.60f, Random.Range(0.82f, 0.96f));
        }

        public static void PlayTitanSound(Vector3 pos, CreatureSoundType type)
        {
            var sys = EnsureInstance(); if (sys == null) return;
            AudioClip clip = type switch
            {
                CreatureSoundType.Attack => (sys._titanSlamToggle++ % 2 == 0) ? sys._procTitanAttack : sys._procTitanAttack2,
                CreatureSoundType.Hurt => sys._procTitanHurt,
                _ => (sys._titanRoarToggle++ % 2 == 0) ? sys._procTitanAggro : sys._procTitanAggro2
            };
            sys.Play3D(clip, pos, 1.0f, Random.Range(0.88f, 1.03f));
        }
    }
}
