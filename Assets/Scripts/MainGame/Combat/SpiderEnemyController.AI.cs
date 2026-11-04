using System.Collections;
using UnityEngine;
using ForjaGame.Audio;

namespace ForjaGame.Combat
{
    /// <summary>
    /// IA ecológica de la Araña:
    /// 1) Defensa frenética de sus crías (SpiderBroodNest),
    /// 2) Caza letal prioritaria de todo ser vivo cercano,
    /// 3) Repliegue al nido en salud crítica, 4) Descanso junto a los huevos, 5) Merodeo de caza y patrulla.
    /// </summary>
    public partial class SpiderEnemyController
    {
        private void Update()
        {
            if (!IsAlive) return;
            _playerTransform = FindClosestLivingPlayer(transform.position);
            Vector3 toPlayer = _playerTransform != null ? (_playerTransform.position - transform.position) : Vector3.zero;
            toPlayer.y = 0f;
            float dist = _playerTransform != null ? toPlayer.magnitude : 999f;

            if (_isLunging)
            {
                SetAnimatorSpeed(1.25f);
                if (_playerTransform != null) FaceDirection(toPlayer, 13f);
                MoveDirectionInCave(transform.forward, runSpeed * 1.65f, 12f);
                if (Time.time >= _stateEndTime || dist <= attackRange * 0.85f)
                {
                    _isLunging = false;
                    ExecuteSpiderAttack(isLungeFinish: true);
                }
                return;
            }

            if (_isAttacking)
            {
                StopHorizontalMovement();
                if (_playerTransform != null) FaceDirection(toPlayer, 11f);
                if (Time.time >= _stateEndTime)
                {
                    _isAttacking = false;
                    _nextAttackTime = Time.time + 0.82f;
                    if (_attackComboCount % 2 == 0)
                    {
                        _isRetreating = true;
                        _stateEndTime = Time.time + 0.45f;
                    }
                }
                return;
            }

            if (_isRetreating && _playerTransform != null)
            {
                FaceDirection(toPlayer, 11f);
                MoveDirectionInCave(-toPlayer.normalized, walkSpeed * 0.85f, 8f);
                PlayGuardedAnim("walkBack");
                if (Time.time >= _stateEndTime) _isRetreating = false;
                return;
            }

            float hpRatio = (float)currentHealth / Mathf.Max(1, maxHealth);
            EcologicalIntent intent = _ecoBrain.Evaluate(transform.position, hpRatio, _playerTransform, Time.deltaTime);

            switch (intent)
            {
                case EcologicalIntent.DefendBrood:
                    SetAnimatorSpeed(1.18f);
                    ExecuteCombatPursuit(toPlayer, dist, speedMult: 1.16f);
                    break;

                case EcologicalIntent.HuntLivingPrey:
                    SetAnimatorSpeed(1.0f);
                    ExecuteCombatPursuit(toPlayer, dist, speedMult: 1.0f);
                    break;

                case EcologicalIntent.CriticalFlee:
                    SetAnimatorSpeed(1.10f);
                    if (dist <= 1.75f && Time.time >= _nextAttackTime) { ExecuteSpiderAttack(false); return; }
                    if (_playerTransform != null)
                    {
                        Vector3 fleeDir = EcologicalCaveBrain.ComputeSafeFleeDirection(transform.position, _playerTransform.position, _ecoBrain.Anchor);
                        MoveDirectionInCave(fleeDir, runSpeed * 1.06f, 12f);
                        PlayGuardedAnim("run");
                    }
                    break;

                case EcologicalIntent.Resting:
                    ExecuteNestRest();
                    break;

                case EcologicalIntent.ForagingProwl:
                    SetAnimatorSpeed(0.95f);
                    ExecutePatrolMovement(walkSpeed * 0.86f, "largeWalk");
                    break;

                default:
                    SetAnimatorSpeed(0.85f);
                    ExecutePatrolMovement(walkSpeed * 0.62f, "walk");
                    break;
            }
        }

        private void ExecuteCombatPursuit(Vector3 toPlayer, float dist, float speedMult)
        {
            if (_playerTransform == null) { StopHorizontalMovement(); return; }

            if (dist <= attackRange)
            {
                StopHorizontalMovement();
                FaceDirection(toPlayer, 12f);
                if (Time.time >= _nextAttackTime) ExecuteSpiderAttack(isLungeFinish: false);
                else PlayGuardedAnim("idle");
            }
            else if (dist >= lungeMinRange && dist <= lungeMaxRange && Time.time >= _nextLungeTime)
            {
                _isLunging = true;
                _stateEndTime = Time.time + 0.42f;
                _nextLungeTime = Time.time + 4.6f;
                FaceDirection(toPlayer, 15f);
                ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Aggro);
                PlayGuardedAnim("run");
            }
            else
            {
                bool sprint = dist > 3.4f;
                MoveAlongCave(_playerTransform.position, (sprint ? runSpeed : walkSpeed) * speedMult, 12f);
                PlayGuardedAnim(sprint ? "run" : "largeWalk");
            }
        }

        private void ExecuteNestRest()
        {
            Vector3 anchor = _broodNest != null ? _broodNest.transform.position : _ecoBrain.Anchor;
            Vector2 toNest = new Vector2(anchor.x - transform.position.x, anchor.z - transform.position.z);
            if (toNest.sqrMagnitude > 5.2f)
            {
                SetAnimatorSpeed(0.75f);
                MoveAlongCave(anchor, walkSpeed * 0.55f, 8f);
                PlayGuardedAnim("walk");
            }
            else
            {
                StopHorizontalMovement();
                SetAnimatorSpeed(0.14f);
                PlayGuardedAnim("idle", 0.3f);
            }
        }

        private void ExecutePatrolMovement(float speed, string animName)
        {
            if (_ecoBrain.IsPatrolMoving)
            {
                MoveAlongCave(_ecoBrain.PatrolTarget, speed, 10f);
                PlayGuardedAnim(animName);
            }
            else
            {
                StopHorizontalMovement();
                PlayGuardedAnim("idle");
            }
        }

        private void ExecuteSpiderAttack(bool isLungeFinish)
        {
            _isAttacking = true;
            _attackComboCount++;
            _stateEndTime = Time.time + 1.02f;
            _activeAnimKey = string.Empty;
            PlayGuardedAnim("attack", 0.08f);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Attack);
            StartCoroutine(ApplyBiteDelayed(0.40f, isLungeFinish ? lungeDamage : biteDamage));
        }

        private IEnumerator ApplyBiteDelayed(float delay, int damage)
        {
            yield return new WaitForSeconds(delay);
            if (TryDealConeDamageToPlayer(attackRange + 0.55f, 75f, 1.35f, damage))
            {
                _ecoBrain.NotifyFedOnPrey(42f);
                currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.RoundToInt(maxHealth * 0.06f));
            }
        }
    }
}
