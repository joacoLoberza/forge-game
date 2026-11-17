using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Navegación guiada por la espina de la caverna, evasión dinámica de obstáculos intermedios
    /// (menas, columnas de estalactitas, muros, Altar), desatasco automático y sondeo multinivel.
    /// </summary>
    public abstract partial class CaveCreatureBase
    {
        private static readonly float[] SteerMagnitudes = { 26f, 52f, 84f, 118f, 152f };
        private static readonly Collider[] _propProbeCache = new Collider[12];
        private Vector3 _lastProgressPos, _smoothedMoveDir;
        private float _lastProgressCheckTime, _unstuckUntilTime, _steerCommitUntil, _unstuckTurnSign = 1f, _steerSideBias = 1f;
        private int _stuckCounter;

        protected void MoveAlongCave(Vector3 targetWorldPos, float speed, float turnRate = 7.5f) =>
            MoveDirectionInCave(SteerAroundObstacles(ComputeCaveGuidedDirection(transform.position, targetWorldPos), targetWorldPos), speed, turnRate);

        protected void MoveDirectionInCave(Vector3 dir, float speed, float turnRate = 7.5f)
        {
            if (_rb == null || _rb.isKinematic) return;
            if (IsStunned) { StopHorizontalMovement(); return; }
            if (Time.time < _knockbackUntil) { _rb.linearVelocity = new Vector3(_knockbackVel.x, _rb.linearVelocity.y, _knockbackVel.z); return; }
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) { StopHorizontalMovement(); return; }
            dir.Normalize();

            Vector3 sep = ComputeCreatureSeparation();
            if (sep.sqrMagnitude > 0.001f) dir = (dir + sep * 0.45f).normalized;

            if (speed > 0.4f && turnRate > 0.5f)
            {
                bool blockedAhead = IsBlockedDual(dir, 0.36f, 1.45f, transform.position + dir * 2.0f, out _);
                bool skipClose = !blockedAhead && _playerTransform != null && Vector3.Distance(transform.position, _playerTransform.position) <= attackRange + 0.65f;
                float interval = _playerTransform != null ? 0.55f : 0.70f;
                if (!skipClose && Time.time - _lastProgressCheckTime >= interval)
                {
                    Vector2 moved = new Vector2(transform.position.x - _lastProgressPos.x, transform.position.z - _lastProgressPos.z);
                    _lastProgressPos = transform.position; _lastProgressCheckTime = Time.time;
                    if (moved.sqrMagnitude < 0.04f && Time.time >= _unstuckUntilTime)
                    {
                        if (_stuckCounter == 0) _unstuckTurnSign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
                        else if (_stuckCounter % 2 == 0) _unstuckTurnSign = -_unstuckTurnSign;
                        _stuckCounter++; _unstuckUntilTime = Time.time + 0.65f;
                    }
                    else if (moved.sqrMagnitude >= 0.08f) _stuckCounter = 0;
                }
                if (!skipClose && Time.time < _unstuckUntilTime)
                    dir = (Quaternion.Euler(0f, 44f * _unstuckTurnSign, 0f) * dir).normalized;
            }

            bool crossHunt = AllowsCrossChamberHunt && _playerTransform != null;
            bool lockFirstWall = _zoneMaxZ < 44.5f;
            float minZ = crossHunt ? (CaveDungeonBuilder.CaveEntranceZ + 1.5f) : Mathf.Max(CaveDungeonBuilder.CaveEntranceZ + 1.5f, _zoneMinZ);
            float maxZ = (crossHunt && !lockFirstWall) ? (CaveDungeonBuilder.CaveEndZ - 1.8f) : Mathf.Min(CaveDungeonBuilder.CaveEndZ - 1.8f, _zoneMaxZ);
            Vector3 p = transform.position;
            if ((p.z <= minZ && dir.z < 0f) || (p.z >= maxZ && dir.z > 0f)) dir.z = 0f;

            float sx = CaveDungeonBuilder.GetSpineX(p.z);
            CaveDungeonBuilder.GetWallWidthsAtZ(p.z, out float lw, out float rw);
            float minX = sx - Mathf.Max(2.2f, lw - 1.8f), maxX = sx + Mathf.Max(2.2f, rw - 1.8f);
            if ((p.x <= minX && dir.x < 0f) || (p.x >= maxX && dir.x > 0f)) dir.x = (sx - p.x) * 0.5f;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();

            _smoothedMoveDir = _smoothedMoveDir.sqrMagnitude < 0.01f ? dir : Vector3.Slerp(_smoothedMoveDir, dir, Time.deltaTime * 7.0f).normalized;
            Vector3 moveDir = turnRate > 0.5f ? _smoothedMoveDir : dir;

            float effSpeed = speed;
            if (turnRate > 0.5f)
            {
                FaceDirection(moveDir, turnRate);
                effSpeed = speed * Mathf.Clamp(Mathf.InverseLerp(0.05f, 0.88f, Vector3.Dot(transform.forward, moveDir)), 0.18f, 1.0f);
            }

            if (Physics.Raycast(transform.position + Vector3.up * 0.6f, Vector3.down, out RaycastHit gHit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 slopeDir = Vector3.ProjectOnPlane(moveDir, gHit.normal).normalized;
                if (slopeDir.y > 0.04f) { _rb.linearVelocity = slopeDir * effSpeed; return; }
            }
            _rb.linearVelocity = new Vector3(moveDir.x * effSpeed, _rb.linearVelocity.y, moveDir.z * effSpeed);
        }

        private Vector3 ComputeCreatureSeparation()
        {
            Vector3 push = Vector3.zero;
            float myR = _col != null ? _col.radius : 0.70f;
            Vector3 myPos = transform.position;
            AccumulateSeparation(GolemEnemyController.ActiveGolems, myPos, myR, 0.55f, ref push);
            AccumulateSeparation(SpiderEnemyController.ActiveSpiders, myPos, myR, 0.35f, ref push);
            int hits = Physics.OverlapSphereNonAlloc(myPos + Vector3.up * 0.45f, myR + 1.15f, _propProbeCache, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                var c = _propProbeCache[i];
                if (c == null || c.GetComponentInParent<ResourceNodeController>() == null) continue;
                Vector3 diff = myPos - c.bounds.ClosestPoint(myPos); diff.y = 0f;
                float r = myR + 0.85f;
                if (diff.sqrMagnitude < r * r && diff.sqrMagnitude > 0.0001f) push += diff.normalized * ((r - diff.magnitude) / r) * 1.45f;
            }
            return push;
        }

        private Vector3 SteerAroundObstacles(Vector3 desiredDir, Vector3 targetPos)
        {
            desiredDir.y = 0f;
            if (desiredDir.sqrMagnitude < 0.0001f) return Vector3.forward;
            desiredDir.Normalize();
            float probeR = _col != null ? Mathf.Clamp(_col.radius * 0.62f, 0.28f, 0.85f) : 0.42f;
            float probeD = _col != null ? Mathf.Clamp(_col.radius * 2.0f + 1.35f, 2.2f, 4.0f) : 2.6f;
            if (!IsBlockedDual(desiredDir, probeR, probeD, targetPos, out _)) return desiredDir;
            if (Time.time >= _steerCommitUntil) _steerSideBias = (GetInstanceID() & 1) == 0 ? 1f : -1f;

            float bestClear = -1f; Vector3 bestDir = desiredDir; float chosenSign = _steerSideBias;
            for (int m = 0; m < SteerMagnitudes.Length; m++)
            {
                for (int s = 0; s < 2; s++)
                {
                    float sign = s == 0 ? _steerSideBias : -_steerSideBias;
                    Vector3 cand = Quaternion.Euler(0f, SteerMagnitudes[m] * sign, 0f) * desiredDir;
                    if (!IsBlockedDual(cand, probeR, probeD, targetPos, out float hitDist))
                    { _steerSideBias = sign; _steerCommitUntil = Time.time + 0.65f; return cand; }
                    if (hitDist > bestClear) { bestClear = hitDist; bestDir = cand; chosenSign = sign; }
                }
            }
            _steerSideBias = chosenSign; _steerCommitUntil = Time.time + 0.45f;
            return bestDir;
        }

        private bool IsBlockedDual(Vector3 dir, float radius, float maxDist, Vector3 targetPos, out float hitDistance)
        {
            float lowY = Mathf.Clamp(radius + 0.06f, 0.34f, 0.50f);
            float midY = Mathf.Clamp(radius + 0.55f, 0.82f, 1.08f);
            return IsBlocked(transform.position + Vector3.up * lowY, dir, radius, maxDist, targetPos, out hitDistance)
                || IsBlocked(transform.position + Vector3.up * midY, dir, radius, maxDist, targetPos, out hitDistance);
        }

        private bool IsBlocked(Vector3 origin, Vector3 dir, float radius, float maxDist, Vector3 targetPos, out float hitDistance)
        {
            hitDistance = maxDist;
            float effDist = Mathf.Min(maxDist, Mathf.Max(0.75f, Vector3.Distance(origin, targetPos) - 0.55f));
            if (!Physics.SphereCast(origin, radius, dir, out RaycastHit hit, effDist, ~0, QueryTriggerInteraction.Ignore)) return false;
            Collider c = hit.collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) return false;
            if (c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInParent<CaveCreatureBase>() != null) return false;
            if (c.GetComponentInParent<ResourceNodeController>() == null && hit.normal.y > 0.62f) return false;
            hitDistance = hit.distance; return true;
        }

        public static Vector3 ComputeCaveGuidedDirection(Vector3 from, Vector3 to)
        {
            Vector3 direct = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float distZ = Mathf.Abs(direct.z);
            if (distZ > 12.0f)
            {
                float aheadZ = from.z + Mathf.Sign(direct.z) * Mathf.Min(10.0f, distZ * 0.5f);
                float sx = CaveDungeonBuilder.GetSpineX(aheadZ);
                CaveDungeonBuilder.GetWallWidthsAtZ(aheadZ, out float lw, out float rw);
                float clampedX = Mathf.Clamp(Mathf.Lerp(from.x, to.x, 0.45f), sx - (lw - 2.4f), sx + (rw - 2.4f));
                Vector3 steered = new Vector3(clampedX - from.x, 0f, aheadZ - from.z);
                if (steered.sqrMagnitude > 0.04f) return steered.normalized;
            }
            return direct.sqrMagnitude > 0.0001f ? direct.normalized : Vector3.forward;
        }

        protected void StopHorizontalMovement() { if (_rb != null && !_rb.isKinematic) _rb.linearVelocity = Time.time < _knockbackUntil ? new Vector3(_knockbackVel.x, _rb.linearVelocity.y, _knockbackVel.z) : new Vector3(0f, _rb.linearVelocity.y, 0f); }
        protected void FaceDirection(Vector3 dir, float speed)
        {
            dir.y = 0f; if (dir.sqrMagnitude <= 0.001f || speed <= 0.01f) return;
            Quaternion next = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * Mathf.Min(speed, 7.5f));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, next, Mathf.Clamp(speed * 26f, 85f, 220f) * Time.deltaTime);
        }
        protected void PlaceOnCaveFloor(Vector3 desiredPos)
        {
            float baseFloor = CaveDungeonBuilder.GetFloorY(desiredPos.x, desiredPos.z);
            float groundY = UnitySafeUtility.ProbeGroundY(new Vector3(desiredPos.x, baseFloor, desiredPos.z), transform, 1.8f, 12.0f);
            Vector3 finalPos = new Vector3(desiredPos.x, groundY + 0.05f, desiredPos.z);
            transform.position = finalPos; if (_rb != null) _rb.position = finalPos;
        }
        protected void SetAnimatorSpeed(float speed) { if (_animator != null && !Mathf.Approximately(_animator.speed, speed)) _animator.speed = speed; OnAnimatorSpeedChanged(speed); }

        protected virtual void PlayGuardedAnim(string stateName, float fade = 0.12f, bool allowWhenDead = false, bool forceRestart = false)
        {
            if ((_isDead && !allowWhenDead) || _animator == null) return;
            if (!forceRestart && _activeAnimKey == stateName)
            {
                if (!_isAttacking && _animator.runtimeAnimatorController != null && !_animator.IsInTransition(0) && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.96f)
                    _animator.CrossFadeInFixedTime(stateName, 0.06f, 0, 0f);
                return;
            }
            _activeAnimKey = stateName;
            if (_animator.runtimeAnimatorController != null) _animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
        }

        public bool HasWallBetween(Vector3 targetWorldPos)
        {
            Vector3 from = transform.position + Vector3.up * 0.75f, to = targetWorldPos + Vector3.up * 0.85f;
            Vector3 d = to - from; float dist = d.magnitude;
            if (dist < 0.15f) return false;
            RaycastHit[] hits = Physics.RaycastAll(from, d / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<IDestructibleTunnelWall>() != null) return true;
                if (c.GetComponentInParent<CaveCreatureBase>() != null || c.GetComponentInParent<PlayerHealthController>() != null || c.GetComponentInParent<PlayerMovement>() != null) continue;
                if (hits[i].normal.y < 0.45f) return true;
            }
            return false;
        }

        protected bool TryDealConeDamageToPlayer(float maxReach, float halfConeAngle, float closePointBlank, int dmg)
        {
            if (!IsAlive || _playerTransform == null || GameStateController.State == GameState.Victory || GameLoopController.IsEndScreenOpen) return false;
            float maxVerticalReach = _col != null ? Mathf.Clamp(_col.height * 0.85f, 1.35f, 2.60f) : 1.55f;
            if (Mathf.Abs(_playerTransform.position.y - transform.position.y) > maxVerticalReach) return false;
            Vector3 diff = new Vector3(_playerTransform.position.x - transform.position.x, 0f, _playerTransform.position.z - transform.position.z);
            float dist = diff.magnitude;
            if (dist > maxReach || (dist > closePointBlank && Vector3.Angle(transform.forward, diff.normalized) > halfConeAngle) || HasWallBetween(_playerTransform.position)) return false;
            var ph = _playerTransform.GetComponentInParent<PlayerHealthController>();
            if (ph == null || !ph.IsAlive) return false;
            ph.TakeDamageFrom(dmg, this, false); return true;
        }

        public static Transform FindClosestLivingPlayer(Vector3 fromPos)
        {
            Transform best = null; float bestSq = float.MaxValue;
            var list = PlayerHealthController.AllPlayers;
            for (int i = 0; i < list.Count; i++) { var p = list[i]; if (p != null && p.IsAlive && p.gameObject.activeInHierarchy) { float sq = (p.transform.position - fromPos).sqrMagnitude; if (sq < bestSq) { bestSq = sq; best = p.transform; } } }
            return best;
        }
        protected static T LoadCreatureAsset<T>(string editorPath, string resourcePath) where T : UnityEngine.Object
        {
            T res = UnityEngine.Resources.Load<T>(resourcePath); if (res != null) return res;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(editorPath);
#else
            return null;
#endif
        }
    }
}
