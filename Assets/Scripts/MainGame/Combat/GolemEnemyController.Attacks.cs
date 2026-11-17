using System.Collections;
using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Repertorio marcial, zancadas físicas, shockwaves radiales, retaliación e infighting del Gólem.
    /// </summary>
    public partial class GolemEnemyController
    {
        private void ExecuteSequentialAttack()
        {
            string anim = _attackSequence[_currentAttackIndex];
            _currentAttackIndex = (_currentAttackIndex + 1) % _attackSequence.Length;
            ExecuteTacticalGolemAttack(anim);
        }

        private static void GetGolemAttackProfile(string anim, out float impactClipTime, out float totalClipTime)
        {
            if (anim == "attack3") { impactClipTime = 0.68f; totalClipTime = 1.40f; }
            else if (anim == "attack4") { impactClipTime = 0.82f; totalClipTime = 1.70f; }
            else if (anim == "attack6") { impactClipTime = 0.58f; totalClipTime = 1.20f; }
            else { impactClipTime = 0.55f; totalClipTime = 1.20f; }
        }

        private void ExecuteRockFissureAttack()
        {
            _isAttacking = true; _stateEndTime = Time.time + 3.2f;
            SetAnimatorSpeed(0.60f); PlayGuardedAnim("attack3", 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, 1.05f, "GOLEM FISSURE", attackRange);
            StartCoroutine(RockFissureRoutine());
        }

        private IEnumerator WaitGolemClipTarget(float targetClip, float maxWait, bool trackPlayer = false)
        {
            float elapsed = 0f;
            while (IsAlive && !IsStunned && _isAttacking && elapsed < maxWait)
            {
                if (GetPlayableClipTime(_currentClipPlayable, _animator, elapsed) >= targetClip) break;
                if (trackPlayer && _playerTransform != null) FaceDirection(_playerTransform.position - transform.position, 5.2f);
                elapsed += Time.deltaTime; yield return null;
            }
        }

        private IEnumerator RockFissureRoutine()
        {
            yield return WaitGolemClipTarget(0.48f, 1.5f, trackPlayer: true);
            if (!IsAlive || IsStunned || !_isAttacking) yield break;
            SetAnimatorSpeed(1.12f);
            yield return WaitGolemClipTarget(0.68f, 0.9f);
            if (!IsAlive || IsStunned || !_isAttacking) yield break;

            Vector3 dir = _playerTransform != null ? (_playerTransform.position - transform.position) : transform.forward;
            dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            PlayerCameraFollow.Instance?.TriggerImpulse(0.20f);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Attack);
            StartCoroutine(SpawnGolemRockSpikesRoutine(transform.position, dir, Mathf.RoundToInt(attackDamage * 0.90f)));
            SetAnimatorSpeed(0.88f);
            yield return WaitGolemClipTarget(1.40f, 1.2f);
            SetAnimatorSpeed(1.0f); _isAttacking = false; _nextAttackCooldown = Time.time + 1.05f;
        }

        private IEnumerator SpawnGolemRockSpikesRoutine(Vector3 origin, Vector3 dir, int dmg)
        {
            var rockMat = ForjaGame.Environment.CaveDungeonBuilder.GetCaveRockMaterial();
            for (int step = 0; step < 3; step++)
            {
                Vector3 p = origin + dir * (2.2f + step * 2.05f);
                Vector3 floorPos = new Vector3(p.x, ForjaGame.Environment.CaveDungeonBuilder.GetFloorY(p.x, p.z), p.z);
                EnemyAttackTelegraph.EmitImpactDust(floorPos, dir, 5, 0.45f);
                var spike = new GameObject($"GolemRockSpike_{step}", typeof(MeshFilter), typeof(MeshRenderer));
                spike.transform.SetPositionAndRotation(floorPos + Vector3.down * 1.4f, Quaternion.Euler(0f, step * 47f, 0f));
                spike.GetComponent<MeshFilter>().sharedMesh = ForjaGame.Resources.OreMeshGenerator.CreateCrystalSpireMesh(0.58f, 1.85f, 320 + step * 17);
                spike.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
                for (float u = 0f; u < 0.14f; u += Time.deltaTime) { spike.transform.position = Vector3.Lerp(floorPos + Vector3.down * 1.4f, floorPos, u / 0.14f); yield return null; }
                TitanArenaHazardBuilder.DamagePlayersInRadius(floorPos, 1.35f, 2.2f, dmg, 4.5f);
                Destroy(spike, 0.75f);
                yield return new WaitForSeconds(0.14f);
            }
        }

        private void ExecuteTacticalGolemAttack(string anim)
        {
            _isAttacking = true;
            GetGolemAttackProfile(anim, out float impactClip, out float _);
            float estWindup = (impactClip * 0.62f) / 0.62f + 0.10f + (impactClip * 0.38f) / 1.12f;
            _stateEndTime = Time.time + 3.2f;
            SetAnimatorSpeed(0.62f);
            PlayGuardedAnim(anim, 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, estWindup, anim == "attack3" ? "GOLEM SLAM" : "GOLEM SMASH", attackRange + 0.5f);
            StartCoroutine(DelayedGolemStrikeSequence(anim));
        }

        private IEnumerator DelayedGolemStrikeSequence(string anim)
        {
            GetGolemAttackProfile(anim, out float impactClip, out float totalClip);
            SetAnimatorSpeed(0.62f);
            yield return WaitGolemClipTarget(impactClip * 0.55f, 1.0f, trackPlayer: true);
            if (!IsAlive || IsStunned || !_isAttacking) yield break;
            yield return WaitGolemClipTarget(impactClip * 0.72f, 0.7f);
            if (!IsAlive || IsStunned || !_isAttacking) yield break;

            SetAnimatorSpeed(0.45f);
            yield return new WaitForSeconds(0.10f);
            if (!IsAlive || IsStunned || !_isAttacking) yield break;

            SetAnimatorSpeed(1.12f);
            if (_rb != null && !_rb.isKinematic)
            {
                float lunge = (anim == "attack3" ? 1.4f : 2.5f) * visualScaleMultiplier;
                _rb.linearVelocity = new Vector3(transform.forward.x * lunge, Mathf.Min(0f, _rb.linearVelocity.y), transform.forward.z * lunge);
            }
            yield return WaitGolemClipTarget(impactClip, 0.9f);
            if (!IsAlive || IsStunned || !_isAttacking) yield break;

            ApplyGolemStrikeHit(anim);
            SetAnimatorSpeed(0.88f);
            yield return WaitGolemClipTarget(totalClip, 1.4f);

            SetAnimatorSpeed(1.0f);
            _isAttacking = false;
            _nextAttackCooldown = Time.time + 0.85f;
        }

        private void ApplyGolemStrikeHit(string attackType)
        {
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Attack);
            EnemyAttackTelegraph.EmitImpactDust(transform.position + transform.forward * (attackRange * 0.65f), transform.forward, 6, 0.75f);

            if (attackType == "attack3")
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.24f);
                ApplyRadialGroundShockwave(attackRange * 1.35f, Mathf.RoundToInt(attackDamage * 1.25f));
            }
            else if (attackType == "attack4")
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.12f);
                TryDealConeDamageToPlayer(attackRange * 1.25f, 140f, 2.2f * visualScaleMultiplier, Mathf.RoundToInt(attackDamage * 1.1f));
                DamageNearbyGolemsInArc(attackRange * 1.25f, 140f, Mathf.RoundToInt(attackDamage * 0.85f));
            }
            else
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.10f);
                TryDealConeDamageToPlayer(attackRange + 0.8f, 75f, 1.8f * visualScaleMultiplier, attackDamage);
                DamageNearbyGolemsInArc(attackRange + 0.8f, 75f, Mathf.RoundToInt(attackDamage * 0.90f));
            }

            if (_playerTransform != null && (_playerTransform.position - transform.position).sqrMagnitude <= (attackRange + 1.2f) * (attackRange + 1.2f))
            {
                var sp = _playerTransform.GetComponentInParent<SpiderEnemyController>();
                if (sp != null && sp.IsAlive) sp.TakeDamage(attackDamage);
            }
        }

        private void DamageNearbyGolemsInArc(float reach, float arc, int dmg)
        {
            var golems = ActiveGolems;
            for (int i = 0; i < golems.Count; i++)
            {
                var g = golems[i];
                if (g == null || g == this || !g.IsAlive) continue;
                Vector3 toG = g.transform.position - transform.position; toG.y = 0f;
                if (toG.magnitude <= reach && Vector3.Angle(transform.forward, toG.normalized) <= arc * 0.5f)
                {
                    g.TakeDamage(dmg); g.TriggerGolemRetaliation(transform);
                }
            }
        }

        private void ApplyRadialGroundShockwave(float radius, int dmg)
        {
            foreach (var p in PlayerHealthController.AllPlayers)
            {
                if (p == null || !p.IsAlive || HasWallBetween(p.transform.position)) continue;
                Vector3 diff = p.transform.position - transform.position;
                if (Mathf.Abs(diff.y) > 2.5f) continue;
                diff.y = 0f;
                if (diff.sqrMagnitude <= radius * radius)
                {
                    p.TakeDamageFrom(dmg, this, false, unparriable: true);
                    if (p.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                    {
                        Vector3 kb = diff.sqrMagnitude > 0.01f ? diff.normalized : transform.forward;
                        rb.linearVelocity = new Vector3(kb.x * 7.5f, 3.2f, kb.z * 7.5f);
                    }
                }
            }
            var golems = ActiveGolems;
            for (int i = 0; i < golems.Count; i++)
            {
                var g = golems[i];
                if (g == null || g == this || !g.IsAlive) continue;
                Vector3 d = g.transform.position - transform.position; if (Mathf.Abs(d.y) > 2.5f) continue;
                d.y = 0f;
                if (d.sqrMagnitude <= radius * radius)
                {
                    g.TakeDamage(Mathf.RoundToInt(dmg * 0.75f)); g.TriggerGolemRetaliation(transform);
                }
            }
        }

        public void TriggerGolemRetaliation(Transform enemyAttacker)
        {
            if (!IsAlive || enemyAttacker == null || enemyAttacker == transform) return;
            _playerTransform = enemyAttacker; _ecoBrain.ForceHostileAlert(16.0f);
            if (!_isAttacking && !_hasRoaredThisEngagement && Time.time >= _nextRoarAllowedTime)
            {
                _hasRoaredThisEngagement = true; _isRoaring = true; _stateEndTime = Time.time + 1.25f;
                _nextRoarAllowedTime = Time.time + 35f;
                StopHorizontalMovement(); SetAnimatorSpeed(1.0f); FaceDirection(enemyAttacker.position - transform.position, 8f);
                PlayGuardedAnim("roar", 0.08f, forceRestart: true);
                ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
                PlayerCameraFollow.Instance?.TriggerImpulse(0.14f);
            }
        }

        public void TriggerWallDefenseRage(Transform threat)
        {
            if (!IsAlive) return;
            Transform valid = threat != null && CaveTargetVisibility.IsTargetAccessibleInCave(transform.position, threat)
                ? threat : CaveTargetVisibility.FindClosestCaveTarget(transform.position);
            if (valid == null) return;
            _playerTransform = valid;
            _ecoBrain.ForceHostileAlert(14.0f);
            if (_playerTransform != null) FaceDirection((_playerTransform.position - transform.position), 8f);
            if (!_hasRoaredThisEngagement && Time.time >= _nextRoarAllowedTime)
            {
                _hasRoaredThisEngagement = true; _isRoaring = true; _stateEndTime = Time.time + 1.35f;
                _nextRoarAllowedTime = Time.time + 35f;
                StopHorizontalMovement(); SetAnimatorSpeed(1.0f);
                ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
                PlayGuardedAnim("roar", 0.08f, forceRestart: true);
                PlayerCameraFollow.Instance?.TriggerImpulse(0.12f);
            }
        }
    }
}

