using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Clase base SOLID para criaturas subterráneas (Golems y Arañas).
    /// Centraliza salud, tinte de variante/élite, parpadeo MaterialPropertyBlock, retroceso físico y muerte.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    public abstract partial class CaveCreatureBase : MonoBehaviour, IDamageable
    {
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        [Header("Estadísticas Base y Territorio")]
        [SerializeField] protected int maxHealth = 80, currentHealth = 80;
        [SerializeField] protected float detectionRadius = 16.0f, deaggroRadius = 85.0f, attackRange = 2.10f;
        [SerializeField] protected Vector3 mapSpawnPoint = new Vector3(0f, 0.05f, 60f);
        [SerializeField] protected float _zoneMinZ = 29f, _zoneMaxZ = 328f;

        protected Animator _animator;
        protected Rigidbody _rb;
        protected CapsuleCollider _col;
        protected Transform _playerTransform;
        protected readonly EcologicalCaveBrain _ecoBrain = new();
        protected string _activeAnimKey = string.Empty;
        protected bool _isAttacking, _isDead, _isEliteVariant;
        protected float _stateEndTime = -1f, _stunnedUntil = -1f;
        public bool IsStunned => Time.time < _stunnedUntil;
        public bool IsAttackingOrWindup => _isAttacking;
        public bool HasTargetEngaged => _playerTransform != null;
        protected Color _baseTintColor = Color.white, _emissionTintColor = Color.black;

        private MaterialPropertyBlock _mpb;
        private Renderer[] _cachedRenderers;
        private Vector3 _knockbackVel;
        private float _knockbackUntil;
        private float _parryStunImmuneUntil = -1f;
        public bool CanBeParryStunned => IsAlive && Time.time >= _parryStunImmuneUntil;

        protected float AccumulateSeparation<T>(System.Collections.Generic.IList<T> list, Vector3 myPos, float myRadius, float pad, ref Vector3 push) where T : CaveCreatureBase
        {
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || o == this || !o.IsAlive) continue;
                Vector3 diff = myPos - o.transform.position; diff.y = 0f;
                float dSq = diff.sqrMagnitude, minD = myRadius + (o._col != null ? o._col.radius : 0.65f) + pad;
                if (dSq < minD * minD && dSq > 0.001f) { float d = Mathf.Sqrt(dSq); push += (diff / d) * ((minD - d) / minD); }
            }
            return push.sqrMagnitude;
        }

        protected virtual void OnAnimatorSpeedChanged(float speed) { }
        protected void NotifyCreatureDiedBase() => CreatureDied?.Invoke(this);
        protected void ClearKnockbackState() => _knockbackUntil = 0f;
        protected bool IsKnockbackActive => Time.time < _knockbackUntil;

        protected static float GetPlayableClipTime(AnimationClipPlayable playable, Animator animator, float elapsedFallback)
        {
            if (playable.IsValid()) return (float)playable.GetTime();
            return animator != null ? elapsedFallback * Mathf.Max(0.1f, animator.speed) : elapsedFallback;
        }

        public event Action<CaveCreatureBase> CreatureDied;
        public bool IsAlive => !_isDead && currentHealth > 0;
        public bool IsEliteVariant => _isEliteVariant;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public Vector3 SpawnPoint => mapSpawnPoint;
        public EcologicalCaveBrain Brain => _ecoBrain;
        protected virtual bool AllowsCrossChamberHunt => false;

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

        public void SetCreatureTint(Color baseTint, Color emissionTint = default, bool isElite = false)
        {
            _baseTintColor = baseTint;
            _emissionTintColor = emissionTint;
            _isEliteVariant = isElite;
            ApplyPersistentTintBlock();
        }

        public void ApplyKnockback(Vector3 dir, float speed, float duration = 0.16f)
        {
            if (!IsAlive) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            _knockbackVel = dir.normalized * (speed * (_isEliteVariant ? 0.55f : 1.0f));
            _knockbackUntil = Time.time + duration;
        }

        public virtual void ApplyParryStun(float duration = 1.05f)
        {
            if (!IsAlive) return;
            if (Time.time < _parryStunImmuneUntil)
            {
                ApplyKnockback(-transform.forward, 1.8f, 0.12f);
                return;
            }
            float effDur = _isEliteVariant ? Mathf.Min(duration, 0.78f) : Mathf.Min(duration, 1.08f);
            _parryStunImmuneUntil = Time.time + 4.5f;
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + effDur);
            _isAttacking = false;
            EnemyAttackTelegraph.Cancel(this);
            OnParryStunned();
            ParryStunStarsEffect.AttachOrRefresh(this, effDur);
        }

        public void ApplyTelegraphTint(Color col, float strength)
        {
            if (_cachedRenderers == null || _cachedRenderers.Length == 0) CacheRenderers();
            _mpb ??= new MaterialPropertyBlock();
            Color baseC = Color.Lerp(_baseTintColor, col, strength);
            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                var r = _cachedRenderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorProp, baseC); _mpb.SetColor(ColorProp, baseC);
                _mpb.SetColor(EmissionColorProp, col * (strength * 0.9f));
                r.SetPropertyBlock(_mpb);
            }
        }

        public void RefreshPersistentTint() => ApplyPersistentTintBlock();

        protected virtual void OnParryStunned() { }

        public virtual void TakeDamage(int amount)
        {
            if (!IsAlive) return;
            _ecoBrain.NotifyDamaged();
            currentHealth = Mathf.Max(0, currentHealth - amount);
            ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzGolpeForja();
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
            _knockbackUntil = 0f;
            EnemyAttackTelegraph.Cancel(this);
            StopHorizontalMovement();
            PlayGuardedAnim(DeathAnimKey, 0.12f, allowWhenDead: true, forceRestart: true);
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
            Color hitColor = new Color(1f, 0.32f, 0.25f, 1f);
            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                var r = _cachedRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorProp, hitColor);
                _mpb.SetColor(ColorProp, hitColor);
                _mpb.SetColor(EmissionColorProp, new Color(0.55f, 0.12f, 0.08f));
                r.SetPropertyBlock(_mpb);
            }
            yield return new WaitForSeconds(0.11f);
            ApplyPersistentTintBlock();
        }

        protected void ApplyPersistentTintBlock()
        {
            if (_cachedRenderers == null || _cachedRenderers.Length == 0) CacheRenderers();
            bool hasCustomTint = _baseTintColor != Color.white || _emissionTintColor.maxColorComponent > 0.01f;
            _mpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                var r = _cachedRenderers[i];
                if (r == null) continue;
                if (!hasCustomTint) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorProp, _baseTintColor);
                _mpb.SetColor(ColorProp, _baseTintColor);
                if (_emissionTintColor.maxColorComponent > 0.01f) _mpb.SetColor(EmissionColorProp, _emissionTintColor);
                r.SetPropertyBlock(_mpb);
            }
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
    }
}
