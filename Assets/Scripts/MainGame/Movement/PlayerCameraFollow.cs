using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Forge;

namespace ForjaGame.Core
{
    /// <summary>
    /// Cámara orbital en tercera persona inspirada en Monster Hunter.
    /// Soporta múltiples instancias para Split-Screen / Multijugador local.
    /// Lee rotación desde Ratón y Stick Derecho de Gamepad.
    /// </summary>
    public partial class PlayerCameraFollow : MonoBehaviour
    {
        public static PlayerCameraFollow Instance { get; private set; }

        [Header("Target Configuration")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerMovement targetPlayer;
        [SerializeField] private float targetHeight = 2.05f;
        [SerializeField] private float distance = 6.2f;
        [SerializeField] private float shoulderOffset = 0.45f;

        [Header("Orbit & Sensitivity")]
        [SerializeField] private float sensitivityX = 0.16f;
        [SerializeField] private float sensitivityY = 0.14f;
        [SerializeField] private float gamepadSensitivity = 120f;
        [SerializeField] private float minPitch = -18f;
        [SerializeField] private float maxPitch = 62f;
        [SerializeField] private float smoothSpeed = 14f;

        [Header("Collision Avoidance")]
        [SerializeField] private LayerMask collisionLayers = ~0;
        [SerializeField] private float collisionRadius = 0.28f;

        private float _yaw;
        private float _pitch = 18f;
        private bool _isOrbitEnabled = true;

        private bool _isStationViewActive;
        private Vector3 _stationTargetPos;
        private Quaternion _stationTargetRot;
        private float _stationTransitionSpeed = 8.5f;
        private Vector2 _lookInput;
        private float _shakeIntensity = 0f;

        public Transform Target { get => target; set => target = value; }
        public PlayerMovement TargetPlayer
        {
            get => targetPlayer;
            set
            {
                targetPlayer = value;
                if (targetPlayer != null) { target = targetPlayer.transform; targetPlayer.PlayerCamera = GetComponent<Camera>(); }
            }
        }
        public float TargetHeight { get => targetHeight; set => targetHeight = value; }
        public float Distance { get => distance; set => distance = value; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (targetPlayer == null && target != null) targetPlayer = target.GetComponent<PlayerMovement>();
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            _yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            if (targetPlayer != null && targetPlayer.PlayerCamera == null)
                targetPlayer.PlayerCamera = GetComponent<Camera>();
        }

        public void OnLook(InputAction.CallbackContext context) => _lookInput = context.ReadValue<Vector2>();

        private void Update()
        {
            if (target == null)
            {
                if (targetPlayer != null) target = targetPlayer.transform;
                else
                {
                    var local = PlayerMovement.LocalInstance ?? FindFirstObjectByType<PlayerMovement>();
                    if (local != null)
                    {
                        targetPlayer = local;
                        target = local.transform;
                    }
                    else return;
                }
            }

            bool inStation = _isStationViewActive || BaseForgeStationController.IsPlayerOccupyingAnyStation(target.gameObject);
            bool isPanelOpen = ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen || GameLoopController.IsEndScreenOpen;

            // Gestión del cursor para el Jugador 1 / Primario / Local
            bool isPrimaryCam = (Instance == this || targetPlayer == null || targetPlayer.PlayerIndex == 0 || (PlayerMovement.LocalInstance != null && targetPlayer == PlayerMovement.LocalInstance));
            if (isPrimaryCam)
            {
                if (inStation || isPanelOpen)
                {
                    if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
                else
                {
                    if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }

                    if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                        if (UnityEngine.EventSystems.EventSystem.current != null)
                            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    }
                }
            }

            if (inStation || isPanelOpen) return;

            // 1. Lectura de Look Input directo desde eventos / gamepad
            if (_lookInput.sqrMagnitude > 0.001f)
            {
                // Si la magnitud es menor o igual a 1 (stick de gamepad normalizado), aplicar delta de tiempo
                if (_lookInput.sqrMagnitude <= 1.5f)
                {
                    _yaw += _lookInput.x * gamepadSensitivity * Time.deltaTime;
                    _pitch = Mathf.Clamp(_pitch - _lookInput.y * gamepadSensitivity * Time.deltaTime, minPitch, maxPitch);
                }
                else
                {
                    // Movimiento delta de ratón
                    _yaw += _lookInput.x * sensitivityX;
                    _pitch = Mathf.Clamp(_pitch - _lookInput.y * sensitivityY, minPitch, maxPitch);
                }
            }
            // 2. Si no hay evento y es cámara primaria, leer mouse delta directamente
            else if (isPrimaryCam && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                if (delta.sqrMagnitude > 0.001f)
                {
                    _yaw += delta.x * sensitivityX;
                    _pitch = Mathf.Clamp(_pitch - delta.y * sensitivityY, minPitch, maxPitch);
                }
            }

            // 3. Fallback de gamepad directo si este jugador tiene gamepad asignado
            if (targetPlayer != null)
            {
                var pInput = targetPlayer.GetComponent<PlayerInput>();
                if (pInput != null && pInput.currentControlScheme == "Gamepad" && Gamepad.current != null)
                {
                    Vector2 stick = Gamepad.current.rightStick.ReadValue();
                    if (stick.sqrMagnitude > 0.01f)
                    {
                        _yaw += stick.x * gamepadSensitivity * Time.deltaTime;
                        _pitch = Mathf.Clamp(_pitch - stick.y * gamepadSensitivity * Time.deltaTime, minPitch, maxPitch);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (!_isOrbitEnabled || target == null) return;

            if (_isStationViewActive)
            {
                ApplyStationCameraFrame();
                return;
            }

            if (BaseForgeStationController.IsPlayerOccupyingAnyStation(target.gameObject))
                return;

            Vector3 focusPoint = target.position + Vector3.up * targetHeight;
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 desiredDirection = rotation * Vector3.back;
            Vector3 sideOffset = rotation * Vector3.right * shoulderOffset;
            Vector3 desiredPosition = focusPoint + (desiredDirection * distance) + sideOffset;

            // Detección de colisiones para no atravesar paredes o suelo
            Vector3 rayDir = desiredPosition - focusPoint;
            float rayDist = rayDir.magnitude;
            if (rayDist > 0.01f)
            {
                if (Physics.SphereCast(focusPoint, collisionRadius, rayDir.normalized, out RaycastHit hit, rayDist, collisionLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider.transform.IsChildOf(target))
                    {
                        desiredPosition = focusPoint + rayDir.normalized * Mathf.Max(0.6f, hit.distance - 0.08f);
                    }
                }
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, smoothSpeed * Time.deltaTime);

            if (_shakeIntensity > 0.001f)
            {
                transform.position += Random.insideUnitSphere * _shakeIntensity;
                _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, Time.deltaTime * 16f);
            }
        }

        public void TriggerImpulse(float intensity = 0.22f) => _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);


        public void SetFollowing(bool follow) => _isOrbitEnabled = follow;

        public void SetTarget(Transform newTarget, PlayerMovement newPlayer = null)
        {
            target = newTarget;
            targetPlayer = newPlayer ?? (newTarget != null ? newTarget.GetComponent<PlayerMovement>() : null);
            if (targetPlayer != null)
            {
                targetPlayer.PlayerCamera = GetComponent<Camera>();
            }
        }
    }
}
