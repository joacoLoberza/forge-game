using System.Collections;
using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Resources;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    /// <summary>
    /// IA de combate, barra de jefe en HUD y 3 ataques especiales procedurales del Titán Abisal:
    /// 1) Onda Sísmica Expansiva (Anillo de Magma), 2) Lluvia de Estalactitas del Techo y 3) Erupción de Agujas Abisales.
    /// </summary>
    public partial class TitanBossController
    {
        private bool _isChasingTarget;

        private void Update()
        {
            if (!IsAlive) return;
            _playerTransform = CaveTargetVisibility.FindClosestCaveTarget(transform.position);
            if (_playerTransform == null) { StopHorizontalMovement(); PlayGuardedAnim("StandIdle"); return; }

            Vector3 toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            if (dist > detectionRadius && !_hasAwakened) { StopHorizontalMovement(); PlayGuardedAnim("StandIdle"); return; }

            if (!_hasAwakened)
            {
                _hasAwakened = true;
                _nextSpecialTime = Time.time + 5.5f;
                ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Aggro);
                PlayerCameraFollow.Instance?.TriggerImpulse(0.18f);
            }

            if (_isStaggered || _isPerformingSpecial || _isAttacking) { StopHorizontalMovement(); return; }
            bool wallBlocked = HasWallBetween(_playerTransform.position);
            if (!wallBlocked && Time.time >= _nextSpecialTime && dist <= 26f) { _isChasingTarget = false; TriggerNextSpecialAttack(toPlayer); return; }

            if (!wallBlocked && (_isChasingTarget ? dist <= attackRange + 0.35f : dist <= attackRange + 1.15f))
            {
                _isChasingTarget = false;
                StopHorizontalMovement();
                FaceDirection(toPlayer, 5.2f);
                if (Time.time >= _nextMeleeTime) StartCoroutine(ExecuteMeleeStrikeRoutine());
                else { SetAnimatorSpeed(0.85f); PlayGuardedAnim("StandIdle"); }
            }
            else if (wallBlocked && dist <= 4.5f)
            {
                _isChasingTarget = false; StopHorizontalMovement(); FaceDirection(toPlayer, 4.5f); PlayGuardedAnim("StandIdle");
            }
            else
            {
                _isChasingTarget = true;
                bool phase2 = currentHealth <= maxHealth * 0.55f;
                float spd = moveSpeed * (phase2 ? 1.08f : 0.96f);
                SetAnimatorSpeed(phase2 ? 1.08f : 0.96f);
                if (!wallBlocked && dist <= 20f && dist > 0.4f) MoveDirectionInCave(toPlayer / dist, spd, 4.2f);
                else MoveAlongCave(_playerTransform.position, spd, 4.5f);
                PlayGuardedAnim("WalkCycle");
            }
        }

        public override void TakeDamage(int amount)
        {
            if (!IsAlive) return;
            _hasAwakened = true;
            base.TakeDamage(amount);
            if (!IsAlive) return;

            float hpPct = (float)currentHealth / Mathf.Max(1, maxHealth);
            if (hpPct <= 1f - (_staggerThresholdStep * 0.25f) && _staggerThresholdStep <= 3 && !_isStaggered)
            {
                _staggerThresholdStep++;
                StartCoroutine(ExecuteHitStaggerRoutine());
            }
        }

        protected override void Die()
        {
            if (_isDead) return;
            _isDead = true;
            StopAllCoroutines();
            ClearActiveArenaHazards();
            StopHorizontalMovement();
            SetAnimatorSpeed(1.0f);
            PlayGuardedAnim("Die", 0.08f, allowWhenDead: true, forceRestart: true);
            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Aggro);
            PlayerCameraFollow.Instance?.TriggerImpulse(0.28f);

            if (_col != null) _col.enabled = false;
            if (_rb != null) _rb.isKinematic = true;
            GetComponent<EnemyLootDropper>()?.DropLoot(transform.position);
            DroppedItemController.Spawn(transform.position + new Vector3(-0.8f, 0.5f, 0f), "Gold", 3, new Vector3(-2.2f, 3.4f, -1.5f));
            DroppedItemController.Spawn(transform.position + new Vector3(0.8f, 0.5f, 0f), "Iron", 4, new Vector3(2.2f, 3.4f, -1.5f));

            ConsecrationAltarInteractable.RevealSanctumAltarAfterTitanDefeat();
            StartCoroutine(BossDeathSinkRoutine());
        }

        private IEnumerator BossDeathSinkRoutine()
        {
            yield return new WaitForSeconds(3.2f);
            Vector3 startPos = transform.position;
            for (float t = 0f; t < 1.8f; t += Time.deltaTime)
            {
                transform.position = startPos + Vector3.down * (t * 2.2f);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private IEnumerator ExecuteHitStaggerRoutine()
        {
            _isStaggered = true;
            _isAttacking = _isPerformingSpecial = false;
            SetAnimatorSpeed(0.90f);
            PlayGuardedAnim((_staggerThresholdStep % 2 == 0) ? "GetHit1" : "GetHit2", 0.06f, forceRestart: true);
            yield return new WaitForSeconds(1.25f);
            _isStaggered = false;
            _nextMeleeTime = Mathf.Max(_nextMeleeTime, Time.time + 1.0f);
            if (IsAlive && _playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) <= 16f)
                StartCoroutine(ExecuteShockwaveRingRoutine());
        }

        private IEnumerator ExecuteMeleeStrikeRoutine()
        {
            _isAttacking = true;
            string clip = _meleeClips[_meleeIndex++ % _meleeClips.Length];
            bool phase2 = currentHealth <= maxHealth * 0.55f;
            float windSpd = phase2 ? 0.62f : 0.54f;
            float strikeSpd = phase2 ? 1.02f : 0.92f;
            float impactClip = clip == "Attack2" ? 1.22f : 0.85f;
            float recoverClip = clip == "Attack2" ? 2.08f : 1.80f;

            SetAnimatorSpeed(windSpd);
            PlayGuardedAnim(clip, 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Aggro);

            float trackCapClip = impactClip * 0.45f;
            float elapsed = 0f;
            while (elapsed < 1.8f && IsAlive && !_isStaggered)
            {
                float ct = GetPlayableClipTime(_currentClipPlayable, _animator, elapsed);
                if (ct >= trackCapClip) break;
                if (_playerTransform != null) FaceDirection(_playerTransform.position - transform.position, 4.5f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!IsAlive || _isStaggered) { _isAttacking = false; yield break; }

            float remClip = Mathf.Max(0.15f, impactClip - GetPlayableClipTime(_currentClipPlayable, _animator, elapsed));
            TitanAttackTelegraphBuilder.SpawnMeleeCleaveTelegraph(this, GetArenaFxRoot(), transform.position, transform.forward, attackRange + 1.55f, 120f, Mathf.Max(0.45f, remClip / windSpd));

            SetAnimatorSpeed(windSpd);
            while (elapsed < 2.5f && IsAlive && !_isStaggered)
            { if (GetPlayableClipTime(_currentClipPlayable, _animator, elapsed) >= impactClip * 0.78f) break; elapsed += Time.deltaTime; yield return null; }
            if (!IsAlive || _isStaggered) { _isAttacking = false; yield break; }

            SetAnimatorSpeed(strikeSpd);
            if (_rb != null && !_rb.isKinematic)
                _rb.linearVelocity = new Vector3(transform.forward.x * 4.0f, _rb.linearVelocity.y, transform.forward.z * 4.0f);
            while (elapsed < 3.0f && IsAlive && !_isStaggered)
            { if (GetPlayableClipTime(_currentClipPlayable, _animator, elapsed) >= impactClip) break; elapsed += Time.deltaTime; yield return null; }

            if (IsAlive && !_isStaggered)
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.22f);
                ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Attack);
                TryDealConeDamageToPlayer(attackRange + 1.55f, 120f, 3.8f, meleeDamage);
            }

            SetAnimatorSpeed(0.85f);
            while (elapsed < 3.6f && IsAlive && !_isStaggered)
            {
                if (GetPlayableClipTime(_currentClipPlayable, _animator, elapsed) >= recoverClip) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetAnimatorSpeed(1.0f);
            _isAttacking = false;
            _nextMeleeTime = Time.time + (phase2 ? 1.40f : 1.90f);
            _nextSpecialTime = Mathf.Max(_nextSpecialTime, Time.time + 1.35f);
        }

        private void OnGUI()
        {
            if (!IsAlive || GameLoopController.IsEndScreenOpen) return;
            Transform p = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.transform : _playerTransform;
            if (p == null || p.position.z < 482f) return;
            EnemyHealthBarHUD.DrawPlayerStyledHealthPlaque(new Rect((Screen.width - 520f) * 0.5f, 18f, 520f, 52f), "⚔ ABYSSAL SANCTUM TITAN ⚔", currentHealth, maxHealth);
        }
    }
}
