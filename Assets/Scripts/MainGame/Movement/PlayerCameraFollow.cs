using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Forge;

namespace ForjaGame.Core
{
    /// <summary>
    /// Cámara orbital en tercera persona inspirada en Monster Hunter.
    /// Bloquea el cursor del ratón para orbitar con suavidad alrededor del jugador
    /// y cede el control cuando el jugador opera una estación de forja o abre menús.
    /// </summary>
    public class PlayerCameraFollow : MonoBehaviour
    {
        public static PlayerCameraFollow Instance { get; private set; }

        [Header("Target Configuration")]
        [SerializeField] private Transform target;
        [SerializeField] private float targetHeight = 2.05f;
        [SerializeField] private float distance = 6.2f;
        [SerializeField] private float shoulderOffset = 0.45f;

        [Header("Orbit & Sensitivity")]
        [SerializeField] private float sensitivityX = 0.16f;
        [SerializeField] private float sensitivityY = 0.14f;
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

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public float TargetHeight
        {
            get => targetHeight;
            set => targetHeight = value;
        }

        public float Distance
        {
            get => distance;
            set => distance = value;
        }

        public bool IsInStationView => _isStationViewActive;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
            }
            else
            {
                _yaw = transform.eulerAngles.y;
            }
        }

        private void Update()
        {
            if (target == null) return;

            bool inStation = _isStationViewActive || BaseForgeStationController.IsPlayerOccupyingAnyStation(target.gameObject);
            bool isPanelOpen = ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen;

            if (inStation || isPanelOpen)
            {
                // En estación o con UI abierta: liberar cursor
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }

            // En exploración activa: bloquear cursor para orbitar como en Monster Hunter
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _yaw += delta.x * sensitivityX;
                _pitch = Mathf.Clamp(_pitch - delta.y * sensitivityY, minPitch, maxPitch);
            }
        }

        private void LateUpdate()
        {
            if (!_isOrbitEnabled || target == null) return;

            if (_isStationViewActive)
            {
                transform.position = Vector3.Lerp(transform.position, _stationTargetPos, _stationTransitionSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, _stationTargetRot, _stationTransitionSpeed * Time.deltaTime);
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

        private float _shakeIntensity = 0f;
        public void TriggerImpulse(float intensity = 0.22f) => _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);

        public void SetStationView(Vector3 targetPos, Quaternion targetRot, float speed = 8.5f)
        {
            _isStationViewActive = true;
            _stationTargetPos = targetPos;
            _stationTargetRot = targetRot;
            _stationTransitionSpeed = speed;
        }

        public void ClearStationView()
        {
            if (_isStationViewActive)
            {
                _isStationViewActive = false;
                float eulerPitch = transform.eulerAngles.x;
                if (eulerPitch > 180f) eulerPitch -= 360f;
                _pitch = Mathf.Clamp(eulerPitch, minPitch, maxPitch);
                _yaw = transform.eulerAngles.y;
            }
        }

        public void SetFollowing(bool follow) => _isOrbitEnabled = follow;
    }
}
