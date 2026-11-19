using System.Collections;
using UnityEngine;
using ForjaGame.Audio;

namespace ForjaGame.Combat
{
    public partial class SpiderEnemyController
    {
        private void ExecuteSpiderBite()
        {
            _isAttacking = true;
            _isChargingAttack = true;
            _isArmoredWindup = false;
            _stateEndTime = Time.time + 1.55f;
            StopHorizontalMovement();
            SetAnimatorSpeed(1.20f);
            PlayGuardedAnim("attack", 0.06f, forceRestart: true);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, 0.42f, "SPIDER STRIKE", attackRange, isArmored: false);
            StartCoroutine(PerformSpiderStrikeRoutine(biteDamage));
        }

        private IEnumerator PerformSpiderStrikeRoutine(int dmg)
        {
            // Fase 1 (Interrumpible): Encogimiento breve (0..0.48s clip) siguiendo a la presa
            float elapsed = 0f;
            while (IsAlive && !IsStunned && _isAttacking && elapsed < 0.85f)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                if (ct >= 0.48f) break;
                if (ct < 0.42f && _playerTransform != null)
                {
                    Vector3 toT = _playerTransform.position - transform.position; toT.y = 0f;
                    if (toT.sqrMagnitude > 0.2f) FaceDirection(toT, 14.5f);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!IsAlive || IsStunned || !_isAttacking) { _isChargingAttack = false; yield break; }

            // Fase 2: Mordida fluida hacia la presa con curva de impulso orgánica
            _isChargingAttack = false;
            Vector3 strikeDir = _playerTransform != null ? (_playerTransform.position - transform.position) : transform.forward;
            strikeDir.y = 0f; strikeDir = strikeDir.sqrMagnitude > 0.01f ? strikeDir.normalized : transform.forward;
            FaceDirection(strikeDir, 15f);
            SetAnimatorSpeed(1.80f);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Attack);

            bool hasHitPrey = false;
            while (IsAlive && !IsStunned && _isAttacking && elapsed < 1.35f)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                FaceDirection(strikeDir, 14f);
                if (ct < 0.72f && _rb != null && !_rb.isKinematic)
                {
                    float arc = Mathf.Sin(Mathf.Clamp01((ct - 0.44f) / 0.28f) * Mathf.PI);
                    float dashSpd = runSpeed * (0.45f + 0.95f * arc);
                    _rb.linearVelocity = new Vector3(strikeDir.x * dashSpd, _rb.linearVelocity.y, strikeDir.z * dashSpd);
                }

                if (!hasHitPrey && ct >= 0.56f)
                {
                    ApplyBiteDamageAtImpact(dmg);
                    hasHitPrey = true;
                }
                if (ct >= 0.74f) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!IsAlive || IsStunned || !_isAttacking) yield break;
            if (!hasHitPrey) ApplyBiteDamageAtImpact(dmg);

            // Fase 3: Recuperación limpia y paso lateral veloz
            SetAnimatorSpeed(1.35f);
            while (IsAlive && !IsStunned && _isAttacking && elapsed < 1.55f)
            {
                if (GetPlayableClipTime(_currentClipPlayable, _animator, elapsed) >= 1.02f) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (_isAttacking) CompleteSpiderAttackState();
        }

        private void ApplyBiteDamageAtImpact(int dmg)
        {
            EnemyAttackTelegraph.EmitImpactDust(transform.position + transform.forward * (attackRange * 0.65f), transform.forward, 4, 0.40f);
            if (!IsAlive || IsStunned || _playerTransform == null) return;
            if (Mathf.Abs(_playerTransform.position.y - transform.position.y) > 2.55f) return;

            if (TryDealConeDamageToPlayer(attackRange + 0.50f, 75f, 1.45f, dmg, 2.55f))
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


