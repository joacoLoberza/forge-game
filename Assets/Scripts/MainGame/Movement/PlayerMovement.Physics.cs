using UnityEngine;

namespace ForjaGame.Core
{
    public partial class PlayerMovement
    {
        private void FixedUpdate()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && !netObj.IsOwner) return;

            if (!_movementEnabled)
                return;

            if (_rb != null)
                _rb.angularVelocity = Vector3.zero;

            bool isMoving = _moveInput.sqrMagnitude > 0.01f;

            // Movimiento relativo al ángulo de la cámara propia del jugador (o Camera.main si no tiene)
            Transform cam = playerCamera != null ? playerCamera.transform : (Camera.main != null ? Camera.main.transform : null);
            Vector3 camFwd = Vector3.forward;
            Vector3 camRgt = Vector3.right;
            if (cam != null)
            {
                Vector3 projectedFwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                if (projectedFwd.sqrMagnitude > 0.001f) camFwd = projectedFwd.normalized;
                Vector3 projectedRgt = Vector3.ProjectOnPlane(cam.right, Vector3.up);
                if (projectedRgt.sqrMagnitude > 0.001f) camRgt = projectedRgt.normalized;
            }
            Vector3 moveDir = (camFwd * _moveInput.y + camRgt * _moveInput.x).normalized;

            // Modulación de velocidad por peso de arma forjada (inercia estilo Monster Hunter)
            float speedMultiplier = 1.0f;
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
                // Al atacar se tiene el 70% de la velocidad original para reposicionarse con agilidad
                float attackMoveSpeed = currentSpeed * 0.70f;
                Vector3 targetHVel = isMoving ? moveDir * attackMoveSpeed : Vector3.zero;
                Vector3 currentHVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                Vector3 blendedHVel = Vector3.Lerp(currentHVel, targetHVel, 10f * Time.fixedDeltaTime);
                float groundedY = Mathf.Min(0f, _rb.linearVelocity.y);
                _rb.linearVelocity = new Vector3(blendedHVel.x, groundedY, blendedHVel.z);

                if (isMoving && moveDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, (rotationSpeed * 0.80f) * Time.fixedDeltaTime);
                }
                return;
            }

            Vector3 velocityXZ = moveDir * currentSpeed;
            _rb.linearVelocity = new Vector3(velocityXZ.x, _rb.linearVelocity.y, velocityXZ.z);

            if (isMoving && moveDir.sqrMagnitude > 0.001f)
            {
                // Giro suave hacia la dirección de avance
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
            }
        }
    }
}
