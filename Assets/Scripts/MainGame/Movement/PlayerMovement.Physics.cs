using UnityEngine;

namespace ForjaGame.Core
{
    public partial class PlayerMovement
    {
        public Vector3 GetCurrentCameraRelativeMoveDir()
        {
            if (_moveInput.sqrMagnitude <= 0.01f) return Vector3.zero;
            Transform cam = playerCamera != null ? playerCamera.transform : (Camera.main != null ? Camera.main.transform : null);
            Vector3 camFwd = Vector3.forward, camRgt = Vector3.right;
            if (cam != null)
            {
                Vector3 pf = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                if (pf.sqrMagnitude > 0.001f) camFwd = pf.normalized;
                Vector3 pr = Vector3.ProjectOnPlane(cam.right, Vector3.up);
                if (pr.sqrMagnitude > 0.001f) camRgt = pr.normalized;
            }
            return (camFwd * _moveInput.y + camRgt * _moveInput.x).normalized;
        }

        private void FixedUpdate()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && !netObj.IsOwner) return;

            if (!_movementEnabled || _rb == null || _rb.isKinematic)
                return;

            _rb.angularVelocity = Vector3.zero;
            if (_rollCtrl == null) _rollCtrl = GetComponent<ForjaGame.Combat.PlayerRollController>();
            if (_rollCtrl != null && _rollCtrl.IsRolling) { _rollCtrl.ApplyRollFixedPhysics(_rb); return; }

            bool isMoving = _moveInput.sqrMagnitude > 0.01f;
            Vector3 moveDir = GetCurrentCameraRelativeMoveDir();
            if (isMoving && moveDir.sqrMagnitude > 0.001f) _originalWalkDir = moveDir;

            // Modulación de velocidad por peso de arma forjada (inercia estilo Monster Hunter)
            float speedMultiplier = 1.0f;
            bool isSlowed = Time.time < _slowUntilTime;
            if (isSlowed)
            {
                speedMultiplier *= _slowMultiplier;
                IsSprinting = false;
            }
            else
            {
                _slowMultiplier = 1.0f;
                if (animator != null && animator.speed < 0.95f)
                    animator.speed = 1.0f;
            }

            if (_equipmentCtrl == null) _equipmentCtrl = GetComponent<ForjaGame.Combat.PlayerEquipmentController>();
            if (_equipmentCtrl != null && _equipmentCtrl.HasWeaponEquipped && _equipmentCtrl.CurrentWeapon != null)
            {
                var stance = _equipmentCtrl.CurrentWeapon.GetCombatStance();
                if (stance == ForjaGame.Forge.WeaponCombatStance.TwoHanded)
                    speedMultiplier = 0.76f; // Mandoble / Gran Maza
                else if (stance == ForjaGame.Forge.WeaponCombatStance.Polearm)
                    speedMultiplier = 0.84f; // Asta / Alabarda
                else if (stance == ForjaGame.Forge.WeaponCombatStance.OneHanded)
                    speedMultiplier = 0.93f; // Espada / Hacha 1H
            }

            float currentSpeed = (IsSprinting ? runSpeed : walkSpeed) * speedMultiplier;

            if (_attackCtrl != null && _attackCtrl.IsAttacking)
            {
                // Sin deslizamiento artificial: al desplazarse sincronizamos con LocomotionBlend inferior y amortiguamos paso en reposo
                float attackMoveSpeed = currentSpeed * 0.88f;
                Vector3 targetHVel = isMoving ? moveDir * attackMoveSpeed : Vector3.zero;
                Vector3 currentHVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                float dampRate = isMoving ? 15f : 12f;
                Vector3 blendedHVel = Vector3.Lerp(currentHVel, targetHVel, dampRate * Time.fixedDeltaTime);
                float groundedY = Mathf.Min(0f, _rb.linearVelocity.y);
                _rb.linearVelocity = new Vector3(blendedHVel.x, groundedY, blendedHVel.z);

                if (isMoving && moveDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, (rotationSpeed * 0.85f) * Time.fixedDeltaTime);
                }
                return;
            }

            Vector3 velocityXZ = moveDir * currentSpeed;
            _rb.linearVelocity = new Vector3(velocityXZ.x, _rb.linearVelocity.y, velocityXZ.z);
            UpdateJumpPhysics();

            if (isMoving && moveDir.sqrMagnitude > 0.001f)
            {
                // Giro suave hacia la dirección de avance
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
            }
        }

        private float _slowMultiplier = 1.0f;
        private float _slowUntilTime = 0f;
        public bool IsWebSlowed => Time.time < _slowUntilTime;

        public void ApplySlowdown(float factor, float duration)
        {
            if (GameStateController.State == GameState.Victory || GameLoopController.IsEndScreenOpen) return;
            _slowMultiplier = Mathf.Clamp(factor, 0.30f, 0.65f);
            float safeDur = Mathf.Clamp(duration, 0.2f, 2.4f);
            _slowUntilTime = Mathf.Max(_slowUntilTime, Time.time + safeDur);
            if (animator != null) animator.speed = 0.70f;
        }
    }
}
