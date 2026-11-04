using System;
using System.Collections;
using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Clase base SOLID para criaturas subterráneas (Golems y Arañas).
    /// Centraliza salud, parpadeo MaterialPropertyBlock sin fugas, navegación guiada por la espina
    /// de la caverna, sondeo físico de suelo, animaciones con guardia y daño en cono.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    public abstract class CaveCreatureBase : MonoBehaviour, IDamageable
    {
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        [Header("Estadísticas Base")]
        [SerializeField] protected int maxHealth = 200;
        [SerializeField] protected int currentHealth = 200;
        [SerializeField] protected float detectionRadius = 11.0f;
        [SerializeField] protected float deaggroRadius = 20.0f;
        [SerializeField] protected float attackRange = 2.10f;

        [Header("Territorio")]
        [SerializeField] protected Vector3 mapSpawnPoint = new Vector3(0f, 0.05f, 60f);
        [SerializeField] protected float _zoneMinZ = 29f, _zoneMaxZ = 224f;

        protected Animator _animator;
        protected Rigidbody _rb;
        protected CapsuleCollider _col;
        protected Transform _playerTransform;
        protected readonly EcologicalCaveBrain _ecoBrain = new();
        protected string _activeAnimKey = string.Empty;
        protected bool _isAttacking;
        protected bool _isDead;
        protected float _stateEndTime = -1f;
        private MaterialPropertyBlock _mpb;
        private Renderer[] _cachedRenderers;

        public event Action<CaveCreatureBase> CreatureDied;
        public bool IsAlive => !_isDead && currentHealth > 0;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public Vector3 SpawnPoint => mapSpawnPoint;
        public EcologicalCaveBrain Brain => _ecoBrain;

        protected virtual void OnEnable()
        {
            ResourceNodeController.OnVeinDisturbed += HandleVeinDisturbed;
            SpiderBroodNest.OnBroodThreatened += HandleBroodThreatened;
        }

        protected virtual void OnDisable()
        {
            ResourceNodeController.OnVeinDisturbed -= HandleVeinDisturbed;
            SpiderBroodNest.OnBroodThreatened -= HandleBroodThreatened;
        }

        protected virtual void HandleVeinDisturbed(ResourceNodeController node, GameObject attacker)
        {
            if (node != null && IsAlive) _ecoBrain.NotifyNearbyVeinMined(node.transform.position, node);
        }

        protected virtual void HandleBroodThreatened(Vector3 nestPos)
        {
            if (IsAlive) _ecoBrain.NotifyBroodDisturbed(nestPos);
        }

        public virtual void ForceRitualAggro(Transform target)
        {
            _playerTransform = target;
            _ecoBrain.ForceHostileAlert(25f);
        }

        public virtual void TakeDamage(int amount)
        {
            if (!IsAlive) return;
            _ecoBrain.NotifyDamaged();
            currentHealth = Mathf.Max(0, currentHealth - amount);
            OnHurtAudio();
            StartCoroutine(FlashDamageRoutine());
            if (currentHealth <= 0) Die();
        }

        protected abstract void OnHurtAudio();
        protected abstract string DeathAnimKey { get; }

        protected virtual void Die()
        {
            if (_isDead) return;
            _isDead = true;
            StopHorizontalMovement();
            PlayGuardedAnim(DeathAnimKey, 0.12f, allowWhenDead: true);
            SetAnimatorSpeed(1.0f);
            if (_col != null) _col.enabled = false;
            if (_rb != null) _rb.isKinematic = true;
            GetComponent<EnemyLootDropper>()?.DropLoot(transform.position);
            CreatureDied?.Invoke(this);
            StartCoroutine(SinkAndDestroyRoutine());
        }

        private IEnumerator FlashDamageRoutine()
        {
            _mpb ??= new MaterialPropertyBlock();
            if (_cachedRenderers == null || _cachedRenderers.Length == 0) CacheRenderers();
            Color hitColor = new Color(1f, 0.26f, 0.22f, 1f);
            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                var r = _cachedRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorProp, hitColor);
                _mpb.SetColor(ColorProp, hitColor);
                r.SetPropertyBlock(_mpb);
            }
            yield return new WaitForSeconds(0.11f);
            for (int i = 0; i < _cachedRenderers.Length; i++)
                if (_cachedRenderers[i] != null) _cachedRenderers[i].SetPropertyBlock(null);
        }

        private IEnumerator SinkAndDestroyRoutine()
        {
            yield return new WaitForSeconds(2.0f);
            Vector3 startPos = transform.position;
            for (float elapsed = 0f; elapsed < 1.4f; elapsed += Time.deltaTime)
            {
                transform.position = startPos - Vector3.up * elapsed;
                yield return null;
            }
            UnitySafeUtility.DestroyGameObjectSafe(gameObject);
        }

        protected void CacheRenderers() => _cachedRenderers = GetComponentsInChildren<Renderer>(true);

        protected void MoveAlongCave(Vector3 targetWorldPos, float speed, float turnRate = 7.5f) =>
            MoveDirectionInCave(ComputeCaveGuidedDirection(transform.position, targetWorldPos), speed, turnRate);

        protected void MoveDirectionInCave(Vector3 dir, float speed, float turnRate = 7.5f)
        {
            if (_rb == null || _rb.isKinematic) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { StopHorizontalMovement(); return; }
            dir.Normalize();
            FaceDirection(dir, turnRate);

            float minZ = Mathf.Max(CaveDungeonBuilder.CaveEntranceZ + 1.5f, _zoneMinZ);
            float maxZ = Mathf.Min(CaveDungeonBuilder.CaveEndZ - 1.8f, _zoneMaxZ);
            Vector3 p = transform.position;
            if ((p.z <= minZ && dir.z < 0f) || (p.z >= maxZ && dir.z > 0f)) dir.z = 0f;

            float sx = CaveDungeonBuilder.GetSpineX(p.z);
            CaveDungeonBuilder.GetWallWidthsAtZ(p.z, out float lw, out float rw);
            float minX = sx - Mathf.Max(1.8f, lw - 1.5f), maxX = sx + Mathf.Max(1.8f, rw - 1.5f);
            if ((p.x <= minX && dir.x < 0f) || (p.x >= maxX && dir.x > 0f)) dir.x = (sx - p.x) * 0.5f;

            _rb.linearVelocity = new Vector3(dir.x * speed, _rb.linearVelocity.y, dir.z * speed);
        }

        public static Vector3 ComputeCaveGuidedDirection(Vector3 from, Vector3 to)
        {
            Vector3 direct = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float distZ = Mathf.Abs(direct.z);
            if (distZ > 7.5f)
            {
                float aheadZ = from.z + Mathf.Sign(direct.z) * Mathf.Min(6.5f, distZ * 0.5f);
                float sx = CaveDungeonBuilder.GetSpineX(aheadZ);
                CaveDungeonBuilder.GetWallWidthsAtZ(aheadZ, out float lw, out float rw);
                float clampedX = Mathf.Clamp(Mathf.Lerp(from.x, to.x, 0.45f), sx - (lw - 1.8f), sx + (rw - 1.8f));
                Vector3 steered = new Vector3(clampedX - from.x, 0f, aheadZ - from.z);
                if (steered.sqrMagnitude > 0.04f) return steered.normalized;
            }
            return direct.sqrMagnitude > 0.0001f ? direct.normalized : Vector3.forward;
        }

        protected void StopHorizontalMovement()
        {
            if (_rb != null && !_rb.isKinematic) _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }

        protected void FaceDirection(Vector3 dir, float speed)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * speed);
        }

        protected void PlaceOnCaveFloor(Vector3 desiredPos)
        {
            float baseFloor = CaveDungeonBuilder.GetFloorY(desiredPos.x, desiredPos.z);
            float groundY = UnitySafeUtility.ProbeGroundY(new Vector3(desiredPos.x, baseFloor + 0.1f, desiredPos.z), transform, 5.0f, 18.0f);
            Vector3 finalPos = new Vector3(desiredPos.x, groundY + 0.05f, desiredPos.z);
            transform.position = finalPos;
            if (_rb != null) _rb.position = finalPos;
        }

        protected void SetAnimatorSpeed(float speed)
        {
            if (_animator != null && !Mathf.Approximately(_animator.speed, speed)) _animator.speed = speed;
        }

        protected virtual void PlayGuardedAnim(string stateName, float fade = 0.14f, bool allowWhenDead = false)
        {
            if ((_isDead && !allowWhenDead) || _activeAnimKey == stateName || _animator == null) return;
            _activeAnimKey = stateName;
            if (_animator.runtimeAnimatorController != null) _animator.CrossFade(stateName, fade);
        }

        protected bool TryDealConeDamageToPlayer(float maxReach, float halfConeAngle, float closePointBlank, int dmg)
        {
            if (!IsAlive || _playerTransform == null) return false;
            Vector3 diff = new Vector3(_playerTransform.position.x - transform.position.x, 0f, _playerTransform.position.z - transform.position.z);
            float dist = diff.magnitude;
            if (dist > maxReach || (dist > closePointBlank && Vector3.Angle(transform.forward, diff.normalized) > halfConeAngle)) return false;
            var ph = _playerTransform.GetComponentInParent<PlayerHealthController>();
            if (ph == null || !ph.IsAlive) return false;
            ph.TakeDamage(dmg);
            return true;
        }

        public static Transform FindClosestLivingPlayer(Vector3 fromPos)
        {
            Transform best = null;
            float bestSq = float.MaxValue;
            var list = PlayerHealthController.AllPlayers;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null || !p.IsAlive || !p.gameObject.activeInHierarchy) continue;
                float sq = (p.transform.position - fromPos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p.transform; }
            }
            return best;
        }

        protected static T LoadCreatureAsset<T>(string editorPath, string resourcePath) where T : UnityEngine.Object
        {
            T res = UnityEngine.Resources.Load<T>(resourcePath);
            if (res != null) return res;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(editorPath);
#else
            return null;
#endif
        }
    }
}
