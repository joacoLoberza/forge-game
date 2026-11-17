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
        private float _stunRecoilUntil;

        protected override void OnParryStunned()
        {
            _isAttacking = _isLunging = _isShootingWeb = _isRetreating = false;
            _stunRecoilUntil = Time.time + 0.30f;
            ApplyKnockback(-transform.forward, 3.8f, 0.24f);
            SetAnimatorSpeed(1.55f);
            PlayGuardedAnim("walkBack", 0.04f, forceRestart: true);
        }

        private void Update()
        {
            if (!IsAlive) return;
            if (IsStunned)
            {
                if (!IsKnockbackActive) StopHorizontalMovement();
                if (Time.time < _stunRecoilUntil) { SetAnimatorSpeed(1.45f); PlayGuardedAnim("walkBack"); }
                else { SetAnimatorSpeed(0.14f); PlayGuardedAnim("idle", 0.12f); }
                return;
            }
            _playerTransform = FindClosestPreyTarget();
            Vector3 toPrey = _playerTransform != null ? (_playerTransform.position - transform.position) : Vector3.zero;
            toPrey.y = 0f;
            float dist = _playerTransform != null ? toPrey.magnitude : 999f;
            float hpRatio = (float)currentHealth / Mathf.Max(1, maxHealth);

            bool targetSlowed = IsPreyWebSlowed();
            if (targetSlowed) { _isRetreating = false; _keepDistanceUntil = 0f; }

            if (_isShootingWeb)
            {
                StopHorizontalMovement();
                if (_playerTransform != null && dist > 1.25f) FaceDirection(toPrey, 5.5f);
                if (Time.time >= _stateEndTime) { _isShootingWeb = false; _keepDistanceUntil = Time.time + 0.45f; SetAnimatorSpeed(1.0f); }
                return;
            }

            if (_isLunging)
            {
                if (Time.time >= _stateEndTime) CompleteSpiderAttackState(true);
                return;
            }

            if (_isAttacking)
            {
                StopHorizontalMovement();
                if (Time.time >= _stateEndTime) CompleteSpiderAttackState(false);
                return;
            }

            if (_isRetreating)
            {
                if (_playerTransform != null && dist < 5.4f && !HasWallBetween(_playerTransform.position))
                {
                    if (dist > 1.35f) FaceDirection(toPrey, 4.5f);
                    SetAnimatorSpeed(1.18f); PlayGuardedAnim("walkBack");
                    MoveDirectionInCave(_retreatDir, walkSpeed * 1.10f, 0f);
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
                    SetAnimatorSpeed(1.28f);
                    if (_ecoBrain.IsPatrolMoving) { MoveAlongCave(_ecoBrain.PatrolTarget, walkSpeed * 1.15f, 9.5f); PlayGuardedAnim("largeWalk"); }
                    else { StopHorizontalMovement(); PlayGuardedAnim("idle"); }
                    break;
                case EcologicalIntent.Resting:
                    StopHorizontalMovement(); SetAnimatorSpeed(0.28f); PlayGuardedAnim("idle", 0.30f);
                    break;
                default:
                    SetAnimatorSpeed(1.0f); ExecuteOrganicPatrol();
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

        private void CompleteSpiderAttackState(bool wasLunge)
        {
            _isLunging = false; _isAttacking = false; StopHorizontalMovement(); SetAnimatorSpeed(1.0f);
            bool targetSlowed = IsPreyWebSlowed();
            if (wasLunge)
            {
                if (!targetSlowed) BeginHitAndRunRetreat(1.0f, 2.3f);
                else _nextAttackTime = Time.time + 0.35f;
                return;
            }
            _attackComboCount++;
            if (!targetSlowed || _attackComboCount >= 2) { _attackComboCount = 0; BeginHitAndRunRetreat(1.05f, 2.4f); }
            else _nextAttackTime = Time.time + 0.38f;
        }

        private bool IsPreyWebSlowed() => _playerTransform != null && _playerTransform.GetComponentInParent<PlayerMovement>()?.IsWebSlowed == true;

        private void ExecuteTacticalSkirmishTree(Vector3 toPrey, float dist, bool ferociousBrood, bool targetSlowed)
        {
            if (_playerTransform == null) { ExecuteOrganicPatrol(); return; }
            float spdMult = (ferociousBrood ? 1.10f : 1.0f) * (targetSlowed ? 1.18f : 1.0f);
            float verticalGap = Mathf.Abs(_playerTransform.position.y - transform.position.y);
            bool wallBlocked = HasWallBetween(_playerTransform.position);

            if (wallBlocked && dist <= 2.8f)
            {
                StopHorizontalMovement(); if (dist > 1.2f) FaceDirection(toPrey, 5.5f);
                SetAnimatorSpeed(0.95f); PlayGuardedAnim("idle");
                return;
            }

            if (targetSlowed && !wallBlocked && verticalGap <= 1.55f)
            {
                if (dist <= attackRange)
                {
                    StopHorizontalMovement(); if (dist > 0.9f) FaceDirection(toPrey, 6.8f);
                    if (Time.time >= _nextAttackTime) ExecuteSpiderBite();
                    return;
                }
                if (dist >= 2.2f && dist <= lungeMaxRange + 0.8f && Time.time >= _nextLungeTime)
                { ExecuteSpiderLunge(toPrey.normalized, true); return; }
                SetAnimatorSpeed(1.38f); MoveAlongCave(_playerTransform.position, runSpeed * 1.24f, 6.2f); PlayGuardedAnim("run");
                return;
            }

            if (!wallBlocked && Time.time < _keepDistanceUntil)
            {
                StopHorizontalMovement(); if (dist > 1.2f) FaceDirection(toPrey, 5.5f);
                if (verticalGap <= 2.2f && dist >= 3.4f && dist <= 11.5f && Time.time >= _nextWebSpitTime)
                { ExecuteSpiderWebSpit(toPrey); return; }
                SetAnimatorSpeed(0.75f); PlayGuardedAnim("idle");
                return;
            }

            if (!wallBlocked && verticalGap <= 2.2f && dist >= 4.2f && dist <= 11.0f && Time.time >= _nextWebSpitTime)
            { ExecuteSpiderWebSpit(toPrey); return; }

            if (!wallBlocked && verticalGap <= 1.35f && dist >= lungeMinRange && dist <= lungeMaxRange && Time.time >= _nextLungeTime)
            { ExecuteSpiderLunge(toPrey.normalized, ferociousBrood); return; }

            if (!wallBlocked && verticalGap <= 1.55f && dist <= attackRange)
            {
                StopHorizontalMovement(); if (dist > 0.9f) FaceDirection(toPrey, 6.2f);
                if (Time.time >= _nextAttackTime) ExecuteSpiderBite();
                else { SetAnimatorSpeed(0.85f); PlayGuardedAnim("idle"); }
                return;
            }

            SetAnimatorSpeed(1.16f * spdMult);
            MoveAlongCave(_playerTransform.position, runSpeed * 1.02f * spdMult, 5.8f);
            PlayGuardedAnim("run");
        }

        private Transform FindClosestPreyTarget()
        {
            Transform player = CaveTargetVisibility.FindClosestCaveTarget(transform.position);
            if (player != null) return player;
            float bestSq = float.MaxValue; Transform best = null;
            var dummies = FindObjectsByType<CombatDummyEnemy>(FindObjectsSortMode.None);
            for (int i = 0; i < dummies.Length; i++)
            {
                var d = dummies[i];
                if (d == null || !d.IsAlive || !CaveTargetVisibility.IsTargetAccessibleInCave(transform.position, d.transform)) continue;
                float sq = (d.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq && sq <= 484f) { bestSq = sq; best = d.transform; }
            }
            return best;
        }

        private void ExecuteFleeToShadows(Vector3 toPrey, float dist)
        {
            if (_playerTransform == null) { StopHorizontalMovement(); PlayGuardedAnim("idle"); return; }
            bool wallBlocked = HasWallBetween(_playerTransform.position);
            if (!wallBlocked && dist <= attackRange * 1.25f && Time.time >= _nextAttackTime)
            {
                StopHorizontalMovement(); FaceDirection(toPrey, 9.5f);
                ExecuteSpiderBite();
                return;
            }
            if (dist < 6.4f)
            {
                float dirZ = Mathf.Abs(toPrey.z) > 0.25f ? -Mathf.Sign(toPrey.z) : (transform.position.z < (_zoneMinZ + _zoneMaxZ) * 0.5f ? 1f : -1f);
                float targetZ = Mathf.Clamp(transform.position.z + dirZ * 4.8f, _zoneMinZ + 2.2f, _zoneMaxZ - 2.2f);
                Vector3 retreatGoal = new Vector3(ForjaGame.Environment.CaveDungeonBuilder.GetSpineX(targetZ), transform.position.y, targetZ);
                SetAnimatorSpeed(1.28f); PlayGuardedAnim("run");
                MoveAlongCave(retreatGoal, runSpeed * 0.88f, 9.5f);
            }
            else
            {
                StopHorizontalMovement(); FaceDirection(toPrey, 6.5f);
                if (!wallBlocked && dist <= 11.2f && Time.time >= _nextWebSpitTime) ExecuteSpiderWebSpit(toPrey);
                else { SetAnimatorSpeed(0.80f); PlayGuardedAnim("idle"); }
            }
        }

        private void ExecuteOrganicPatrol()
        {
            if (_ecoBrain.IsPatrolMoving) { MoveAlongCave(_ecoBrain.PatrolTarget, walkSpeed * 0.78f, 8.0f); PlayGuardedAnim("walk"); }
            else { StopHorizontalMovement(); PlayGuardedAnim("idle"); }
        }
    }
}
