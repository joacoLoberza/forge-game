using System.Collections;
using UnityEngine;
using ForjaGame.Audio;

namespace ForjaGame.Combat
{
    public partial class SpiderEnemyController
    {
        private Transform _jawAnchor;

        private void ExecuteSpiderBite()
        {
            _isAttacking = true;
            _stateEndTime = Time.time + 2.35f;
            StopHorizontalMovement();
            SetAnimatorSpeed(0.70f);
            PlayGuardedAnim("attack", 0.08f, forceRestart: true);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, 0.92f, "SPIDER BITE", attackRange);
            StartCoroutine(PerformSpiderStrikeRoutine(isLunge: false, initialDir: transform.forward, dmg: biteDamage));
        }

        private void ExecuteSpiderLunge(Vector3 dir, bool ferociousBrood)
        {
            _isLunging = true;
            _stateEndTime = Time.time + 2.45f;
            _nextLungeTime = Time.time + (ferociousBrood ? 2.8f : 4.2f);
            StopHorizontalMovement();
            if (dir.sqrMagnitude > 0.01f) FaceDirection(dir, 6.5f);
            SetAnimatorSpeed(0.65f);
            PlayGuardedAnim("attack", 0.06f, forceRestart: true);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, 0.96f, "SPIDER LUNGE", 1.85f);
            StartCoroutine(PerformSpiderStrikeRoutine(isLunge: true, initialDir: dir, dmg: lungeDamage));
        }

        private IEnumerator PerformSpiderStrikeRoutine(bool isLunge, Vector3 initialDir, int dmg)
        {
            // Fase 1: Encogimiento/alzado real de Armature|attack hasta su pico máximo de tensión (0.52s en SpiderModel.fbx)
            float elapsed = 0f;
            while (IsAlive && !IsStunned && (_isAttacking || _isLunging) && elapsed < 1.35f)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                if (ct >= 0.52f) break;
                if (ct < 0.34f && _playerTransform != null)
                {
                    Vector3 toT = _playerTransform.position - transform.position; toT.y = 0f;
                    if (toT.sqrMagnitude > 0.6f) FaceDirection(toT, 4.8f);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!IsAlive || IsStunned || (!(_isAttacking || _isLunging))) yield break;

            // Breve pausa de tensión física antes de soltar la mordida/salto
            SetAnimatorSpeed(0.30f);
            yield return new WaitForSeconds(0.07f);
            if (!IsAlive || IsStunned || (!(_isAttacking || _isLunging))) yield break;

            // Fase 2: Abalanzamiento sostenido hacia la presa + ventana barrida de impacto para conectar con precisión
            Vector3 leapDir = initialDir;
            float leapSpeed = runSpeed * 1.85f;
            if (isLunge)
            {
                Vector3 toTarget = _playerTransform != null ? (_playerTransform.position - transform.position) : initialDir;
                toTarget.y = 0f;
                float distToPrey = _playerTransform != null ? Mathf.Clamp(toTarget.magnitude, 1.8f, 6.0f) : 3.4f;
                leapDir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward;
                transform.rotation = Quaternion.LookRotation(leapDir);
                leapSpeed = Mathf.Clamp(distToPrey / 0.28f, runSpeed * 1.55f, runSpeed * 2.55f);
                if (_rb != null && !_rb.isKinematic)
                    _rb.linearVelocity = new Vector3(leapDir.x * leapSpeed, 2.35f, leapDir.z * leapSpeed);
            }
            SetAnimatorSpeed(isLunge ? 1.28f : 1.35f);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Attack);

            bool hasHitPrey = false;
            float hitCtThreshold = isLunge ? 0.58f : 0.65f;
            while (IsAlive && !IsStunned && (_isAttacking || _isLunging) && elapsed < 2.0f)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                if (!isLunge && ct < 0.68f && _rb != null && !_rb.isKinematic)
                    _rb.linearVelocity = new Vector3(leapDir.x * (walkSpeed * 0.55f), _rb.linearVelocity.y, leapDir.z * (walkSpeed * 0.55f));

                if (isLunge && ct < 0.86f && _rb != null && !_rb.isKinematic)
                {
                    float damp = ct < 0.72f ? 1.0f : Mathf.Lerp(1.0f, 0.35f, (ct - 0.72f) / 0.14f);
                    _rb.linearVelocity = new Vector3(leapDir.x * (leapSpeed * damp), _rb.linearVelocity.y, leapDir.z * (leapSpeed * damp));
                }

                if (!hasHitPrey && ct >= hitCtThreshold)
                {
                    if (isLunge && TryDealConeDamageToPlayer(attackRange + 0.85f, 82f, 1.35f, dmg))
                    {
                        hasHitPrey = true;
                        EnemyAttackTelegraph.EmitImpactDust(transform.position + transform.forward * 1.1f, transform.forward, 5, 0.45f);
                        _ecoBrain.NotifyFedOnPrey(38f);
                    }
                    else if (!isLunge)
                    {
                        ApplyBiteDamageAtImpact(dmg);
                        hasHitPrey = true;
                    }
                }
                if (ct >= (isLunge ? 0.88f : 0.72f)) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!IsAlive || IsStunned || (!(_isAttacking || _isLunging))) yield break;

            if (!hasHitPrey) ApplyBiteDamageAtImpact(dmg);

            // Fase 3: Recuperación limpia de patas antes del repliegue
            SetAnimatorSpeed(0.88f);
            while (IsAlive && !IsStunned && (_isAttacking || _isLunging) && elapsed < 2.3f)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                if (ct >= 1.08f) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            CompleteSpiderAttackState(isLunge);
        }

        private void ExecuteSpiderWebSpit(Vector3 toPrey)
        {
            _isShootingWeb = true;
            _stateEndTime = Time.time + 1.38f;
            _nextWebSpitTime = Time.time + (spiderTier == SpiderTier.EliteMatriarch ? 3.8f : 5.4f);
            StopHorizontalMovement();
            if (toPrey.sqrMagnitude > 0.01f) FaceDirection(toPrey, 6.5f);
            SetAnimatorSpeed(0.92f);
            PlayGuardedAnim("attack", 0.08f, forceRestart: true);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, 0.62f, "WEB SPIT", 1.15f);
            StartCoroutine(ReleaseWebSpitRoutine());
        }

        private IEnumerator ReleaseWebSpitRoutine()
        {
            // Fase 1: Aguardamos alzado trasero (0..0.52s) y snap de cabeza/mandíbula forward (0.58..0.708s) en Armature|attack
            float elapsed = 0f;
            bool snappedSpeed = false;
            while (IsAlive && !IsStunned && _isShootingWeb && elapsed < 1.05f)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                if (ct < 0.46f && _playerTransform != null)
                {
                    Vector3 toT = _playerTransform.position - transform.position; toT.y = 0f;
                    if (toT.sqrMagnitude > 0.4f) FaceDirection(toT, 6.2f);
                }
                if (!snappedSpeed && ct >= 0.50f) { snappedSpeed = true; SetAnimatorSpeed(1.26f); }
                if (ct >= 0.63f) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!IsAlive || IsStunned || !_isShootingWeb) yield break;

            Vector3 spawnOrigin = ResolveWebSpitMuzzlePosition();
            Vector3 targetPos = _playerTransform != null ? _playerTransform.position : transform.position + transform.forward * 5f;
            if (_playerTransform != null)
            {
                var pRb = _playerTransform.GetComponentInParent<Rigidbody>();
                if (pRb != null)
                {
                    Vector3 flatVel = new Vector3(pRb.linearVelocity.x, 0f, pRb.linearVelocity.z);
                    targetPos += Vector3.ClampMagnitude(flatVel * 0.14f, 0.85f);
                }
            }

            EnemyAttackTelegraph.EmitImpactDust(spawnOrigin, transform.forward, 6, 0.34f);
            ForgeAudioSystem.PlaySpiderSound(spawnOrigin, CreatureSoundType.Attack);
            SpiderWebProjectile.Shoot(spawnOrigin, targetPos, spiderTier == SpiderTier.EliteMatriarch ? 2 : 1);

            SetAnimatorSpeed(1.0f);
            while (IsAlive && !IsStunned && _isShootingWeb && elapsed < 1.38f)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                if (ct >= 1.12f) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (_isShootingWeb) { _isShootingWeb = false; _keepDistanceUntil = Time.time + 0.55f; SetAnimatorSpeed(1.0f); }
        }

        private Vector3 ResolveWebSpitMuzzlePosition()
        {
            if (_jawAnchor == null)
            {
                foreach (var tr in GetComponentsInChildren<Transform>(true))
                    if (tr.name == "LJAW" || tr.name == "Bone.005") { _jawAnchor = tr; break; }
            }
            if (_jawAnchor != null)
                return _jawAnchor.position + transform.forward * 0.24f + Vector3.up * 0.08f;
            return transform.position + Vector3.up * 0.58f + transform.forward * 0.92f;
        }

        private void ApplyBiteDamageAtImpact(int dmg)
        {
            EnemyAttackTelegraph.EmitImpactDust(transform.position + transform.forward * (attackRange * 0.65f), transform.forward, 4, 0.40f);
            if (!IsAlive || IsStunned || _playerTransform == null) return;
            if (Mathf.Abs(_playerTransform.position.y - transform.position.y) > 1.55f) return;

            if (TryDealConeDamageToPlayer(attackRange + 0.40f, 65f, 1.10f, dmg))
            { _ecoBrain.NotifyFedOnPrey(38f); return; }

            Vector3 diff = new Vector3(_playerTransform.position.x - transform.position.x, 0f, _playerTransform.position.z - transform.position.z);
            if (diff.magnitude <= attackRange + 0.45f)
            {
                var npc = _playerTransform.GetComponentInParent<IDamageable>();
                if (npc != null && npc != (IDamageable)this && npc.IsAlive)
                { npc.TakeDamage(dmg); _ecoBrain.NotifyFedOnPrey(42f); }
            }
        }
    }
}

