using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    public enum GolemTier
    {
        MinorVein = 0,
        IronGuardian = 1,
        EliteColossus = 2
    }

    /// <summary>
    /// Controlador del Golem subterráneo (hereda de CaveCreatureBase).
    /// Soporta 3 rangos (Veta Menor 0.85x, Guardián Férreo 1.15x, Coloso Élite 1.75x)
    /// y reproducción garantizada de animaciones FBX (walkNormal, attack3/4/6/7, roar, idle).
    /// </summary>
    public partial class GolemEnemyController : CaveCreatureBase
    {
        public static GolemEnemyController Instance { get; private set; }
        public static readonly List<GolemEnemyController> ActiveGolems = new();

        [Header("Estadísticas Específicas del Golem")]
        [SerializeField] private GolemTier golemTier = GolemTier.IronGuardian;
        [SerializeField] private int attackDamage = 24;
        [SerializeField] private float moveSpeed = 2.55f;
        [SerializeField] private float visualScaleMultiplier = 1.0f;

        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationClipPlayable _currentClipPlayable;
        private readonly Dictionary<string, AnimationClip> _fallbackClips = new();
        private ResourceNodeController _boundVein;
        private string _lastUsedAttackAnim;
        private int _meleeSwingsSinceSlam;
        private bool _hasRoaredThisEngagement, _isRoaring, _isBullCharging;
        private float _nextAttackCooldown, _nextRoarAllowedTime, _nextChargeCooldown, _nextFissureCooldown, _nextGroundSlamCooldown, _lastStepDustTime;

        public GolemTier Tier => golemTier;
        protected override string DeathAnimKey => "roar";
        protected override void Die() => ExecuteGolemDeathSequence();
        public override void TakeDamage(int amount) => base.TakeDamage(amount);

        protected override void OnHurtAudio() => ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Hurt);

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!ActiveGolems.Contains(this)) ActiveGolems.Add(this);
        }

        protected override void OnDisable()
        {
            ActiveGolems.Remove(this);
            base.OnDisable();
        }

        private void Awake()
        {
            Instance = this;
            if (maxHealth <= 0) maxHealth = 220;
            currentHealth = maxHealth;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.VeinGuardianGolem, detectionRadius, deaggroRadius, GetInstanceID(), _boundVein);
            EnsureVisualsAndComponents();
        }

        private void OnDestroy()
        {
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
            if (Instance == this) Instance = ActiveGolems.Count > 0 ? ActiveGolems[0] : null;
        }

        public void BindHomeVein(ResourceNodeController vein) { _boundVein = vein; _ecoBrain.SetHomeVein(vein); }

        public void ConfigureGolem(
            Vector3 spawnPos, int hp, int dmg, float scaleMult = 1.0f,
            float zoneMinZ = 29f, float zoneMaxZ = 328f, ResourceNodeController homeVein = null,
            GolemTier tier = GolemTier.IronGuardian)
        {
            mapSpawnPoint = spawnPos;
            _zoneMinZ = zoneMinZ;
            _zoneMaxZ = zoneMaxZ;
            golemTier = tier;
            maxHealth = currentHealth = hp;
            attackDamage = dmg;
            visualScaleMultiplier = Mathf.Clamp(scaleMult, 0.85f, 2.30f);
            moveSpeed = Mathf.Clamp(2.35f / Mathf.Pow(Mathf.Max(0.75f, visualScaleMultiplier), 0.30f), 1.80f, 2.60f);
            detectionRadius = Mathf.Max(detectionRadius, 11.5f);
            deaggroRadius = Mathf.Min(deaggroRadius, 18.0f);
            attackRange = Mathf.Clamp(2.20f * Mathf.Lerp(1f, visualScaleMultiplier, 0.65f), 2.2f, 4.8f);
            _boundVein = homeVein;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.VeinGuardianGolem, detectionRadius, deaggroRadius, GetInstanceID(), homeVein);
            EnsureVisualsAndComponents();
            ApplyTierAppearance(tier);
            GetComponent<EnemyLootDropper>()?.SetLootTable(EnemyLootDropper.BuildAreaLootTable(spawnPos.z, homeVein?.ItemId, tier == GolemTier.EliteColossus));
        }

        private void ApplyTierAppearance(GolemTier tier)
        {
            // Conserva la textura pétrea original del Gólem sin teñidos planos ni emisión artificial
            bool elite = tier == GolemTier.EliteColossus || (_boundVein != null && _boundVein.ItemId == "Gold");
            SetCreatureTint(Color.white, Color.black, isElite: elite);
            RefreshGolemMineralCoreColor();
        }

        public void ResetToSpawn()
        {
            StopAllCoroutines();
            _isDead = _hasRoaredThisEngagement = _isRoaring = _isAttacking = false;
            _nextRoarAllowedTime = _nextGroundSlamCooldown = 0f; _meleeSwingsSinceSlam = 0; _lastUsedAttackAnim = null;
            _activeAnimKey = string.Empty;
            currentHealth = maxHealth;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.VeinGuardianGolem, detectionRadius, deaggroRadius, GetInstanceID(), _boundVein);
            if (_col != null) _col.enabled = true;
            if (_rb != null) _rb.isKinematic = false;
            PlaceOnCaveFloor(mapSpawnPoint);
            SetAnimatorSpeed(1.0f);
            PlayGuardedAnim("idle", 0.1f, forceRestart: true);
        }

        public void EnsureVisualsAndComponents()
        {
            visualScaleMultiplier = Mathf.Clamp(visualScaleMultiplier, 0.75f, 1.30f);
            _col = UnitySafeUtility.GetOrAdd<CapsuleCollider>(gameObject);
            _col.height = Mathf.Min(3.0f, 2.10f * visualScaleMultiplier);
            _col.radius = 0.75f * visualScaleMultiplier;
            _col.center = new Vector3(0f, _col.height * 0.5f, 0f);

            _rb = UnitySafeUtility.GetOrAdd<Rigidbody>(gameObject);
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.mass = 300f * visualScaleMultiplier;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            UnitySafeUtility.GetOrAdd<EnemyLootDropper>(gameObject);
            SetupGolemVisualModel();
            CacheRenderers();
            ApplyTierAppearance(golemTier);
            PlaceOnCaveFloor(mapSpawnPoint);
        }

        private void LoadFallbackGolemClips()
        {
            foreach (var c in UnityEngine.Resources.LoadAll<AnimationClip>("Models/GolemModel")) RegisterGolemClip(c);
#if UNITY_EDITOR
            foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Character/golem-animation/source/Model.fbx"))
                if (o is AnimationClip c) RegisterGolemClip(c);
#endif
        }

        private void RegisterGolemClip(AnimationClip c)
        {
            if (c == null || c.name.StartsWith("__preview__")) return;
            string n = c.name.ToLowerInvariant();
            string key = null;
            if (n.Contains("walk")) key = "walkNormal";
            else if (n.Contains("idle")) key = "idle";
            else if (n.Contains("roar")) key = "roar";
            else if (n.Contains("attack3")) key = "attack3";
            else if (n.Contains("attack4")) key = "attack4";
            else if (n.Contains("attack6")) key = "attack6";
            else if (n.Contains("attack7")) key = "attack7";

            if (key != null)
            {
                if (key == "walkNormal" || key == "idle") c.wrapMode = WrapMode.Loop;
                _fallbackClips[key] = c;
            }
            _fallbackClips[c.name] = c;
        }

        protected override void OnAnimatorSpeedChanged(float speed)
        {
            if (_currentClipPlayable.IsValid()) _currentClipPlayable.SetSpeed(Mathf.Max(0f, speed));
        }

        protected override void PlayGuardedAnim(string stateName, float fade = 0.12f, bool allowWhenDead = false, bool forceRestart = false)
        {
            if ((_isDead && !allowWhenDead) || _animator == null) return;
            bool isLoop = stateName == "walkNormal" || stateName == "idle";
            if (!forceRestart && _activeAnimKey == stateName)
            {
                if (_currentClipPlayable.IsValid())
                {
                    _currentClipPlayable.SetSpeed(Mathf.Max(0.05f, _animator.speed));
                    var activeClip = _currentClipPlayable.GetAnimationClip();
                    if (isLoop && activeClip != null && activeClip.length > 0.05f
                        && _currentClipPlayable.GetTime() >= activeClip.length - 0.03f)
                        _currentClipPlayable.SetTime(0);
                }
                else if (isLoop && _animator.runtimeAnimatorController != null && !_animator.IsInTransition(0)
                         && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.96f)
                    _animator.CrossFadeInFixedTime(stateName, 0.06f, 0, 0f);
                return;
            }
            _activeAnimKey = stateName;
            if (_fallbackClips.TryGetValue(stateName, out var clip) && clip != null)
            {
                if (!_playableGraph.IsValid())
                {
                    _playableGraph = PlayableGraph.Create($"GolemAnim_{GetInstanceID()}");
                    _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "GolemAnimOut", _animator);
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
    }
}
