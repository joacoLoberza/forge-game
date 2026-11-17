using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Jefe Final del Santuario Profundo (Boss3_Titan.fbx, Cámara 6, Z = 525.5m).
    /// Custodia el centro del Santuario; al derrotarlo ejecuta su animación Die y hace emerger
    /// el Pilar de Victoria (ConsecrationAltarInteractable) desde el subsuelo con animación.
    /// </summary>
    public partial class TitanBossController : CaveCreatureBase
    {
        public static TitanBossController Instance { get; private set; }

        [Header("Estadísticas del Titán Abisal")]
        [SerializeField] private int meleeDamage = 44;
        [SerializeField] private float moveSpeed = 4.65f;
        [SerializeField] private float targetBossHeight = 8.40f;

        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationClipPlayable _currentClipPlayable;
        private readonly Dictionary<string, AnimationClip> _clips = new();
        private readonly string[] _meleeClips = { "Attack1", "Attack2" };
        private int _meleeIndex, _specialAttackIndex, _staggerThresholdStep = 1;
        private float _nextMeleeTime, _nextSpecialTime;
        private bool _isPerformingSpecial, _isStaggered, _hasAwakened;
        private Transform _arenaFxRoot;

        protected override string DeathAnimKey => "Die";
        protected override void OnHurtAudio() => ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Hurt);

        private void Awake()
        {
            Instance = this;
            maxHealth = currentHealth = 2600;
            detectionRadius = 38f; deaggroRadius = 52f; attackRange = 5.75f;
        }

        private void OnDestroy()
        {
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
            if (_arenaFxRoot != null) UnitySafeUtility.DestroyGameObjectSafe(_arenaFxRoot.gameObject);
            if (Instance == this) Instance = null;
        }

        public void ConfigureTitan(Vector3 spawnPos, float zoneMinZ = 496f, float zoneMaxZ = 554f)
        {
            Instance = this;
            mapSpawnPoint = spawnPos; _zoneMinZ = zoneMinZ; _zoneMaxZ = zoneMaxZ;
            maxHealth = currentHealth = 2600;
            detectionRadius = 38f; deaggroRadius = 52f; attackRange = 5.75f;
            _staggerThresholdStep = 1;
            _hasAwakened = _isDead = _isAttacking = _isPerformingSpecial = _isStaggered = false;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.VeinGuardianGolem, detectionRadius, deaggroRadius, GetInstanceID(), null);
            EnsureTitanVisualsAndComponents();
            transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        public void ResetTitanForNewServerMatch()
        {
            StopAllCoroutines(); ClearActiveArenaHazards(); gameObject.SetActive(true);
            _isDead = _isAttacking = _isPerformingSpecial = _isStaggered = _hasAwakened = false;
            _activeAnimKey = string.Empty; _staggerThresholdStep = 1; currentHealth = maxHealth;
            if (_col != null) _col.enabled = true; if (_rb != null) _rb.isKinematic = false;
            PlaceOnCaveFloor(mapSpawnPoint); transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            SetAnimatorSpeed(1.0f); PlayGuardedAnim("StandIdle", 0.1f, allowWhenDead: true, forceRestart: true);
        }

        public void EnsureTitanVisualsAndComponents()
        {
            _col = UnitySafeUtility.GetOrAdd<CapsuleCollider>(gameObject);
            _col.height = targetBossHeight * 0.90f; _col.radius = 2.18f; _col.center = new Vector3(0f, _col.height * 0.5f, 0f);

            _rb = UnitySafeUtility.GetOrAdd<Rigidbody>(gameObject);
            _rb.isKinematic = false; _rb.useGravity = true; _rb.mass = 1450f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            UnitySafeUtility.GetOrAdd<EnemyLootDropper>(gameObject);
            SetupTitanVisualModel();
            CacheRenderers();
            SetCreatureTint(Color.white, Color.black, isElite: true);
            PlaceOnCaveFloor(mapSpawnPoint);
            PlayGuardedAnim("StandIdle", 0.1f, forceRestart: true);
        }

        private void SetupTitanVisualModel()
        {
            Transform visual = transform.Find("Visual_Titan");
            if (visual == null)
            {
                GameObject fbx = LoadCreatureAsset<GameObject>(
                    "Assets/Models/Character/titan-animation/source/Boss3_Titan.fbx", "Models/TitanModel")
                    ?? LoadCreatureAsset<GameObject>("Assets/Models/Character/golem-animation/source/Model.fbx", "Models/GolemModel");
                if (fbx != null)
                {
                    GameObject go = Instantiate(fbx, transform);
                    go.name = "Visual_Titan";
                    go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go.transform.localScale = Vector3.one;
                    visual = go.transform;
                }
            }
            if (visual == null) return;

            if (!visual.TryGetComponent<Animator>(out _animator) || _animator == null)
                if ((_animator = visual.GetComponentInChildren<Animator>()) == null) _animator = visual.gameObject.AddComponent<Animator>();
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var ctrl = LoadCreatureAsset<RuntimeAnimatorController>("Assets/Resources/Animations/TitanAnimator.controller", "Animations/TitanAnimator")
                ?? LoadCreatureAsset<RuntimeAnimatorController>("Assets/Animations/TitanAnimator.controller", "Animations/TitanAnimator");
            if (ctrl != null)
            {
                _animator.runtimeAnimatorController = ctrl;
                foreach (var c in ctrl.animationClips) RegisterTitanClip(c);
            }
            LoadTitanClipsFromAssets();

            ApplyTitanVisualMaterials(visual);
            NormalizeTitanBoundsAndCollider(visual);
            EnsureBossChestAuraLight(visual);
        }

        private void NormalizeTitanBoundsAndCollider(Transform visual)
        {
            visual.localScale = Vector3.one; visual.localPosition = Vector3.zero;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float scaleFactor = Mathf.Clamp(targetBossHeight / Mathf.Max(0.05f, b.size.y), 0.02f, 250f);
            visual.localScale = Vector3.one * scaleFactor;

            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            visual.localPosition = new Vector3(0f, Mathf.Clamp(transform.position.y - b.min.y, -1.5f, 1.5f), 0f);

            if (_col != null)
            {
                _col.height = Mathf.Clamp(b.size.y * 0.92f, 5.0f, 9.8f);
                _col.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.30f, 1.65f, 2.85f);
                _col.center = new Vector3(0f, _col.height * 0.5f, 0f);
            }
        }

        private static void EnsureBossChestAuraLight(Transform visual)
        {
            if (visual.Find("TitanAbyssLight") != null) return;
            var lGo = new GameObject("TitanAbyssLight", typeof(Light));
            lGo.transform.SetParent(visual, false); lGo.transform.localPosition = new Vector3(0f, 2.7f, 0.6f);
            var l = lGo.GetComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.95f, 0.36f, 0.12f); l.range = 16f; l.intensity = 2.1f;
        }

        private void LoadTitanClipsFromAssets()
        {
            foreach (var c in UnityEngine.Resources.LoadAll<AnimationClip>("Models/TitanModel")) RegisterTitanClip(c);
#if UNITY_EDITOR
            foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Character/titan-animation/source/Boss3_Titan.fbx"))
                if (o is AnimationClip c) RegisterTitanClip(c);
#endif
        }

        private void RegisterTitanClip(AnimationClip c)
        {
            if (c == null || c.name.StartsWith("__preview__")) return;
            string n = c.name.ToLowerInvariant();
            string key = n.Contains("idle") ? "StandIdle" : n.Contains("walk") ? "WalkCycle" : n.Contains("attackhard") ? "AttackHard"
                : n.Contains("attack1") ? "Attack1" : n.Contains("attack2") ? "Attack2" : n.Contains("gethit1") ? "GetHit1"
                : n.Contains("gethit2") ? "GetHit2" : n.Contains("die") ? "Die" : null;
            if (key != null)
            {
                c.wrapMode = (key == "StandIdle" || key == "WalkCycle") ? WrapMode.Loop : WrapMode.ClampForever;
                _clips[key] = c;
            }
            _clips[c.name] = c;
        }

        protected override void OnAnimatorSpeedChanged(float speed) { if (_currentClipPlayable.IsValid()) _currentClipPlayable.SetSpeed(Mathf.Max(0.05f, speed)); }

        protected override void PlayGuardedAnim(string stateName, float fade = 0.12f, bool allowWhenDead = false, bool forceRestart = false)
        {
            if ((_isDead && !allowWhenDead) || _animator == null) return;
            bool isLoop = stateName == "StandIdle" || stateName == "WalkCycle";
            if (!forceRestart && _activeAnimKey == stateName)
            {
                if (_currentClipPlayable.IsValid())
                    _currentClipPlayable.SetSpeed(Mathf.Max(0.05f, _animator.speed));
                return;
            }
            _activeAnimKey = stateName;
            if (_clips.TryGetValue(stateName, out var clip) && clip != null)
            {
                if (!_playableGraph.IsValid())
                {
                    _playableGraph = PlayableGraph.Create($"TitanAnim_{GetInstanceID()}");
                    _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "TitanOut", _animator);
                }
                if (_currentClipPlayable.IsValid()) _currentClipPlayable.Destroy();
                _currentClipPlayable = AnimationClipPlayable.Create(_playableGraph, clip);
                _currentClipPlayable.SetTime(0);
                _currentClipPlayable.SetSpeed(Mathf.Max(0.05f, _animator.speed));
                _playableOutput.SetSourcePlayable(_currentClipPlayable);
                _playableGraph.Play();
                return;
            }
            if (_animator.runtimeAnimatorController != null) _animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
        }

        public float GetClipDuration(string key, float fallback = 1.6f) =>
            (_clips.TryGetValue(key, out var c) && c != null && c.length > 0.1f) ? c.length : fallback;
    }
}
