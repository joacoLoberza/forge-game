using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador del Golem subterráneo (hereda de CaveCreatureBase).
    /// Custodio lítico de las vetas minerales: no ataca a quien pasa de largo, pero defiende
    /// implacablemente su mena si es picada o si sufre daño directo.
    /// </summary>
    public partial class GolemEnemyController : CaveCreatureBase
    {
        public static GolemEnemyController Instance { get; private set; }
        public static readonly List<GolemEnemyController> ActiveGolems = new();

        [Header("Estadísticas Específicas del Golem")]
        [SerializeField] private int attackDamage = 25;
        [SerializeField] private float moveSpeed = 2.55f;
        [SerializeField] private float visualScaleMultiplier = 1.0f;

        private ResourceNodeController _boundVein;
        private readonly string[] _attackSequence = { "attack3", "attack4", "attack6", "attack7" };
        private int _currentAttackIndex;
        private bool _hasRoaredThisEngagement;
        private bool _isRoaring;
        private float _nextAttackCooldown;

        protected override string DeathAnimKey => "idle";
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
            if (maxHealth < 220) maxHealth = 300;
            currentHealth = maxHealth;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.VeinGuardianGolem, detectionRadius, deaggroRadius, GetInstanceID(), _boundVein);
            EnsureVisualsAndComponents();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = ActiveGolems.Count > 0 ? ActiveGolems[0] : null;
        }

        public void BindHomeVein(ResourceNodeController vein)
        {
            _boundVein = vein;
            _ecoBrain.SetHomeVein(vein);
        }

        public void ConfigureGolem(
            Vector3 spawnPos, int hp, int dmg, float scaleMult = 1.0f,
            float zoneMinZ = 29f, float zoneMaxZ = 224f, ResourceNodeController homeVein = null)
        {
            mapSpawnPoint = spawnPos;
            _zoneMinZ = zoneMinZ;
            _zoneMaxZ = zoneMaxZ;
            maxHealth = hp;
            currentHealth = hp;
            attackDamage = dmg;
            visualScaleMultiplier = scaleMult;
            _boundVein = homeVein;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.VeinGuardianGolem, detectionRadius, deaggroRadius, GetInstanceID(), homeVein);
            EnsureVisualsAndComponents();
        }

        public void ResetToSpawn()
        {
            StopAllCoroutines();
            _isDead = _hasRoaredThisEngagement = _isRoaring = _isAttacking = false;
            _activeAnimKey = string.Empty;
            currentHealth = maxHealth;
            _ecoBrain.Initialize(mapSpawnPoint, CreatureEcologyRole.VeinGuardianGolem, detectionRadius, deaggroRadius, GetInstanceID(), _boundVein);
            if (_col != null) _col.enabled = true;
            if (_rb != null) { _rb.isKinematic = false; _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            PlaceOnCaveFloor(mapSpawnPoint);
            SetAnimatorSpeed(1.0f);
            PlayGuardedAnim("idle");
        }

        public void EnsureVisualsAndComponents()
        {
            _col = UnitySafeUtility.GetOrAdd<CapsuleCollider>(gameObject);
            _col.height = 2.10f * visualScaleMultiplier;
            _col.radius = 0.75f * visualScaleMultiplier;
            _col.center = new Vector3(0f, 1.05f * visualScaleMultiplier, 0f);

            _rb = UnitySafeUtility.GetOrAdd<Rigidbody>(gameObject);
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.mass = 300f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            UnitySafeUtility.GetOrAdd<EnemyLootDropper>(gameObject);
            SetupGolemVisualModel();
            CacheRenderers();
            PlaceOnCaveFloor(mapSpawnPoint);
        }
    }
}
