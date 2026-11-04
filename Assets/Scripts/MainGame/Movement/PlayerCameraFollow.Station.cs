using UnityEngine;
using UnityEngine.InputSystem;

namespace ForjaGame.Core
{
    /// <summary>
    /// Transiciones cinemáticas de cámara para estaciones de forja (Horno, Prensa, Yunque)
    /// con amortiguación suave (SmoothDamp), paralaje sutil de cursor y restauración limpia.
    /// </summary>
    public partial class PlayerCameraFollow
    {
        private Vector3 _stationCamVelocity;
        private Vector3 _stationFocusPoint;
        private bool _stationAllowParallax;

        public bool IsInStationView => _isStationViewActive;

        public void SetStationView(Vector3 targetPos, Quaternion targetRot, float speed = 8.5f, bool allowParallax = false, Vector3 focusPoint = default)
        {
            _isStationViewActive = true;
            _stationTargetPos = targetPos;
            _stationTargetRot = targetRot;
            _stationTransitionSpeed = Mathf.Max(1.5f, speed);
            _stationAllowParallax = allowParallax;
            _stationFocusPoint = focusPoint;
        }

        public void ClearStationView()
        {
            if (_isStationViewActive)
            {
                _isStationViewActive = false;
                _stationCamVelocity = Vector3.zero;
                float eulerPitch = transform.eulerAngles.x;
                if (eulerPitch > 180f) eulerPitch -= 360f;
                _pitch = Mathf.Clamp(eulerPitch, minPitch, maxPitch);
                _yaw = transform.eulerAngles.y;
            }
        }

        public void SnapBehindTarget(Transform newTarget)
        {
            if (newTarget == null) return;
            SetTarget(newTarget);
            _isStationViewActive = false;
            _stationCamVelocity = Vector3.zero;
            _yaw = newTarget.eulerAngles.y;
            _pitch = 16f;

            Vector3 focus = newTarget.position + Vector3.up * targetHeight;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = focus + (rot * Vector3.back * distance) + (rot * Vector3.right * shoulderOffset);
            transform.rotation = rot;
        }

        private void ApplyStationCameraFrame()
        {
            Vector3 desiredPos = _stationTargetPos;
            Quaternion desiredRot = _stationTargetRot;

            if (_stationAllowParallax && Mouse.current != null && Screen.width > 10 && Screen.height > 10)
            {
                Vector2 mp = Mouse.current.position.ReadValue();
                float nx = Mathf.Clamp((mp.x / Screen.width - 0.5f) * 2f, -1f, 1f);
                float ny = Mathf.Clamp((mp.y / Screen.height - 0.5f) * 2f, -1f, 1f);
                Vector3 right = desiredRot * Vector3.right;
                Vector3 up = desiredRot * Vector3.up;
                desiredPos += right * (nx * 0.085f) + up * (ny * 0.055f);
                if (_stationFocusPoint != Vector3.zero)
                    desiredRot = Quaternion.LookRotation((_stationFocusPoint - desiredPos).normalized);
            }

            float smoothTime = Mathf.Clamp(1.0f / _stationTransitionSpeed, 0.06f, 0.38f);
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _stationCamVelocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, (_stationTransitionSpeed * 1.15f) * Time.deltaTime);
        }
    }
}
