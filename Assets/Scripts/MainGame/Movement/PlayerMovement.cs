using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Inventory;
using ForjaGame.Forge;

namespace ForjaGame.Core
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(InventoryController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.8f;
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float groundCheckDistance = 1.15f;
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        private Rigidbody _rb;
        private Vector2 _moveInput;
        private bool _movementEnabled = true;

        public bool IsSprinting { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            // Desactivar Root Motion para evitar saltos o teletransporte al girar
            if (animator != null)
                animator.applyRootMotion = false;

            if (GetComponent<ForjaGame.Combat.PlayerEquipmentController>() == null)
                gameObject.AddComponent<ForjaGame.Combat.PlayerEquipmentController>();

            if (GetComponent<ForjaGame.Combat.PlayerHealthController>() == null)
                gameObject.AddComponent<ForjaGame.Combat.PlayerHealthController>();
        }

        public void SetAnimator(Animator anim)
        {
            animator = anim;
            if (animator != null)
                animator.applyRootMotion = false;
        }

        private Vector2 _moveInputFromEvents;

        public void OnMove(InputAction.CallbackContext context)
        {
            _moveInputFromEvents = context.ReadValue<Vector2>();
            if (_movementEnabled && _moveInputFromEvents.sqrMagnitude > 0.01f)
            {
                _moveInput = _moveInputFromEvents;
            }
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (!context.started || !_movementEnabled)
                return;

            if (IsGrounded())
            {
                _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);
        }

        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;

            if (!enabled)
            {
                _moveInput = Vector2.zero;
                _moveInputFromEvents = Vector2.zero;
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                _rb.angularVelocity = Vector3.zero;
            }
        }

        private ForjaGame.Combat.PlayerHealthController _healthCtrl;

        private void Update()
        {
            if (_healthCtrl == null) _healthCtrl = GetComponent<ForjaGame.Combat.PlayerHealthController>();
            if (_healthCtrl != null && !_healthCtrl.IsAlive)
            {
                _moveInput = Vector2.zero;
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }

            if (_movementEnabled)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    float x = 0f;
                    float y = 0f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;

                    Vector2 directInput = new Vector2(x, y);
                    if (directInput.sqrMagnitude > 0.01f)
                        _moveInput = directInput.normalized;
                    else if (_moveInputFromEvents.sqrMagnitude > 0.01f)
                        _moveInput = _moveInputFromEvents.normalized;
                    else
                        _moveInput = Vector2.zero;

                    if (kb.spaceKey.wasPressedThisFrame && IsGrounded())
                    {
                        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                    }
                }
                else if (_moveInputFromEvents.sqrMagnitude > 0.01f)
                {
                    _moveInput = _moveInputFromEvents.normalized;
                }
                else
                {
                    _moveInput = Vector2.zero;
                }
            }
            else
            {
                _moveInput = Vector2.zero;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
                if (animator != null) animator.applyRootMotion = false;
            }
            if (animator == null)
                return;

            if (_attackCtrl == null)
                _attackCtrl = GetComponent<ForjaGame.Combat.AttackController>();

            bool isAttacking = _attackCtrl != null && _attackCtrl.IsAttacking;
            bool isMoving = _movementEnabled && _moveInput.sqrMagnitude > 0.01f;
            bool isShift = Keyboard.current != null &&
                          (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
            IsSprinting = isMoving && isShift && !isAttacking;

            float targetSpeed = !isMoving ? 0f : (isAttacking ? 0.75f : (isShift ? 2.0f : 1.0f));
            float dampTime = !isMoving ? 0.08f : 0.15f;
            animator.SetFloat("Speed", targetSpeed, dampTime, Time.deltaTime);
        }

        private ForjaGame.Combat.AttackController _attackCtrl;

        private void FixedUpdate()
        {
            if (!_movementEnabled)
                return;

            if (_rb != null)
                _rb.angularVelocity = Vector3.zero;

            bool isMoving = _moveInput.sqrMagnitude > 0.01f;

            // Movimiento relativo al ángulo de la cámara (estilo Monster Hunter)
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 camFwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRgt = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
            Vector3 moveDir = (camFwd * _moveInput.y + camRgt * _moveInput.x).normalized;

            if (_attackCtrl != null && _attackCtrl.IsAttacking)
            {
                float phaseMult = _attackCtrl.GetCurrentPhaseSpeedMultiplier();
                bool dirLocked = _attackCtrl.IsDirectionLocked();

                if (!dirLocked && isMoving && moveDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 8.0f * Time.fixedDeltaTime);
                }

                float combatMoveSpeed = walkSpeed * phaseMult;
                Vector3 combatVel = (isMoving && moveDir.sqrMagnitude > 0.001f) ? (moveDir * combatMoveSpeed) : Vector3.zero;

                if (dirLocked && _attackCtrl.ActiveMove != null)
                {
                    combatVel += transform.forward * (_attackCtrl.ActiveMove.lungeDistance * 2.5f);
                }

                _rb.linearVelocity = new Vector3(combatVel.x, _rb.linearVelocity.y, combatVel.z);
                return;
            }
            IsSprinting = isMoving && Keyboard.current != null &&
                          (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

            // Modulación de velocidad por peso de arma forjada (inercia estilo Monster Hunter)
            float speedMultiplier = 1.0f;
            var equipment = ForjaGame.Combat.PlayerEquipmentController.Instance;
            if (equipment != null && equipment.HasWeaponEquipped && equipment.CurrentWeapon != null)
            {
                var stance = equipment.CurrentWeapon.GetCombatStance();
                if (stance == ForjaGame.Forge.WeaponCombatStance.TwoHanded)
                    speedMultiplier = 0.76f; // Mandoble / Gran Maza: 3.4 walk, 5.9 run
                else if (stance == ForjaGame.Forge.WeaponCombatStance.Polearm)
                    speedMultiplier = 0.84f; // Asta / Alabarda: 3.7 walk, 6.5 run
                else if (stance == ForjaGame.Forge.WeaponCombatStance.OneHanded)
                    speedMultiplier = 0.93f; // Espada / Hacha 1H: 4.1 walk, 7.2 run
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