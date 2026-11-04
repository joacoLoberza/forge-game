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
    /// Controlador de la Araña cavernícola (hereda de CaveCreatureBase).
    /// Depredadora activa de seres vivos cercanos y protectora feroz de sus crías (SpiderBroodNest).
    /// </summary>
    public partial class SpiderEnemyController : CaveCreatureBase
    {
        public static readonly List<SpiderEnemyController> ActiveSpiders = new();

        [Header("Estadísticas Específicas de la Araña")]
        [SerializeField] private int biteDamage = 14;
        [SerializeField] private int lungeDamage = 20;
        [SerializeField] private float walkSpeed = 3.25f;
        [SerializeField] private float runSpeed = 5.35f;
        [SerializeField] private float lungeMinRange = 2.60f;
        [SerializeField] private float lungeMaxRange = 4.85f;
        [SerializeField] private float visualLegSpan = 1.85f;

        private PlayableGraph _playableGraph;
        private AnimationClipPlayable _currentClipPlayable;
        private readonly Dictionary<string, AnimationClip> _fallbackClips = new();
        private SpiderBroodNest _broodNest;
        private bool _isLunging;
        private bool _isRetreating;
        private float _nextAttackTime;
        private float _nextLungeTime;
        private int _attackComboCount;

        protected override string DeathAnimKey => "die";
        protected override void OnHurtAudio() => ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Hurt);

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
            if (maxHealth <= 0) maxHealth = 130;
            currentHealth = maxHealth;
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
            float zoneMinZ = 29f, float zoneMaxZ = 224f, SpiderBroodNest nest = null)
        {
            mapSpawnPoint = spawnPos;
            _zoneMinZ = zoneMinZ;
            _zoneMaxZ = zoneMaxZ;
            maxHealth = hp;
            currentHealth = hp;
            biteDamage = biteDmg;
            lungeDamage = Mathf.RoundToInt(biteDmg * 1.4f);
            visualLegSpan = legSpan;
            _broodNest = nest;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.BroodHunterSpider, detectionRadius, deaggroRadius, GetInstanceID());
            EnsureVisualsAndComponents();
        }

        public void ResetToSpawn()
        {
            StopAllCoroutines();
            _isDead = _isAttacking = _isLunging = _isRetreating = false;
            _activeAnimKey = string.Empty;
            currentHealth = maxHealth;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.BroodHunterSpider, detectionRadius, deaggroRadius, GetInstanceID());
            if (_col != null) _col.enabled = true;
            if (_rb != null) { _rb.isKinematic = false; _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            PlaceOnCaveFloor(mapSpawnPoint);
            SetAnimatorSpeed(1.0f);
            PlayGuardedAnim("idle");
        }

        public void EnsureVisualsAndComponents()
        {
            _col = UnitySafeUtility.GetOrAdd<CapsuleCollider>(gameObject);
            float spiderH = Mathf.Clamp(visualLegSpan * 0.28f, 0.40f, 0.60f);
            _col.radius = Mathf.Clamp(visualLegSpan * 0.32f, 0.50f, 0.70f);
            _col.height = spiderH;
            _col.center = new Vector3(0f, spiderH * 0.5f, 0f);

            _rb = UnitySafeUtility.GetOrAdd<Rigidbody>(gameObject);
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.mass = 85f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            if (!TryGetComponent<EnemyLootDropper>(out var dropper))
            {
                dropper = gameObject.AddComponent<EnemyLootDropper>();
                dropper.SetLootTable(new List<LootDropEntry>
                {
                    new LootDropEntry("Coal", 0.70f, 1, 3),
                    new LootDropEntry("Copper", 0.45f, 1, 2),
                    new LootDropEntry("Iron", 0.55f, 1, 2)
                });
            }

            SetupSpiderModelVisual();
            CacheRenderers();
            PlaceOnCaveFloor(mapSpawnPoint);
        }
    }
}
