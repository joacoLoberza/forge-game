using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Resources;
using ForjaGame.Forge;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    public partial class AttackController
    {
        private void UpdateHitboxVisualizer()
        {
            if (_attackHitboxVisualizer == null) return;

            _attackHitboxVisualizer.IsVolumeActive = _isAttacking;
            if (_isAttacking)
            {
                GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize);
                _attackHitboxVisualizer.UpdateBox(localCenter, boxSize);
                if (Time.time > _hitBlackTime)
                {
                    _attackHitboxVisualizer.IsConnecting = false;
                }
            }
        }

        public void GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize)
        {
            var style = _equippedWeapon != null ? _equippedWeapon.GetAnimationStyle() : WeaponAnimationStyle.Sword1H;
            AttackMotionType motion = WeaponComboAnimator.GetMotionType(style, _currentComboIndex);
            float range = Mathf.Max(1.6f, attackRange);

            switch (motion)
            {
                case AttackMotionType.Thrust:
                    // Estocada: Forma rectangular alargada desde el centro del personaje hasta el alcance máximo
                    boxSize = new Vector3(0.75f, 0.80f, range);
                    localCenter = new Vector3(0f, 0.90f, range * 0.5f);
                    break;

                case AttackMotionType.OverheadSlam:
                    // Aplastamiento vertical: Volumen vertical enfocado al frente
                    boxSize = new Vector3(0.95f, 1.50f, range * 0.85f);
                    localCenter = new Vector3(0f, 0.75f, (range * 0.85f) * 0.5f);
                    break;

                default: // AttackMotionType.HorizontalSlash
                    // Tajo horizontal: Mayor área lateral pero menor alcance frontal que la estocada
                    float lateralWidth = range * 1.40f;
                    float forwardDepth = range * 0.65f;
                    boxSize = new Vector3(lateralWidth, 0.90f, forwardDepth);
                    localCenter = new Vector3(0f, 0.95f, forwardDepth * 0.5f);
                    break;
            }
        }

        public void ApplyWeaponStats(WeaponStats weapon)
        {
            if (weapon == null) { ResetToDefaultStats(); return; }
            _equippedWeapon = weapon;
            damage = Mathf.Max(1, Mathf.RoundToInt(weapon.EffectiveDamage));
            attackRange = Mathf.Clamp(weapon.attackRange, 1.6f, 4.5f);
            float speedRatio = Mathf.Clamp(weapon.GetSpeedMultiplier(), 0.70f, 1.30f);
            weapon.attackSpeed = speedRatio;
            _maxComboSteps = Mathf.Clamp(weapon.MaxComboCount, 1, 5);
            float baseDuration = weapon.GetBaseAttackDuration();
            attackDuration = baseDuration / speedRatio;
            comboTransitionTime = (baseDuration * weapon.GetComboLinkRatio()) / speedRatio;
            attackCooldown = attackDuration;
        }

        public void ResetToDefaultStats()
        {
            _equippedWeapon = null;
            damage = 0;
            attackRange = 1.0f;
            attackCooldown = 0.5f;
            attackDuration = 0.75f;
            comboTransitionTime = 0.52f;
            _maxComboSteps = 0;
            _hasBufferedNextStrike = false;
            _isAttacking = false;
            _isCarryingAttackStep = false;
            _comboAvailableTime = -1f;
        }

        private void PerformHitDetection()
        {
            GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 halfExtents = boxSize * 0.5f;
            Quaternion orientation = transform.rotation;

            Collider[] targets = Physics.OverlapBox(worldCenter, halfExtents, orientation, targetLayers, QueryTriggerInteraction.Ignore);
            IDamageable closestTarget = null;
            float closestDist = float.PositiveInfinity;
            bool hitConnected = false;

            foreach (Collider target in targets)
            {
                if (target.gameObject == gameObject || target.transform.IsChildOf(transform)) continue;
                IDamageable d = target.GetComponentInParent<IDamageable>() ?? target.GetComponentInChildren<IDamageable>();
                if (d == null || !d.IsAlive) continue;

                hitConnected = true;
                Vector3 toTarget = target.bounds.center - worldCenter;
                float dist = toTarget.magnitude;
                if (dist < closestDist) { closestTarget = d; closestDist = dist; }
            }

            if (_attackHitboxVisualizer != null)
            {
                _attackHitboxVisualizer.IsConnecting = hitConnected;
                if (hitConnected)
                {
                    _hitBlackTime = Time.time + 0.35f;
                }
            }

            if (closestTarget != null)
            {
                bool isFinisher = _currentComboIndex >= Mathf.Max(1, _maxComboSteps - 1);
                float comboMult = isFinisher ? 1.65f : (1.0f + _currentComboIndex * 0.12f);
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * comboMult));
                if (closestTarget is ResourceNodeController node) node.TakeDamageFrom(finalDamage, gameObject);
                else closestTarget.TakeDamage(finalDamage);

                if (isFinisher) PlayerCameraFollow.Instance?.TriggerImpulse(0.08f);

                if (_equippedWeapon != null)
                {
                    _equippedWeapon.ConsumeSharpness(1.0f);
                    damage = Mathf.Max(1, Mathf.RoundToInt(_equippedWeapon.EffectiveDamage));
                }
            }
        }

        private bool HasMovementInput()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed);
        }

        public Vector3 GetPlayerAttackInputDirection()
        {
            var kb = Keyboard.current;
            if (kb == null) return transform.forward;

            float x = 0f, y = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;

            Vector2 input = new Vector2(x, y);
            if (input.sqrMagnitude < 0.01f) return transform.forward;

            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 camFwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRgt = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;

            Vector3 dir = (camFwd * input.y + camRgt * input.x).normalized;
            return dir.sqrMagnitude > 0.01f ? dir : transform.forward;
        }
    }
}
