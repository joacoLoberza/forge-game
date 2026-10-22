using UnityEngine;

namespace ForjaGame.Core
{
    public partial class PlayerMovement
    {
        private void FixedUpdate()
        {
            if (!_movementEnabled)
                return;

            if (_rb != null)
                _rb.angularVelocity = Vector3.zero;

            bool isMoving = _moveInput.sqrMagnitude > 0.01f;

            // Movimiento relativo al ángulo de la cámara propia del jugador (o Camera.main si no tiene)
            Transform cam = playerCamera != null ? playerCamera.transform : (Camera.main != null ? Camera.main.transform : null);
            Vector3 camFwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRgt = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
            Vector3 moveDir = (camFwd * _moveInput.y + camRgt * _moveInput.x).normalized;

            if (_attackCtrl != null && _attackCtrl.IsAttacking)
            {
                // Amortiguación firme de la inercia/lunge durante el golpe manteniéndolo firmemente plantado al suelo
                Vector3 currentHVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                Vector3 dampedHVel = Vector3.Lerp(currentHVel, Vector3.zero, 8.5f * Time.fixedDeltaTime);
                float groundedY = Mathf.Min(0f, _rb.linearVelocity.y);
                _rb.linearVelocity = new Vector3(dampedHVel.x, groundedY, dampedHVel.z);
                return;
            }

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
