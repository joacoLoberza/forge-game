using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Gestión de cámara cinemática y encuadres 3D para estaciones de forja interactivas.
    /// Provee métodos virtuales para cálculo de foco, seguimiento y restauración de vista.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public abstract partial class BaseForgeStationController
    {
        protected virtual void LateUpdate()
        {
            if (!_isOccupied || _currentInteractor == null) return;
            var cam = GetInteractorCamera(_currentInteractor);
            if (cam == null) return;

            GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset);
            Vector3 camPos = focusPoint + transform.TransformDirection(camOffset);
            Quaternion targetRot = Quaternion.LookRotation(focusPoint - camPos);
            cam.SetStationView(camPos, targetRot, 6.0f, allowParallax: true, focusPoint: focusPoint);
        }

        protected virtual void GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset)
        {
            focusPoint = transform.position + Vector3.up * 1f;
            camOffset = new Vector3(0f, 0.5f, -1.5f);
        }

        protected PlayerCameraFollow GetInteractorCamera(GameObject interactor)
        {
            if (interactor != null)
            {
                var pMove = interactor.GetComponentInParent<PlayerMovement>();
                if (pMove != null && pMove.PlayerCamera != null)
                {
                    var cf = pMove.PlayerCamera.GetComponent<PlayerCameraFollow>();
                    if (cf != null) return cf;
                }
            }
            return PlayerCameraFollow.Instance;
        }

        protected void StartStationCamera(GameObject interactor)
        {
            var cam = GetInteractorCamera(interactor);
            if (cam == null) return;
            GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset);
            Vector3 camPos = focusPoint + transform.TransformDirection(camOffset);
            Quaternion targetRot = Quaternion.LookRotation(focusPoint - camPos);
            cam.SetStationView(camPos, targetRot, 6.0f, allowParallax: true, focusPoint: focusPoint);
        }
    }
}

