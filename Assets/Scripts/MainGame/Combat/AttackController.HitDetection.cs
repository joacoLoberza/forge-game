using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Resources;
using ForjaGame.Forge;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    public partial class AttackController
    {
        public float GetCurrentPhaseSpeedMultiplier()
        {
            if (!_isAttacking || _activeMove == null) return 1.0f;
            float elapsed = Time.time - _currentAttackStartTime;
            float p = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attackDuration));

            if (p <= _activeMove.anticipationEnd) return 0.85f;
            if (p <= _activeMove.hitApex) return 0.55f;
            return 0.80f;
        }

        public bool IsDirectionLocked()
        {
            if (!_isAttacking || _activeMove == null) return false;
            float elapsed = Time.time - _currentAttackStartTime;
            float p = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attackDuration));
            return p >= _activeMove.anticipationEnd && p <= _activeMove.hitApex;
        }

        public void ApplyWeaponStats(WeaponStats weapon)
        {
            if (weapon == null) { ResetToDefaultStats(); return; }
            _equippedWeapon = weapon;
            _currentAnalysis = WeaponAnalyzer.Analyze(weapon);
            _currentComboMoves = ComboCatalog.GetCombo(_currentAnalysis.archetype);
            _maxComboSteps = _currentAnalysis.archetype == WeaponArchetype.Pickaxe ? 1 : 5;

            damage = Mathf.Max(1, Mathf.RoundToInt(weapon.EffectiveDamage));
            attackRange = Mathf.Clamp(weapon.attackRange, 1.6f, 4.5f);
            float speedRatio = Mathf.Clamp(weapon.GetSpeedMultiplier(), 0.70f, 1.30f);
            weapon.attackSpeed = speedRatio;
            attackCooldown = 0.65f / speedRatio;
        }

        public void ResetToDefaultStats()
        {
            _equippedWeapon = null;
            _currentComboMoves = null;
            _activeMove = null;
            damage = 0;
            attackRange = 1.0f;
            attackCooldown = 0.5f;
            attackDuration = 0.75f;
            comboTransitionTime = 0.52f;
            _maxComboSteps = 0;
            _hasBufferedNextStrike = false;
            _isAttacking = false;
            _comboAvailableTime = -1f;
        }

        private void UpdateHitboxVisualizer()
        {
            if (_attackHitboxVisualizer == null) return;
            _attackHitboxVisualizer.IsVolumeActive = _isAttacking;
            if (_isAttacking)
            {
                GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize);
                _attackHitboxVisualizer.UpdateBox(localCenter, boxSize);
                if (Time.time > _hitBlackTime) _attackHitboxVisualizer.IsConnecting = false;
            }
        }

        private readonly List<Collider> _combinedHitBuffer = new();

        public void GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize)
        {
            if (HasPickaxeEquipped)
            {
                // Rango de golpe del pico intuitivo y realista cuerpo a cuerpo
                float pickaxeReach = 1.50f;
                boxSize = new Vector3(1.20f, 1.90f, pickaxeReach);
                localCenter = new Vector3(0f, -0.05f, pickaxeReach * 0.46f);
                return;
            }

            AttackMotionType motion = _activeMove != null ? _activeMove.motionType : AttackMotionType.HorizontalSlash;
            float range = Mathf.Max(1.35f, attackRange);
            float forwardDepth = Mathf.Max(1.55f, range * 1.18f);
            float lateralWidth = Mathf.Max(1.35f, range * 1.02f);
            float verticalHeight = 2.15f;

            switch (motion)
            {
                case AttackMotionType.Thrust:
                    boxSize = new Vector3(Mathf.Max(0.95f, lateralWidth * 0.65f), verticalHeight, forwardDepth + 0.35f);
                    localCenter = new Vector3(0f, -0.10f, (forwardDepth + 0.35f) * 0.44f);
                    break;
                case AttackMotionType.OverheadSlam:
                    boxSize = new Vector3(Mathf.Max(1.15f, lateralWidth * 0.78f), verticalHeight + 0.3f, forwardDepth);
                    localCenter = new Vector3(0f, -0.10f, forwardDepth * 0.44f);
                    break;
                default:
                    boxSize = new Vector3(lateralWidth, verticalHeight, forwardDepth);
                    localCenter = new Vector3(0f, -0.10f, forwardDepth * 0.44f);
                    break;
            }
        }

        private void PerformSweepHitDetection()
        {
            GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            _combinedHitBuffer.Clear();
            Collider[] arcHits = Physics.OverlapBox(worldCenter, boxSize * 0.5f, transform.rotation, targetLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < arcHits.Length; i++) _combinedHitBuffer.Add(arcHits[i]);
            GetEquipment()?.EdgeAligner?.AppendBladeHits(targetLayers, _combinedHitBuffer);
            bool connected = false;

            foreach (Collider target in _combinedHitBuffer)
            {
                if (target.gameObject == gameObject || target.transform.IsChildOf(transform)) continue;

                // Evitar daño entre jugadores aliados (Friendly Fire) salvo que esté habilitado explícitamente
                if (!allowFriendlyFire && target.GetComponentInParent<PlayerMovement>() != null) continue;

                IDamageable d = target.GetComponentInParent<IDamageable>() ?? target.GetComponentInChildren<IDamageable>();
                if (d == null || !d.IsAlive) continue;

                // Garantía: 1 solo impacto por objetivo por cada golpe
                if (!_hitTargetsThisSwing.Add(d)) continue;

                // El pico afecta a menas de recursos y a paredes de túnel, no a criaturas vivas
                if (_equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.Pickaxe && d is not ResourceNodeController && d is not IDestructibleTunnelWall)
                {
                    continue;
                }

                connected = true;
                bool isFinisher = _activeMove != null && _activeMove.IsFinisher;
                bool isBackCrit = Vector3.Dot(transform.forward, target.transform.forward) > 0.25f;
                float comboMult = (_activeMove != null ? _activeMove.damageMultiplier : (isFinisher ? 1.65f : 1.0f)) * (isBackCrit ? 1.30f : 1.0f);
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * comboMult));

                if (d is IDestructibleTunnelWall wall)
                    wall.TakeWeaponDamage(finalDamage, _equippedWeapon != null ? _equippedWeapon.weaponType : null, gameObject);
                else if (d is ResourceNodeController node)
                { node.TakeDamageFrom(finalDamage, gameObject); SpawnVisceralCombatImpact(target.ClosestPoint(worldCenter), true, HasPickaxeEquipped); TriggerImpulseOnPlayerCamera(HasPickaxeEquipped ? 0.055f : 0.025f); }
                else
                {
                    d.TakeDamage(finalDamage);
                    bool isStone = d is GolemEnemyController or TitanBossController;
                    bool heavyHit = isFinisher || isBackCrit || (_equippedWeapon != null && _equippedWeapon.GetCombatStance() != WeaponCombatStance.OneHanded);
                    SpawnVisceralCombatImpact(target.ClosestPoint(worldCenter), isStone, heavyHit);
                    ForjaGame.Audio.ForgeAudioSystem.PlayHitImpact(target.transform.position, isStone, heavyHit);
                }

                TriggerImpulseOnPlayerCamera(isFinisher ? 0.09f : 0.04f);

                // Activar Hit-Stop táctil para sensación de impacto contundente
                float hitStopDur = isFinisher ? 0.10f : (_activeMove != null ? _activeMove.hitStopDuration : 0.055f);
                TriggerHitStop(hitStopDur);

                // Retroceso físico (Knockback) en el objetivo
                Vector3 pushDir = (target.transform.position - transform.position);
                if (d is CaveCreatureBase creature) creature.ApplyKnockback(pushDir, isFinisher ? 9.5f : 5.2f);
                Rigidbody targetRb = target.attachedRigidbody;
                if (targetRb != null && !targetRb.isKinematic)
                {
                    pushDir.y = 0.15f;
                    targetRb.AddForce(pushDir.normalized * (isFinisher ? 10.0f : 5.5f), ForceMode.Impulse);
                }

                if (_equippedWeapon != null)
                {
                    _equippedWeapon.ConsumeSharpness(1.0f);
                    damage = Mathf.Max(1, Mathf.RoundToInt(_equippedWeapon.EffectiveDamage));
                }
            }

            if (_attackHitboxVisualizer != null && connected)
            {
                _attackHitboxVisualizer.IsConnecting = true;
                _hitBlackTime = Time.time + 0.35f;
            }
        }

        private void TriggerImpulseOnPlayerCamera(float intensity)
        {
            if (_movementCtrl != null && _movementCtrl.PlayerCamera != null)
            {
                var camFollow = _movementCtrl.PlayerCamera.GetComponent<PlayerCameraFollow>();
                if (camFollow != null)
                {
                    camFollow.TriggerImpulse(intensity);
                    return;
                }
            }
            PlayerCameraFollow.Instance?.TriggerImpulse(intensity);
        }

        public Vector3 GetPlayerAttackInputDirection()
        {
            Vector2 input = Vector2.zero;
            if (_movementCtrl != null && _movementCtrl.MoveInput.sqrMagnitude > 0.01f)
            {
                input = _movementCtrl.MoveInput;
            }
            else if (_playerInput == null)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    float x = 0f, y = 0f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                    input = new Vector2(x, y);
                }
            }

            if (input.sqrMagnitude < 0.01f) return transform.forward;

            Transform cam = (_movementCtrl != null && _movementCtrl.PlayerCamera != null)
                ? _movementCtrl.PlayerCamera.transform
                : (Camera.main != null ? Camera.main.transform : null);

            Vector3 camFwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRgt = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;

            Vector3 dir = (camFwd * input.y + camRgt * input.x).normalized;
            return dir.sqrMagnitude > 0.01f ? dir : transform.forward;
        }

        private void SetupAttackHitboxVisualizer()
        {
            GameObject atkObj = new GameObject("PlayerAttackHitbox");
            atkObj.transform.SetParent(transform, false);
            _attackHitboxVisualizer = atkObj.AddComponent<HitboxVisualizer>();
            _attackHitboxVisualizer.SetShape(HitboxShape.Box);
            _attackHitboxVisualizer.IsVolumeActive = false;
        }

        public void TriggerHitStop(float duration)
        {
            _hitStopUntil = Time.time + Mathf.Clamp(duration, 0.040f, 0.12f);
        }
    }
}
