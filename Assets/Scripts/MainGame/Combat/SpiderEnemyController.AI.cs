using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Árbol de Reacción Táctico de la Araña Cavernícola (Hostigadora de Alta Movilidad):
    /// 1. Si la presa está ralentizada por telaraña (IsWebSlowed): embiste agresivamente a máxima velocidad para encadenar mordeduras.
    /// 2. Tras acertar un golpe o salto (o al recibir daño): se repliega velozmente hacia atrás/diagonal (Hit & Run) hasta el anillo de seguridad (4.5m-6.8m).
    /// 3. En el anillo de distancia media: orbita lateralmente a gran velocidad manteniendo distancia y dispara telaraña para ralentizar al jugador.
    /// 4. Cuando se abre su ventana ofensiva: se lanza en carrera rápida o salto para acertar un golpe y vuelve a tomar distancia.
    /// </summary>
    public partial class SpiderEnemyController
    {
        private float _stunRecoilUntil, _hurtInterruptUntil, _lastSeenTargetUntil, _openingWaitDeadline;
        private Vector3 _lastKnownTargetPos;
        private bool _alertedPack;

        protected override void OnParryStunned()
        {
            _isAttacking = _isLunging = _isShootingWeb = _isRetreating = _isChargingAttack = false;
            _stunRecoilUntil = Time.time + 0.30f;
            ApplyKnockback(-transform.forward, 3.8f, 0.24f);
            SetAnimatorSpeed(1.55f);
            PlayGuardedAnim("walkBack", 0.04f, forceRestart: true);
        }

        protected override void OnAttackInterruptedByDamage()
        {
            _isAttacking = _isLunging = _isShootingWeb = _isChargingAttack = false;
            _hurtInterruptUntil = Time.time + 0.36f;
            _nextAttackTime = Time.time + 0.65f;
            ApplyKnockback(-transform.forward, 3.4f, 0.22f);
            SetAnimatorSpeed(1.50f);
            PlayGuardedAnim("walkBack", 0.04f, forceRestart: true);
        }

        private void Update()
        {
            if (!IsAlive) return;
            if (IsStunned || Time.time < _hurtInterruptUntil)
            {
                if (!IsKnockbackActive) StopHorizontalMovement();
                if (Time.time < _stunRecoilUntil || Time.time < _hurtInterruptUntil) { SetAnimatorSpeed(1.45f); PlayGuardedAnim("walkBack"); }
                else { SetAnimatorSpeed(0.14f); PlayGuardedAnim("idle", 0.12f); }
                return;
            }
            Transform visiblePrey = FindClosestPreyTarget();
            if (visiblePrey != null)
            {
                _playerTransform = visiblePrey; _lastKnownTargetPos = visiblePrey.position; _lastSeenTargetUntil = Time.time + 3.2f;
                if (!_alertedPack) { _alertedPack = true; CaveTargetVisibility.AlertPackAllies(ActiveSpiders, this, visiblePrey, 15.5f); }
            }
            else
            {
                if (_playerTransform != null && !CaveTargetVisibility.IsTargetAccessibleInCave(transform.position, _playerTransform, requireLineOfSight: false))
                    _playerTransform = null;
                if (_playerTransform == null) _alertedPack = false;
            }
            Vector3 toPrey = _playerTransform != null ? (_playerTransform.position - transform.position) : Vector3.zero;
            toPrey.y = 0f;
            float dist = _playerTransform != null ? toPrey.magnitude : 999f;
            float hpRatio = (float)currentHealth / Mathf.Max(1, maxHealth);

            bool targetSlowed = IsPreyWebSlowed();
            if (targetSlowed) { _isRetreating = false; _keepDistanceUntil = 0f; }

            if (_isAttacking)
            {
                if (Time.time >= _stateEndTime) CompleteSpiderAttackState();
                return;
            }

            if (_isRetreating)
            {
                if (_playerTransform != null && dist < 5.0f && !CaveTargetVisibility.HasCaveWallLineOfSightBlocked(transform.position, _playerTransform.position))
                {
                    MoveDirectionInCave(_retreatDir, walkSpeed * 1.15f, 11f, toPrey);
                    SyncSpiderLocomotion("walkBack", walkSpeed * 1.15f);
                }
                else _isRetreating = false;
                if (Time.time >= _stateEndTime) { _isRetreating = false; StopHorizontalMovement(); SetAnimatorSpeed(1.0f); }
                return;
            }

            EcologicalIntent intent = _ecoBrain.Evaluate(transform.position, hpRatio, _playerTransform, Time.deltaTime);
            switch (intent)
            {
                case EcologicalIntent.CriticalFlee: ExecuteFleeToShadows(toPrey, dist); break;
                case EcologicalIntent.DefendBrood: ExecuteTacticalSkirmishTree(toPrey, dist, true, targetSlowed); break;
                case EcologicalIntent.HuntLivingPrey: ExecuteTacticalSkirmishTree(toPrey, dist, false, targetSlowed); break;
                case EcologicalIntent.ForagingProwl:
                    if (_ecoBrain.IsPatrolMoving) { MoveAlongCave(_ecoBrain.PatrolTarget, walkSpeed * 1.15f, 9.5f); SyncSpiderLocomotion("largeWalk", walkSpeed * 1.15f); }
                    else { StopHorizontalMovement(); SyncSpiderLocomotion("idle", 0f); }
                    break;
                case EcologicalIntent.Resting:
                    StopHorizontalMovement(); SyncSpiderLocomotion("idle", 0f);
                    break;
                default:
                    ExecuteOrganicPatrol();
                    break;
            }
        }

        private void BeginHitAndRunRetreat(float retreatDur, float keepDistDur, Vector3? awayDir = null)
        {
            _isRetreating = true; _stateEndTime = Time.time + retreatDur;
            _keepDistanceUntil = Time.time + keepDistDur; _strafeSign = (GetInstanceID() & 1) == 0 ? 1 : -1;
            _nextAttackTime = Time.time + 0.70f;
            Vector3 away = awayDir ?? (_playerTransform != null ? (transform.position - _playerTransform.position) : -transform.forward);
            away.y = 0f;
            _retreatDir = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
        }

        private void CompleteSpiderAttackState()
        {
            _isLunging = false; _isAttacking = false; _isChargingAttack = false; StopHorizontalMovement(); SetAnimatorSpeed(1.0f);
            BeginHitAndRunRetreat(0.58f, 1.18f);
        }

        private bool IsPreyWebSlowed() => _playerTransform != null && _playerTransform.GetComponentInParent<PlayerMovement>()?.IsWebSlowed == true;

        private void ExecuteTacticalSkirmishTree(Vector3 toPrey, float dist, bool ferociousBrood, bool targetSlowed)
        {
            if (_playerTransform == null) { ExecuteOrganicPatrol(); return; }
            float spdMult = (ferociousBrood ? 1.12f : 1.06f) * (targetSlowed ? 1.16f : 1.0f);
            float verticalGap = Mathf.Abs(_playerTransform.position.y - transform.position.y);
            bool hasLos = !CaveTargetVisibility.HasCaveWallLineOfSightBlocked(transform.position, _playerTransform.position);

            if (!hasLos)
            {
                if (Time.time < _lastSeenTargetUntil && Vector3.Distance(transform.position, _lastKnownTargetPos) > 1.4f)
                { MoveAlongCave(_lastKnownTargetPos, runSpeed * 0.92f, 8.5f); SyncSpiderLocomotion("run", runSpeed * 0.92f); }
                else { StopHorizontalMovement(); SyncSpiderLocomotion("idle", 0f); }
                return;
            }

            CaveTargetVisibility.GetPackSlot(ActiveSpiders, this, _playerTransform, 13.5f, out int rank, out int count, out bool allyAttacking);
            Vector3 toPreyNorm = toPrey.sqrMagnitude > 0.01f ? toPrey.normalized : transform.forward;

            // Acecho lateral orientado hacia la presa (nunca expone el flanco ni golpea de lado)
            if (Time.time < _keepDistanceUntil || (allyAttacking && dist > attackRange + 0.35f && count > 1))
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, toPreyNorm) * _strafeSign;
                float radialPull = Mathf.Clamp((dist - 3.65f) * 0.45f, -0.55f, 0.55f);
                Vector3 orbitDir = (tangent + toPreyNorm * radialPull).normalized;
                MoveDirectionInCave(orbitDir, walkSpeed * 1.24f, 11f, toPreyNorm);
                SyncSpiderLocomotion("walk", walkSpeed * 1.24f);
                return;
            }

            if (verticalGap <= 1.55f && dist <= attackRange + 0.30f)
            {
                FaceDirection(toPreyNorm, 15f);
                if (Time.time >= _nextAttackTime)
                {
                    if (Vector3.Angle(transform.forward, toPreyNorm) > 20f)
                    { StopHorizontalMovement(); SyncSpiderLocomotion("walk", walkSpeed * 0.85f); return; }
                    if (CaveTargetVisibility.ShouldWaitForPlayerAttackOpening(_playerTransform, ref _openingWaitDeadline, 0.95f))
                    {
                        Vector3 strafe = Vector3.Cross(Vector3.up, toPreyNorm) * _strafeSign;
                        Vector3 evade = (-toPreyNorm * 0.55f + strafe * 0.80f).normalized;
                        MoveDirectionInCave(evade, walkSpeed * 1.28f, 12f, toPreyNorm); SyncSpiderLocomotion("walkBack", walkSpeed * 1.28f);
                        return;
                    }
                    StopHorizontalMovement(); ExecuteSpiderBite();
                }
                else { StopHorizontalMovement(); SyncSpiderLocomotion("idle", 0f); }
                return;
            }

            _openingWaitDeadline = 0f;
            Vector3 approachGoal = (count > 1 && dist > 3.2f)
                ? CaveTargetVisibility.ComputePackFlankPosition(_playerTransform.position, transform.position, rank, count, attackRange * 0.9f)
                : _playerTransform.position;
            MoveAlongCave(approachGoal, runSpeed * 1.12f * spdMult, 9.0f);
            SyncSpiderLocomotion("run", runSpeed * 1.12f * spdMult);
        }

        private Transform FindClosestPreyTarget()
        {
            Transform player = CaveTargetVisibility.FindClosestCaveTarget(transform.position, requireLineOfSight: true);
            if (player != null) return player;
            float bestSq = float.MaxValue; Transform best = null;
            var dummies = CombatDummyEnemy.ActiveDummies;
            for (int i = 0; i < dummies.Count; i++)
            {
                var d = dummies[i];
                if (d == null || !d.IsAlive || !CaveTargetVisibility.IsTargetAccessibleInCave(transform.position, d.transform, true)) continue;
                float sq = (d.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq && sq <= 484f) { bestSq = sq; best = d.transform; }
            }
            return best;
        }

        private void ExecuteFleeToShadows(Vector3 toPrey, float dist)
        {
            if (_playerTransform == null) { StopHorizontalMovement(); PlayGuardedAnim("idle"); return; }
            bool hasLos = !CaveTargetVisibility.HasCaveWallLineOfSightBlocked(transform.position, _playerTransform.position);
            if (hasLos && dist <= attackRange * 1.25f && Time.time >= _nextAttackTime)
            { StopHorizontalMovement(); FaceDirection(toPrey, 9.5f); ExecuteSpiderBite(); return; }
            if (dist < 6.4f)
            {
                float dirZ = Mathf.Abs(toPrey.z) > 0.25f ? -Mathf.Sign(toPrey.z) : (transform.position.z < (_zoneMinZ + _zoneMaxZ) * 0.5f ? 1f : -1f);
                float targetZ = Mathf.Clamp(transform.position.z + dirZ * 4.8f, _zoneMinZ + 2.2f, _zoneMaxZ - 2.2f);
                Vector3 retreatGoal = new Vector3(ForjaGame.Environment.CaveDungeonBuilder.GetSpineX(targetZ), transform.position.y, targetZ);
                MoveAlongCave(retreatGoal, runSpeed * 0.90f, 9.5f); SyncSpiderLocomotion("run", runSpeed * 0.90f);
            }
            else
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, toPrey.sqrMagnitude > 0.01f ? toPrey.normalized : Vector3.forward) * _strafeSign;
                MoveAlongCave(_playerTransform.position + tangent * 4.5f, walkSpeed * 1.15f, 7.5f); SyncSpiderLocomotion("walk", walkSpeed * 1.15f);
            }
        }

        private void ExecuteOrganicPatrol()
        {
            if (_ecoBrain.IsPatrolMoving) { MoveAlongCave(_ecoBrain.PatrolTarget, walkSpeed * 0.78f, 8.0f); SyncSpiderLocomotion("walk", walkSpeed * 0.78f); }
            else { StopHorizontalMovement(); SyncSpiderLocomotion("idle", 0f); }
        }
    }
}
