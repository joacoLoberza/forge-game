using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Inventory;
using ForjaGame.Forge;

namespace ForjaGame.Core
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(InventoryController))]
    public partial class PlayerMovement : MonoBehaviour
    {
        public static PlayerMovement LocalInstance { get; set; }

        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.8f;
        [SerializeField] private float jumpForce = 3.2f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float groundCheckDistance = 1.15f;
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        [Header("Multiplayer & Camera")]
        [SerializeField] private int playerIndex = 0;
        [SerializeField] private Camera playerCamera;

        private Rigidbody _rb;
        private Vector2 _moveInput;
        private Vector2 _moveInputFromEvents;
        private bool _movementEnabled = true;
        private bool _isSprintingInput = false;
        private bool _sprintInputFromEvents = false;
        private Vector3 _prevRemotePos;

        private PlayerInput _playerInput;
        private ForjaGame.Combat.PlayerEquipmentController _equipmentCtrl;
        private ForjaGame.Combat.PlayerHealthController _healthCtrl;
        private ForjaGame.Combat.AttackController _attackCtrl;

        public int PlayerIndex
        {
            get => playerIndex;
            set => playerIndex = value;
        }

        public Camera PlayerCamera
        {
            get => playerCamera;
            set => playerCamera = value;
        }

        public Vector2 MoveInput => _moveInput;
        public bool IsSprinting { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;

        private void Awake()
        {
            if (LocalInstance == null) LocalInstance = this;
            _rb = GetComponent<Rigidbody>();
            if (jumpForce > 3.6f) jumpForce = 3.2f;
            if (_rb != null)
            {
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
                animator.applyRootMotion = false;

            _playerInput = GetComponent<PlayerInput>();

            _equipmentCtrl = GetComponent<ForjaGame.Combat.PlayerEquipmentController>();
            if (_equipmentCtrl == null)
                _equipmentCtrl = gameObject.AddComponent<ForjaGame.Combat.PlayerEquipmentController>();

            _healthCtrl = GetComponent<ForjaGame.Combat.PlayerHealthController>();
            if (_healthCtrl == null)
                _healthCtrl = gameObject.AddComponent<ForjaGame.Combat.PlayerHealthController>();

            _attackCtrl = GetComponent<ForjaGame.Combat.AttackController>();
        }

        private void OnDestroy()
        {
            if (LocalInstance == this) LocalInstance = null;
        }

        public void SetAnimator(Animator anim)
        {
            animator = anim;
            if (animator != null)
                animator.applyRootMotion = false;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            _moveInputFromEvents = context.ReadValue<Vector2>();
            if (_movementEnabled) _moveInput = _moveInputFromEvents;
        }

        public void OnMove(InputValue value)
        {
            _moveInputFromEvents = value.Get<Vector2>();
            if (_movementEnabled) _moveInput = _moveInputFromEvents;
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (!context.started || !_movementEnabled) return;
            if (IsGrounded()) _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed && _movementEnabled && IsGrounded())
                _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            _sprintInputFromEvents = context.ReadValueAsButton();
        }

        public void OnSprint(InputValue value)
        {
            _sprintInputFromEvents = value.isPressed;
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
                _isSprintingInput = false;
                _sprintInputFromEvents = false;
                if (_rb != null)
                {
                    _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                    _rb.angularVelocity = Vector3.zero;
                }
            }
        }

        private void Update()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && !netObj.IsOwner)
            {
                Vector3 disp = transform.position - _prevRemotePos;
                disp.y = 0f;
                float netSpeed = disp.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
                _prevRemotePos = transform.position;

                if (animator == null) animator = GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    float remoteSpeed = netSpeed > 0.15f ? (netSpeed > 5.5f ? 2.0f : 1.0f) : 0f;
                    animator.SetFloat("Speed", remoteSpeed, 0.1f, Time.deltaTime);
                }
                return;
            }
            _prevRemotePos = transform.position;

            if (_healthCtrl == null) _healthCtrl = GetComponent<ForjaGame.Combat.PlayerHealthController>();
            if (_healthCtrl != null && !_healthCtrl.IsAlive)
            {
                _moveInput = Vector2.zero;
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }

            if (_movementEnabled)
            {
                Vector2 input = _moveInputFromEvents;
                var kb = Keyboard.current;
                bool kbShift = false;
                if (kb != null)
                {
                    float x = 0f, y = 0f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;

                    Vector2 direct = new Vector2(x, y);
                    if (direct.sqrMagnitude > 0.01f) input = direct.normalized;

                    kbShift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                    if (kb.spaceKey.wasPressedThisFrame && IsGrounded() && _rb != null)
                        _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                }

                var gp = Gamepad.current;
                bool gpSprint = gp != null && (gp.leftStickButton.isPressed || gp.rightTrigger.isPressed);

                _isSprintingInput = kbShift || gpSprint || _sprintInputFromEvents;
                _moveInput = input;
            }
            else
            {
                _moveInput = Vector2.zero;
                _isSprintingInput = false;
            }

            if (transform.position.y < -14.0f && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
            {
                transform.position = new Vector3(playerIndex * 3.0f, 1.2f, 0f);
                if (_rb != null) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
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
            IsSprinting = isMoving && _isSprintingInput && !isAttacking;

            float targetSpeed = !isMoving ? 0f : (isAttacking ? 0.75f : (IsSprinting ? 2.0f : 1.0f));
            float dampTime = !isMoving ? 0.08f : 0.15f;
            animator.SetFloat("Speed", targetSpeed, dampTime, Time.deltaTime);
        }
    }
}