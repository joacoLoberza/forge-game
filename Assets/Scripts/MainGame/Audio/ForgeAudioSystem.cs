using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ForjaGame.Audio
{
    public enum CreatureSoundType { Aggro, Attack, Hurt, Feed }

    /// <summary>
    /// Centralized fail-safe sound system for Forge Game.
    /// Uses assigned AudioClips when available and falls back to procedural synthesis.
    /// </summary>
    public partial class ForgeAudioSystem : MonoBehaviour
    {
        private const string VolumePrefKey = "Forja_MasterVolume";
        private static ForgeAudioSystem _instance;
        private static bool _quitting;

        [Header("Optional Custom AudioClips (Overrides Procedural)")]
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip uiTabClip;
        [SerializeField] private AudioClip hammerStrikeClip;
        [SerializeField] private AudioClip chiselCutClip;
        [SerializeField] private AudioClip polishScrapeClip;
        [SerializeField] private AudioClip quenchSizzleClip;
        [SerializeField] private AudioClip pressCrushClip;
        [SerializeField] private AudioClip furnaceStokeClip;
        [SerializeField] private AudioClip swingLightClip;
        [SerializeField] private AudioClip swingHeavyClip;
        [SerializeField] private AudioClip hitFleshClip;
        [SerializeField] private AudioClip hitStoneClip;
        [SerializeField] private AudioClip pickaxeHitClip;
        [SerializeField] private AudioClip altarVictoryClip;

        private AudioSource _uiSource;
        private AudioSource _forgeHearthLoop;
        private AudioSource _caveAmbienceLoop;
        private readonly List<AudioSource> _pool = new List<AudioSource>(12);
        private readonly HashSet<int> _hookedButtons = new HashSet<int>();
        private float _nextButtonScanTime;
        private float _masterVolume = 0.85f;

        public static ForgeAudioSystem Instance => EnsureInstance();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            EnsureInstance();
        }

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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _masterVolume = PlayerPrefs.GetFloat(VolumePrefKey, 0.85f);
            AudioListener.volume = _masterVolume;

            BuildProceduralBank();
            SetupSources();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnApplicationQuit() => _quitting = true;

        private void OnDestroy()
        {
            if (_instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _hookedButtons.Clear();
            UpdateAmbienceForScene(scene.name);
            HookSceneButtons();
        }

        private void SetupSources()
        {
            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;

            var hearthObj = new GameObject("ForgeHearthAmbience");
            hearthObj.transform.SetParent(transform);
            hearthObj.transform.position = new Vector3(1.0f, 1.4f, 7.0f);
            _forgeHearthLoop = hearthObj.AddComponent<AudioSource>();
            _forgeHearthLoop.clip = _procFireLoop;
            _forgeHearthLoop.loop = true;
            _forgeHearthLoop.spatialBlend = 0.85f;
            _forgeHearthLoop.minDistance = 3f;
            _forgeHearthLoop.maxDistance = 20f;
            _forgeHearthLoop.volume = 0.22f;

            _caveAmbienceLoop = gameObject.AddComponent<AudioSource>();
            _caveAmbienceLoop.clip = _procCaveLoop;
            _caveAmbienceLoop.loop = true;
            _caveAmbienceLoop.spatialBlend = 0f;
            _caveAmbienceLoop.volume = 0f;
            _caveAmbienceLoop.Play();

            for (int i = 0; i < 10; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
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
        }

        private void HookSceneButtons()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b == null) continue;
                int id = b.GetInstanceID();
                if (_hookedButtons.Add(id))
                    b.onClick.AddListener(PlayUIClick);
            }
        }

        private void UpdateCaveSpatialBlend()
        {
            if (_caveAmbienceLoop == null) return;
            Camera cam = Camera.main;
            bool inCave = cam != null && cam.transform.position.z > 27f;
            float targetVol = inCave ? 0.20f : 0f;
            _caveAmbienceLoop.volume = Mathf.MoveTowards(_caveAmbienceLoop.volume, targetVol, Time.unscaledDeltaTime * 0.15f);
        }

        public static void SetMasterVolume(float volume)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            sys._masterVolume = Mathf.Clamp01(volume);
            AudioListener.volume = sys._masterVolume;
            PlayerPrefs.SetFloat(VolumePrefKey, sys._masterVolume);
        }

        public static float GetMasterVolume() => EnsureInstance() != null ? _instance._masterVolume : 0.85f;

        public static void PlayUIClick() => EnsureInstance()?.Play2D(_instance.uiClickClip ?? _instance._procUiClick, 0.42f, Random.Range(0.95f, 1.06f));
        public static void PlayUITab() => EnsureInstance()?.Play2D(_instance.uiTabClip ?? _instance._procUiTab, 0.38f, Random.Range(0.96f, 1.04f));
        public static void PlayAltarConsecrate() => EnsureInstance()?.Play2D(_instance.altarVictoryClip ?? _instance._procAltarChord, 0.65f, 1.0f);
        public static void PlayPlayerHurt(Vector3 pos) => EnsureInstance()?.Play3D(_instance._procPlayerHurt, pos, 0.6f, Random.Range(0.92f, 1.05f));

        public static void PlayHammerStrike(Vector3 pos, float timingQuality = 0.7f, float tempNormalized = 0.7f)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            float pitch = Mathf.Lerp(1.22f, 0.88f, Mathf.Clamp01(tempNormalized)) + (timingQuality > 0.85f ? 0.06f : 0f);
            sys.Play3D(sys.hammerStrikeClip ?? sys._procHammerStrike, pos, Mathf.Lerp(0.45f, 0.78f, timingQuality), pitch);
        }

        public static void PlayChiselCut(Vector3 pos) => EnsureInstance()?.Play3D(_instance.chiselCutClip ?? _instance._procChiselCut, pos, 0.55f, Random.Range(0.95f, 1.08f));
        public static void PlayPolishScrape(Vector3 pos) => EnsureInstance()?.Play3D(_instance.polishScrapeClip ?? _instance._procPolishScrape, pos, 0.35f, Random.Range(0.92f, 1.10f));
        public static void PlayQuenchSizzle(Vector3 pos, float intensity = 1f) => EnsureInstance()?.Play3D(_instance.quenchSizzleClip ?? _instance._procQuenchSizzle, pos, Mathf.Clamp(0.55f * intensity, 0.25f, 0.75f), Random.Range(0.92f, 1.08f));
        public static void PlayPressCrush(Vector3 pos) => EnsureInstance()?.Play3D(_instance.pressCrushClip ?? _instance._procPressCrush, pos, 0.72f, Random.Range(0.94f, 1.03f));
        public static void PlayFurnaceStoke(Vector3 pos) => EnsureInstance()?.Play3D(_instance.furnaceStokeClip ?? _instance._procFurnaceStoke, pos, 0.55f, Random.Range(0.92f, 1.06f));

        public static void PlayWeaponSwing(Vector3 pos, bool isHeavy2H)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            AudioClip clip = isHeavy2H ? (sys.swingHeavyClip ?? sys._procSwingHeavy) : (sys.swingLightClip ?? sys._procSwingLight);
            sys.Play3D(clip, pos, isHeavy2H ? 0.62f : 0.48f, Random.Range(0.93f, 1.07f));
        }

        public static void PlayHitImpact(Vector3 pos, bool isStoneOrArmor, bool isHeavy)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            AudioClip clip = isStoneOrArmor ? (sys.hitStoneClip ?? sys._procHitStone) : (sys.hitFleshClip ?? sys._procHitFlesh);
            sys.Play3D(clip, pos, isHeavy ? 0.75f : 0.58f, Random.Range(0.90f, 1.08f));
        }

        public static void PlayPickaxeStrike(Vector3 pos, bool depleted = false)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            sys.Play3D(sys.pickaxeHitClip ?? sys._procPickaxe, pos, 0.62f, depleted ? 0.82f : Random.Range(0.95f, 1.12f));
        }

        public static void PlaySpiderSound(Vector3 pos, CreatureSoundType type)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            float pitch = type == CreatureSoundType.Hurt ? 1.25f : (type == CreatureSoundType.Feed ? 0.85f : Random.Range(0.95f, 1.12f));
            sys.Play3D(sys._procSpider, pos, type == CreatureSoundType.Feed ? 0.32f : 0.55f, pitch);
        }

        public static void PlayGolemSound(Vector3 pos, CreatureSoundType type)
        {
            var sys = EnsureInstance();
            if (sys == null) return;
            float pitch = type == CreatureSoundType.Hurt ? 0.82f : (type == CreatureSoundType.Feed ? 1.08f : Random.Range(0.88f, 1.02f));
            sys.Play3D(sys._procGolem, pos, type == CreatureSoundType.Feed ? 0.40f : 0.72f, pitch);
        }
    }
}
