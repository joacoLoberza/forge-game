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
        private string SelectNextTacticalMeleeAnim(float dist, float angleToTarget)
        {
            bool canGroundSlam = dist <= attackRange * 0.92f && _meleeSwingsSinceSlam >= 2 && Time.time >= _nextGroundSlamCooldown;
            if (canGroundSlam && Random.value < 0.62f)
            {
                _meleeSwingsSinceSlam = 0; _nextGroundSlamCooldown = Time.time + 8.5f;
                return _lastUsedAttackAnim = "attack3";
            }

            string[] pool = angleToTarget > 52f
                ? new[] { "attack4", "attack6", "attack4", "attack7" }
                : new[] { "attack6", "attack7", "attack6", "attack4", "attack7" };

            string pick = pool[Random.Range(0, pool.Length)];
            if (pick == _lastUsedAttackAnim)
                pick = pick == "attack6" ? "attack7" : (pick == "attack7" ? "attack4" : "attack6");

            _meleeSwingsSinceSlam++;
            return _lastUsedAttackAnim = pick;
        }

        private static bool IsArmoredGolemAnim(string anim) => anim == "attack3" || anim == "attack4";

        private static void GetGolemAttackProfile(string anim, out float impactClipTime, out float totalClipTime)
        {
            if (anim == "attack3") { impactClipTime = 0.68f; totalClipTime = 1.40f; }      // Golpe 2 manos al suelo + Onda Expansiva (Armor)
            else if (anim == "attack4") { impactClipTime = 0.82f; totalClipTime = 1.70f; } // Barrido amplio horizontal (Armor)
            else if (anim == "attack7") { impactClipTime = 0.62f; totalClipTime = 1.32f; } // Machaque vertical + Fisura lineal de rocas
            else { impactClipTime = 0.56f; totalClipTime = 1.18f; }                        // attack6: Puñetazo frontal rapido
        }

        private void ExecuteBoulderThrowAttack()
        {
            _isAttacking = true; _isChargingAttack = true; _isArmoredWindup = false; _stateEndTime = Time.time + 3.0f;
            SetAnimatorSpeed(0.62f); PlayGuardedAnim("roar", 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, 1.00f, "BOULDER THROW", attackRange, isArmored: false);
            StartCoroutine(BoulderThrowRoutine());
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

        private IEnumerator BoulderThrowRoutine()
        {
            yield return WaitGolemClipTarget(0.55f, 1.25f, trackPlayer: true);
            if (!IsAlive || IsStunned || !_isAttacking) { _isChargingAttack = _isArmoredWindup = false; yield break; }
            _isChargingAttack = false;
            PlayerCameraFollow.Instance?.TriggerImpulse(0.14f);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Attack);
            if (_playerTransform != null)
                GolemBoulderProjectile.Launch(transform.position + transform.forward * 1.15f + Vector3.up * 1.75f, _playerTransform.position, Mathf.RoundToInt(attackDamage * 1.35f), GetGolemVeinCrystalColor(), this);
            SetAnimatorSpeed(0.90f); yield return WaitGolemClipTarget(1.15f, 0.85f);
            SetAnimatorSpeed(1.0f); _isAttacking = _isArmoredWindup = false; _nextAttackCooldown = Time.time + 0.95f;
        }

        private IEnumerator SpawnGolemRockSpikesRoutine(Vector3 origin, Vector3 dir, int dmg)
        {
            var rockMat = ForjaGame.Environment.CaveDungeonBuilder.GetCaveRockMaterial();
            for (int step = 0; step < 4; step++)
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
                Destroy(spike, 0.75f); yield return new WaitForSeconds(0.14f);
            }
        }

        private void ExecuteTacticalGolemAttack(string anim)
        {
            bool armored = IsArmoredGolemAnim(anim);
            _isAttacking = true; _isChargingAttack = true; _isArmoredWindup = armored;
            GetGolemAttackProfile(anim, out float impactClip, out float _);
            float windSpd = armored ? 0.44f : 0.68f;
            float estWindup = (impactClip * 0.68f) / windSpd + (armored ? 0.22f : 0.08f);
            _stateEndTime = Time.time + 3.8f;
            SetAnimatorSpeed(windSpd);
            PlayGuardedAnim(anim, 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
            EnemyAttackTelegraph.Begin(this, estWindup, armored ? "ARMORED SMASH" : "GOLEM STRIKE", attackRange + 0.5f, isArmored: armored);
            StartCoroutine(DelayedGolemStrikeSequence(anim, armored, windSpd));
        }

        private IEnumerator DelayedGolemStrikeSequence(string anim, bool armored, float windSpd)
        {
            GetGolemAttackProfile(anim, out float impactClip, out float totalClip);
            SetAnimatorSpeed(windSpd);
            yield return WaitGolemClipTarget(impactClip * 0.55f, 1.4f, trackPlayer: true);
            if (!IsAlive || IsStunned || !_isAttacking) { _isChargingAttack = _isArmoredWindup = false; yield break; }
            yield return WaitGolemClipTarget(impactClip * 0.72f, 1.0f);
            if (!IsAlive || IsStunned || !_isAttacking) { _isChargingAttack = _isArmoredWindup = false; yield break; }

            SetAnimatorSpeed(armored ? 0.34f : 0.50f);
            yield return new WaitForSeconds(armored ? 0.22f : 0.08f);
            _isChargingAttack = false;
            if (!IsAlive || IsStunned || !_isAttacking) { _isArmoredWindup = false; yield break; }

            SetAnimatorSpeed(1.12f);
            if (_rb != null && !_rb.isKinematic)
            {
                float lunge = (anim == "attack3" ? 1.4f : 2.5f) * visualScaleMultiplier;
                _rb.linearVelocity = new Vector3(transform.forward.x * lunge, Mathf.Min(0f, _rb.linearVelocity.y), transform.forward.z * lunge);
            }
            yield return WaitGolemClipTarget(impactClip, 0.9f);
            if (!IsAlive || IsStunned || !_isAttacking) { _isArmoredWindup = false; yield break; }

            ApplyGolemStrikeHit(anim);
            SetAnimatorSpeed(0.88f);
            yield return WaitGolemClipTarget(totalClip, 1.4f);

            SetAnimatorSpeed(1.0f);
            _isAttacking = _isArmoredWindup = false;
            _nextAttackCooldown = Time.time + 0.85f;
        }

        private void ApplyGolemStrikeHit(string attackType)
        {
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Attack);
            EnemyAttackTelegraph.EmitImpactDust(transform.position + transform.forward * (attackRange * 0.65f), transform.forward, 6, 0.75f);

            if (attackType == "attack3") // Mapeo 1:1 -> Golpe 2 manos: Onda expansiva AoE + impacto radial
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.24f);
                ApplyRadialGroundShockwave(attackRange * 1.08f, Mathf.RoundToInt(attackDamage * 1.55f));
                TitanArenaHazardBuilder.SpawnGolemArmoredShockwave(transform.position + transform.forward * 0.85f, attackRange * 1.42f, Mathf.RoundToInt(attackDamage * 0.65f), this);
            }
            else if (attackType == "attack4") // Mapeo 1:1 -> Barrido horizontal amplio con armadura
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.15f);
                TryDealConeDamageToPlayer(attackRange * 1.25f, 145f, 2.2f * visualScaleMultiplier, Mathf.RoundToInt(attackDamage * 1.40f));
                DamageNearbyGolemsInArc(attackRange * 1.25f, 145f, Mathf.RoundToInt(attackDamage * 0.85f));
            }
            else if (attackType == "attack7") // Mapeo 1:1 -> Machaque vertical: fisura lineal de rocas hacia adelante
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.16f);
                Vector3 fDir = transform.forward; fDir.y = 0f; fDir = fDir.sqrMagnitude > 0.01f ? fDir.normalized : Vector3.forward;
                TryDealConeDamageToPlayer(attackRange + 0.65f, 68f, 1.7f * visualScaleMultiplier, Mathf.RoundToInt(attackDamage * 1.15f));
                StartCoroutine(SpawnGolemRockSpikesRoutine(transform.position, fDir, Mathf.RoundToInt(attackDamage * 1.25f)));
            }
            else // Mapeo 1:1 -> attack6: Puñetazo frontal rapido e interrumpible
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.10f);
                TryDealConeDamageToPlayer(attackRange + 0.70f, 65f, 1.75f * visualScaleMultiplier, attackDamage);
                DamageNearbyGolemsInArc(attackRange + 0.70f, 65f, Mathf.RoundToInt(attackDamage * 0.90f));
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
                var g = golems[i]; if (g == null || g == this || !g.IsAlive) continue;
                Vector3 toG = g.transform.position - transform.position; toG.y = 0f;
                if (toG.magnitude <= reach && Vector3.Angle(transform.forward, toG.normalized) <= arc * 0.5f)
                { g.TakeDamage(dmg); g.TriggerGolemRetaliation(transform); }
            }
        }

        private void ApplyRadialGroundShockwave(float radius, int dmg)
        {
            foreach (var p in PlayerHealthController.AllPlayers)
            {
                if (p == null || !p.IsAlive || HasWallBetween(p.transform.position)) continue;
                Vector3 diff = p.transform.position - transform.position; if (Mathf.Abs(diff.y) > 2.5f) continue;
                diff.y = 0f;
                if (diff.sqrMagnitude <= radius * radius)
                {
                    p.TakeDamageFrom(dmg, this, false);
                    if (p.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                    { Vector3 kb = diff.sqrMagnitude > 0.01f ? diff.normalized : transform.forward; rb.linearVelocity = new Vector3(kb.x * 7.5f, 3.2f, kb.z * 7.5f); }
                }
            }
            for (int i = 0; i < ActiveGolems.Count; i++)
            {
                var g = ActiveGolems[i]; if (g == null || g == this || !g.IsAlive) continue;
                Vector3 d = g.transform.position - transform.position; if (Mathf.Abs(d.y) > 2.5f) continue;
                d.y = 0f;
                if (d.sqrMagnitude <= radius * radius)
                { g.TakeDamage(Mathf.RoundToInt(dmg * 0.75f)); g.TriggerGolemRetaliation(transform); }
            }
        }

        public void TriggerGolemRetaliation(Transform enemyAttacker)
        {
            if (!IsAlive || enemyAttacker == null || enemyAttacker == transform) return;
            _playerTransform = enemyAttacker; _ecoBrain.ForceHostileAlert(16.0f);
            if (!_isAttacking && !_hasRoaredThisEngagement && Time.time >= _nextRoarAllowedTime) BeginGolemRoar(1.25f, 0.14f);
        }

        public void TriggerWallDefenseRage(Transform threat)
        {
            if (!IsAlive) return;
            Transform valid = threat != null && CaveTargetVisibility.IsTargetAccessibleInCave(transform.position, threat)
                ? threat : CaveTargetVisibility.FindClosestCaveTarget(transform.position);
            if (valid == null) return;
            _playerTransform = valid; _ecoBrain.ForceHostileAlert(14.0f);
            FaceDirection(_playerTransform.position - transform.position, 8f);
            if (!_hasRoaredThisEngagement && Time.time >= _nextRoarAllowedTime) BeginGolemRoar(1.35f, 0.12f);
        }

        private void BeginGolemRoar(float duration, float impulse)
        {
            _hasRoaredThisEngagement = true; _isRoaring = true; _stateEndTime = Time.time + duration; _nextRoarAllowedTime = Time.time + 35f;
            StopHorizontalMovement(); SetAnimatorSpeed(1.0f);
            if (_playerTransform != null) FaceDirection(_playerTransform.position - transform.position, 8f);
            PlayGuardedAnim("roar", 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
            PlayerCameraFollow.Instance?.TriggerImpulse(impulse);
        }
    }
}

