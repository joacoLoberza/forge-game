using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    public enum SpiderTier
    {
        Scout = 0,
        Common = 1,
        EliteMatriarch = 2
    }

    /// <summary>
    /// Controlador de la Araña cavernícola (hereda de CaveCreatureBase).
    /// Habita en nichos oscuros y túneles; sale a cazar seres vivos (jugadores o NPCs) cuando tiene hambre
    /// y soporta 3 rangos (Cría Scout 0.65x, Común 1.0x, Matriarca Élite 1.65x).
    /// </summary>
    public partial class SpiderEnemyController : CaveCreatureBase
    {
        public static readonly List<SpiderEnemyController> ActiveSpiders = new();

        [Header("Estadísticas Específicas de la Araña")]
        [SerializeField] private SpiderTier spiderTier = SpiderTier.Common;
        [SerializeField] private int biteDamage = 14;
        [SerializeField] private int lungeDamage = 20;
        [SerializeField] private float walkSpeed = 1.90f;
        [SerializeField] private float runSpeed = 2.85f;
        [SerializeField] private float lungeMinRange = 2.60f;
        [SerializeField] private float lungeMaxRange = 4.85f;
        [SerializeField] private float visualLegSpan = 2.59f;

        private PlayableGraph _playableGraph;
        private AnimationPlayableOutput _playableOutput;
        private AnimationClipPlayable _currentClipPlayable;
        private readonly Dictionary<string, AnimationClip> _fallbackClips = new();
        private SpiderBroodNest _broodNest;
        private bool _isLunging, _isRetreating, _isShootingWeb;
        private float _nextAttackTime, _nextLungeTime, _nextWebSpitTime, _keepDistanceUntil, _nextHurtRetreatTime;
        private int _attackComboCount, _strafeSign = 1;
        private Vector3 _retreatDir;

        private static PhysicsMaterial _slipperySpiderMat;
        public SpiderTier Tier => spiderTier;
        public float ColliderRadius => _col != null ? _col.radius : 0.55f;
        public float ColliderTopY => _col != null ? _col.bounds.max.y : (transform.position.y + 1.25f);
        protected override bool AllowsCrossChamberHunt => true;
        protected override string DeathAnimKey => "die";

        private static PhysicsMaterial GetSlipperyMaterial()
        {
            if (_slipperySpiderMat == null)
                _slipperySpiderMat = new PhysicsMaterial("Mat_SpiderSlippery") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
            return _slipperySpiderMat;
        }
        protected override void OnHurtAudio()
        {
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Hurt);
            // Reacción evasiva controlada (con enfriamiento para evitar giros bruscos al recibir golpes seguidos)
            if (IsAlive && !_isAttacking && !_isLunging && !_isShootingWeb && Time.time >= _nextHurtRetreatTime)
            {
                _nextHurtRetreatTime = Time.time + 2.4f;
                Vector3 away = _playerTransform != null ? (transform.position - _playerTransform.position) : -transform.forward;
                BeginHitAndRunRetreat(0.85f, 2.0f, away);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!ActiveSpiders.Contains(this)) ActiveSpiders.Add(this);
        }

        protected override void OnDisable()
        {
            ActiveSpiders.Remove(this);
            base.OnDisable();
        }

        private void Awake()
        {
            if (maxHealth <= 0) maxHealth = 110;
            currentHealth = maxHealth;
            if (visualLegSpan < 2.2f && spiderTier == SpiderTier.Common) visualLegSpan = 2.59f;
            else if (visualLegSpan < 1.5f && spiderTier == SpiderTier.Scout) visualLegSpan = 1.71f;
            else if (visualLegSpan < 3.5f && spiderTier == SpiderTier.EliteMatriarch) visualLegSpan = 4.13f;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.BroodHunterSpider, detectionRadius, deaggroRadius, GetInstanceID());
            EnsureVisualsAndComponents();
        }

        private void OnDestroy()
        {
            if (_playableGraph.IsValid()) _playableGraph.Destroy();
        }

        public void BindBroodNest(SpiderBroodNest nest) => _broodNest = nest;

        public void ConfigureSpider(
            Vector3 spawnPos, int hp, int biteDmg, float legSpan = 1.85f,
            float zoneMinZ = 29f, float zoneMaxZ = 328f, SpiderBroodNest nest = null,
            SpiderTier tier = SpiderTier.Common)
        {
            mapSpawnPoint = spawnPos;
            _zoneMinZ = zoneMinZ;
            _zoneMaxZ = zoneMaxZ;
            spiderTier = tier;
            maxHealth = currentHealth = hp;
            biteDamage = biteDmg;
            lungeDamage = Mathf.RoundToInt(biteDmg * 1.4f);
            visualLegSpan = legSpan;
            _broodNest = nest;

            if (tier == SpiderTier.Scout) { walkSpeed = 4.2f; runSpeed = 7.4f; attackRange = 1.65f; }
            else if (tier == SpiderTier.EliteMatriarch) { walkSpeed = 3.2f; runSpeed = 6.0f; attackRange = 2.35f; }
            else { walkSpeed = 3.8f; runSpeed = 6.8f; attackRange = 1.90f; }
            _strafeSign = (GetInstanceID() & 1) == 0 ? 1 : -1;

            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.BroodHunterSpider, detectionRadius, deaggroRadius, GetInstanceID());
            EnsureVisualsAndComponents();
            ApplySpiderTierAppearance(tier);
            GetComponent<EnemyLootDropper>()?.SetLootTable(EnemyLootDropper.BuildAreaLootTable(spawnPos.z, null, tier == SpiderTier.EliteMatriarch));
        }

        private void ApplySpiderTierAppearance(SpiderTier tier)
        {
            if (tier == SpiderTier.Scout)
                SetCreatureTint(new Color(0.92f, 0.85f, 0.75f), Color.black, isElite: false);
            else if (tier == SpiderTier.EliteMatriarch)
                SetCreatureTint(new Color(0.46f, 0.72f, 0.44f), new Color(0.05f, 0.22f, 0.07f), isElite: true);
            else
                SetCreatureTint(Color.white, Color.black, isElite: false);
        }

        public void ResetToSpawn()
        {
            StopAllCoroutines();
            _isDead = _isAttacking = _isLunging = _isRetreating = false;
            _activeAnimKey = string.Empty;
            currentHealth = maxHealth;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.BroodHunterSpider, detectionRadius, deaggroRadius, GetInstanceID());
            if (_col != null) _col.enabled = true;
            if (_rb != null) _rb.isKinematic = false;
            PlaceOnCaveFloor(mapSpawnPoint);
            SetAnimatorSpeed(1.0f);
            PlayGuardedAnim("idle", 0.1f, forceRestart: true);
        }

        public void EnsureVisualsAndComponents()
        {
            _col = UnitySafeUtility.GetOrAdd<CapsuleCollider>(gameObject);
            float spiderR = Mathf.Clamp(visualLegSpan * 0.22f, 0.42f, 0.88f);
            float spiderH = Mathf.Max(spiderR * 2.1f, 1.15f);
            _col.radius = spiderR;
            _col.height = spiderH;
            _col.center = new Vector3(0f, spiderH * 0.5f, 0f);
            _col.sharedMaterial = GetSlipperyMaterial();

            _rb = UnitySafeUtility.GetOrAdd<Rigidbody>(gameObject);
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.mass = 95f * Mathf.Clamp(visualLegSpan / 2.59f, 0.6f, 2.2f);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (!TryGetComponent<EnemyLootDropper>(out var dropper))
            {
                dropper = gameObject.AddComponent<EnemyLootDropper>();
                dropper.SetLootTable(EnemyLootDropper.BuildAreaLootTable(mapSpawnPoint.z, null, spiderTier == SpiderTier.EliteMatriarch));
            }

            SetupSpiderModelVisual();
            CacheRenderers();
            ApplySpiderTierAppearance(spiderTier);
            PlaceOnCaveFloor(mapSpawnPoint);
        }
    }
}
