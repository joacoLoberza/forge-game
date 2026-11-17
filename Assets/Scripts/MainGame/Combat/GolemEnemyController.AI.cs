using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// IA ecológica, máquina de estados y sincronización de velocidad de marcha del Gólem subterráneo.
    /// </summary>
    public partial class GolemEnemyController
    {
        private float _stunRecoilUntil;

        protected override void OnParryStunned()
        {
            _isRoaring = _isBullCharging = _isAttacking = false;
            _stunRecoilUntil = Time.time + 0.34f;
            ApplyKnockback(-transform.forward, 3.2f, 0.24f);
            SetAnimatorSpeed(1.25f);
            PlayGuardedAnim("roar", 0.05f, forceRestart: true);
        }

        private void Update()
        {
            if (!IsAlive) return;
            if (IsStunned)
            {
                if (!IsKnockbackActive) StopHorizontalMovement();
                if (Time.time < _stunRecoilUntil) SetAnimatorSpeed(1.20f);
                else { SetAnimatorSpeed(0.16f); PlayGuardedAnim("idle", 0.14f); }
                return;
            }
            Transform livingPlayer = CaveTargetVisibility.FindClosestCaveTarget(transform.position);
            if (livingPlayer != null) _playerTransform = livingPlayer;
            else
            {
                if (_playerTransform != null && !CaveTargetVisibility.IsTargetAccessibleInCave(transform.position, _playerTransform))
                    _playerTransform = null;
                if (_playerTransform == null || !_playerTransform.gameObject.activeInHierarchy)
                    _playerTransform = FindTrespassingSpiderNearVein();
            }
            Vector3 toPlayer = _playerTransform != null ? (_playerTransform.position - transform.position) : Vector3.zero;
            toPlayer.y = 0f; float dist = _playerTransform != null ? toPlayer.magnitude : 999f;
            float hpRatio = (float)currentHealth / Mathf.Max(1, maxHealth);

            if (_isBullCharging)
            {
                if (_playerTransform == null || HasWallBetween(_playerTransform.position) || Time.time >= _stateEndTime || dist <= attackRange * 0.90f)
                {
                    _isBullCharging = false;
                    StopHorizontalMovement();
                    if (_playerTransform != null && dist <= attackRange * 1.25f && !HasWallBetween(_playerTransform.position))
                        ExecuteTacticalGolemAttack("attack4");
                    return;
                }
                float chargeSpd = moveSpeed * 1.42f * visualScaleMultiplier;
                MoveAlongCave(_playerTransform.position, chargeSpd, 5.5f);
                PlayGuardedAnim("walkNormal");
                SetAnimatorSpeed(0.95f);
                if (Time.time >= _lastStepDustTime)
                {
                    _lastStepDustTime = Time.time + 0.28f;
                    EnemyAttackTelegraph.EmitImpactDust(transform.position, transform.forward, 3, 0.45f);
                }
                return;
            }

            if (_isRoaring)
            {
                StopHorizontalMovement();
                if (_playerTransform != null) FaceDirection(toPlayer, 6f);
                if (Time.time >= _stateEndTime) { _isRoaring = false; SetAnimatorSpeed(1.0f); }
                return;
            }

            if (_isAttacking)
            {
                StopHorizontalMovement();
                if (Time.time >= _stateEndTime) { _isAttacking = false; SetAnimatorSpeed(1.0f); _nextAttackCooldown = Time.time + 0.85f; }
                return;
            }

            EcologicalIntent intent = _ecoBrain.Evaluate(transform.position, hpRatio, _playerTransform, Time.deltaTime);
            switch (intent)
            {
                case EcologicalIntent.CriticalFlee:
                    ExecuteTacticalLowHpRetreat(toPlayer, dist);
                    break;
                case EcologicalIntent.SelfDefense:
                case EcologicalIntent.TerritorialDefense:
                    ExecuteGuardianDefense(toPlayer, dist);
                    break;
                case EcologicalIntent.WatchfulVigil:
                    StopHorizontalMovement();
                    SetAnimatorSpeed(0.55f); if (_playerTransform != null) FaceDirection(toPlayer, 3.8f);
                    PlayGuardedAnim("idle");
                    break;
                case EcologicalIntent.FeedingOnVein:
                    ExecuteVeinChanneling();
                    break;
                case EcologicalIntent.Resting:
                    if (Time.time >= _nextRoarAllowedTime) _hasRoaredThisEngagement = false;
                    StopHorizontalMovement(); SetAnimatorSpeed(0.16f); PlayGuardedAnim("idle", 0.35f);
                    break;
                default:
                    if (Time.time >= _nextRoarAllowedTime) _hasRoaredThisEngagement = false;
                    ExecuteOrganicPatrol();
                    break;
            }
        }

        private void ExecuteTacticalLowHpRetreat(Vector3 toPlayer, float dist)
        {
            if (_playerTransform == null) { StopHorizontalMovement(); PlayGuardedAnim("idle"); return; }
            bool wallBlocked = HasWallBetween(_playerTransform.position);
            if (!wallBlocked && dist <= attackRange * 1.18f && Time.time >= _nextAttackCooldown)
            {
                StopHorizontalMovement(); FaceDirection(toPlayer, 9f);
                ExecuteSequentialAttack();
                return;
            }
            if (dist < 5.6f)
            {
                float dirZ = Mathf.Abs(toPlayer.z) > 0.25f ? -Mathf.Sign(toPlayer.z) : (transform.position.z < (_zoneMinZ + _zoneMaxZ) * 0.5f ? 1f : -1f);
                float targetZ = Mathf.Clamp(transform.position.z + dirZ * 4.5f, _zoneMinZ + 2.2f, _zoneMaxZ - 2.2f);
                Vector3 retreatGoal = new Vector3(ForjaGame.Environment.CaveDungeonBuilder.GetSpineX(targetZ), transform.position.y, targetZ);
                float spd = moveSpeed * 0.92f * visualScaleMultiplier;
                PlayGuardedAnim("walkNormal");
                MoveAlongCave(retreatGoal, spd, 7.5f);
                SyncWalkSpeed(spd);
            }
            else
            {
                StopHorizontalMovement(); FaceDirection(toPlayer, 5.5f);
                SetAnimatorSpeed(0.75f); PlayGuardedAnim("idle");
            }
        }

        private void SyncWalkSpeed(float desiredSpeed)
        {
            float naturalSpeed = Mathf.Max(0.85f, 2.45f * visualScaleMultiplier);
            float hVel = _rb != null && !_rb.isKinematic ? new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude : desiredSpeed;
            SetAnimatorSpeed(Mathf.Clamp(hVel / naturalSpeed, 0.30f, 0.78f));
        }

        private void ExecuteGuardianDefense(Vector3 toPlayer, float dist)
        {
            bool wallBlocked = _playerTransform != null && HasWallBetween(_playerTransform.position);
            if (!_hasRoaredThisEngagement && !wallBlocked && Time.time >= _nextRoarAllowedTime)
            {
                _hasRoaredThisEngagement = true; _isRoaring = true; _stateEndTime = Time.time + 1.35f;
                _nextRoarAllowedTime = Time.time + 35f;
                StopHorizontalMovement(); SetAnimatorSpeed(1.0f);
                PlayerCameraFollow.Instance?.TriggerImpulse(0.08f);
                ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
                PlayGuardedAnim("roar", 0.08f, forceRestart: true);
                return;
            }

            if (!wallBlocked && dist <= attackRange)
            {
                StopHorizontalMovement();
                float angleToTarget = toPlayer.sqrMagnitude > 0.01f ? Vector3.Angle(transform.forward, toPlayer.normalized) : 0f;
                FaceDirection(toPlayer, 6.5f);
                if (Time.time >= _nextAttackCooldown)
                {
                    if (angleToTarget > 72f && _attackSequence[_currentAttackIndex] != "attack4")
                        ExecuteTacticalGolemAttack("attack4");
                    else
                        ExecuteSequentialAttack();
                }
                else { SetAnimatorSpeed(0.85f); PlayGuardedAnim("idle"); }
            }
            else if (!wallBlocked && dist >= 4.4f && dist <= 10.8f && Time.time >= _nextFissureCooldown && Time.time >= _nextAttackCooldown)
            {
                _nextFissureCooldown = Time.time + 6.5f;
                FaceDirection(toPlayer, 7.5f);
                ExecuteRockFissureAttack();
            }
            else if (!wallBlocked && dist >= 4.8f && dist <= 10.5f && Time.time >= _nextChargeCooldown)
            {
                // Embestida de Avalancha cuando el jugador intenta mantener distancia media
                _isBullCharging = true;
                _stateEndTime = Time.time + 1.05f;
                _nextChargeCooldown = Time.time + 5.8f;
                ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
                PlayerCameraFollow.Instance?.TriggerImpulse(0.08f);
            }
            else if (wallBlocked && dist <= 3.4f)
            {
                StopHorizontalMovement(); FaceDirection(toPlayer, 6f);
                PlayGuardedAnim("idle");
            }
            else if (_playerTransform != null)
            {
                float chaseSpd = moveSpeed * visualScaleMultiplier;
                MoveAlongCave(_playerTransform.position, chaseSpd, 8.0f);
                PlayGuardedAnim("walkNormal");
                SyncWalkSpeed(chaseSpd);
            }
        }

        private void ExecuteVeinChanneling()
        {
            var vein = _ecoBrain.TargetVein;
            if (vein == null || !vein.IsAlive) { ExecuteOrganicPatrol(); return; }
            if (_ecoBrain.TryPerformFeedBite(transform.position, out bool channeled))
            {
                StopHorizontalMovement(); FaceDirection(vein.transform.position - transform.position, 6f);
                PlayGuardedAnim("attack3", 0.10f, forceRestart: true);
                if (channeled)
                {
                    currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.RoundToInt(maxHealth * 0.08f));
                    ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Feed);
                }
            }
            else
            {
                float spd = moveSpeed * 0.72f * visualScaleMultiplier;
                MoveAlongCave(vein.transform.position, spd, 6f);
                PlayGuardedAnim("walkNormal"); SyncWalkSpeed(spd);
            }
        }

        private void ExecuteOrganicPatrol()
        {
            if (_ecoBrain.IsPatrolMoving)
            {
                float spd = moveSpeed * 0.65f * visualScaleMultiplier;
                MoveAlongCave(_ecoBrain.PatrolTarget, spd, 5.5f);
                PlayGuardedAnim("walkNormal"); SyncWalkSpeed(spd);
            }
            else { StopHorizontalMovement(); PlayGuardedAnim("idle"); }
        }

        private Transform FindTrespassingSpiderNearVein()
        {
            var spiders = SpiderEnemyController.ActiveSpiders;
            for (int i = 0; i < spiders.Count; i++)
                if (spiders[i] != null && spiders[i].IsAlive
                    && !CaveTargetVisibility.HasAliveWallBetweenZ(transform.position.z, spiders[i].transform.position.z)
                    && (spiders[i].transform.position - transform.position).sqrMagnitude <= 42.25f)
                    return spiders[i].transform;
            return null;
        }
    }
}
