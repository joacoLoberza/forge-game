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

            if (p <= _activeMove.anticipationEnd) return 0.60f;
            if (p <= _activeMove.hitApex) return 0.25f;
            return 0.50f;
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
            _maxComboSteps = 5;

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

        public void GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize)
        {
            AttackMotionType motion = _activeMove != null ? _activeMove.motionType : AttackMotionType.HorizontalSlash;
            float range = Mathf.Max(1.6f, attackRange);

            switch (motion)
            {
                case AttackMotionType.Thrust:
                    boxSize = new Vector3(0.75f, 0.80f, range);
                    localCenter = new Vector3(0f, 0.90f, range * 0.5f);
                    break;
                case AttackMotionType.OverheadSlam:
                    boxSize = new Vector3(0.95f, 1.50f, range * 0.85f);
                    localCenter = new Vector3(0f, 0.75f, (range * 0.85f) * 0.5f);
                    break;
                default:
                    float lateralWidth = range * 1.40f;
                    float forwardDepth = range * 0.65f;
                    boxSize = new Vector3(lateralWidth, 0.90f, forwardDepth);
                    localCenter = new Vector3(0f, 0.95f, forwardDepth * 0.5f);
                    break;
            }
        }

        private void PerformSweepHitDetection()
        {
            GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 halfExtents = boxSize * 0.5f;
            Quaternion orientation = transform.rotation;

            Collider[] targets = Physics.OverlapBox(worldCenter, halfExtents, orientation, targetLayers, QueryTriggerInteraction.Ignore);
            bool connected = false;

            foreach (Collider target in targets)
            {
                if (target.gameObject == gameObject || target.transform.IsChildOf(transform)) continue;

                // Evitar daño entre jugadores aliados (Friendly Fire) salvo que esté habilitado explícitamente
                if (!allowFriendlyFire && target.GetComponentInParent<PlayerMovement>() != null) continue;

                IDamageable d = target.GetComponentInParent<IDamageable>() ?? target.GetComponentInChildren<IDamageable>();
                if (d == null || !d.IsAlive) continue;

                // Garantía: 1 solo impacto por objetivo por cada golpe
                if (!_hitTargetsThisSwing.Add(d)) continue;

                connected = true;
                bool isFinisher = _activeMove != null && _activeMove.IsFinisher;
                float comboMult = _activeMove != null ? _activeMove.damageMultiplier : (isFinisher ? 1.65f : 1.0f);
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * comboMult));

                if (d is ResourceNodeController node) node.TakeDamageFrom(finalDamage, gameObject);
                else d.TakeDamage(finalDamage);

                if (isFinisher) TriggerImpulseOnPlayerCamera(0.09f);
                else TriggerImpulseOnPlayerCamera(0.04f);

                // Activar Hit-Stop táctil para sensación de impacto contundente
                float hitStopDur = isFinisher ? 0.10f : (_activeMove != null ? _activeMove.hitStopDuration : 0.055f);
                TriggerHitStop(hitStopDur);

                // Retroceso físico (Knockback) en el objetivo
                Rigidbody targetRb = target.attachedRigidbody;
                if (targetRb != null && !targetRb.isKinematic)
                {
                    Vector3 pushDir = (target.transform.position - transform.position);
                    pushDir.y = 0.15f;
                    float pushForce = isFinisher ? 10.0f : 5.5f;
                    targetRb.AddForce(pushDir.normalized * pushForce, ForceMode.Impulse);
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
