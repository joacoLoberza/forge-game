using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Estación física 3D de la Prensa Mecánica: encuadra la mesada inferior (donde la Lupa se convierte
    /// en Lingote) junto con el descenso rotatorio del tornillo, la palanca y la pala.
    /// </summary>
    public class PressStationController : BaseForgeStationController
    {
        [Header("Referencias de Prensa")]
        [SerializeField] private PressController pressController;
        [SerializeField] private GameObject pressUI;

        private PressMechanicalVisual _pressVisual;

        public PressController Press => pressController;
        public override string InteractionPrompt => "Operar Prensa";

        private void Awake()
        {
            if (pressController == null)
            {
                pressController = GetComponentInChildren<PressController>();
                if (pressController == null) pressController = GetComponent<PressController>();
            }

            if (stationUI == null && pressUI != null)
                stationUI = pressUI;
            else if (pressUI == null && stationUI != null)
                pressUI = stationUI;

            if (stationUI == null)
            {
                var panel = FindFirstObjectByType<UI.PressPanel>(FindObjectsInactive.Include);
                if (panel != null)
                {
                    stationUI = panel.gameObject;
                    pressUI = stationUI;
                }
            }

            ForgeStationModelSetup.SetupPressVisual(true);
            _pressVisual = GetComponent<PressMechanicalVisual>();
        }

        protected override void LateUpdate()
        {
            if (!_isOccupied || _currentInteractor == null) return;
            var cam = GetInteractorCamera(_currentInteractor);
            if (cam == null) return;

            GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset);
            Vector3 camPos = focusPoint + transform.TransformDirection(camOffset);
            Quaternion targetRot = Quaternion.LookRotation(focusPoint - camPos);
            cam.SetStationView(camPos, targetRot, 6.5f, allowParallax: true, focusPoint: focusPoint);
        }

        protected override void GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset)
        {
            if (_pressVisual == null) _pressVisual = GetComponent<PressMechanicalVisual>();
            Vector3 bedPos = _pressVisual != null
                ? _pressVisual.BedWorldFocusPoint
                : new Vector3(transform.position.x, ForgeStationModelSetup.GetGroundHeightAt(transform.position.x, transform.position.z) + 0.68f, transform.position.z);

            if (pressController != null && pressController.IsCompleted)
            {
                // Acercamiento limpio al Lingote recién prensado sobre la parte inferior de la prensa
                focusPoint = bedPos + new Vector3(0f, 0.08f, 0f);
                camOffset = new Vector3(0.24f, 0.44f, -1.48f);
            }
            else
            {
                // Vista 3/4 frontal angulada que muestra la Lupa en la mesada inferior, pala elevada y rosca giratoria
                focusPoint = bedPos + new Vector3(0f, 0.28f, 0f);
                camOffset = new Vector3(0.38f, 0.50f, -1.90f);
            }
        }
    }
}
