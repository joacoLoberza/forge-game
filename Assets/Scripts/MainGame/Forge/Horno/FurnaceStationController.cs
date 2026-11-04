using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Estación física 3D del Horno: gestiona cámara cinemática dinámica que encuadra el horno completo,
    /// sube hacia la boca superior al ingresar minerales y enfoca la boca inferior cuando la Lupa está lista.
    /// </summary>
    public class FurnaceStationController : BaseForgeStationController
    {
        [Header("Referencias de Horno")]
        [SerializeField] private FurnaceController furnaceController;
        [SerializeField] private GameObject furnaceUI;

        private FurnaceVisualController _visualController;
        private float _topInsertionCameraUntil = -1f;

        public FurnaceController Furnace => furnaceController;
        public override string InteractionPrompt => "Horno de Fundición";

        private void Awake()
        {
            if (furnaceController == null)
                furnaceController = GetComponentInChildren<FurnaceController>() ?? GetComponent<FurnaceController>();

            if (stationUI == null && furnaceUI != null)
                stationUI = furnaceUI;
            else if (furnaceUI == null && stationUI != null)
                furnaceUI = stationUI;

            ForgeStationModelSetup.SetupFurnaceVisual(true);
            _visualController = GetComponent<FurnaceVisualController>();
        }

        public void NotifyTopOreInsertion(float duration = 1.35f)
        {
            _topInsertionCameraUntil = Time.time + duration;
        }

        protected override void LateUpdate()
        {
            if (!_isOccupied || _currentInteractor == null) return;
            var cam = GetInteractorCamera(_currentInteractor);
            if (cam == null) return;

            GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset);
            Vector3 camPos = focusPoint + transform.TransformDirection(camOffset);
            Quaternion targetRot = Quaternion.LookRotation(focusPoint - camPos);
            cam.SetStationView(camPos, targetRot, 6.2f, allowParallax: true, focusPoint: focusPoint);
        }

        protected override void GetStationCameraFocus(out Vector3 focusPoint, out Vector3 camOffset)
        {
            float baseY = ForgeStationModelSetup.GetGroundHeightAt(transform.position.x, transform.position.z);
            bool watchingTop = Time.time < _topInsertionCameraUntil || (_visualController != null && _visualController.IsAnimatingTopDrop);
            bool hasBottomBloom = furnaceController != null && furnaceController.UnclaimedBloom != null;

            if (watchingTop)
            {
                // Encuadre elevado hacia la boca superior del horno al caer los minerales en crudo
                focusPoint = new Vector3(transform.position.x, baseY + 1.82f, transform.position.z);
                camOffset = new Vector3(0.58f, 0.86f, -2.42f);
            }
            else if (hasBottomBloom)
            {
                // Encuadre hacia el arco inferior mostrando la Lupa incandescente lista para sacar
                focusPoint = new Vector3(transform.position.x, baseY + 0.62f, transform.position.z) - transform.forward * 0.15f;
                camOffset = new Vector3(0.46f, 0.48f, -1.95f);
            }
            else
            {
                // Vista 3/4 dramática que muestra fogón inferior con llamas, minerales 3D y boca superior
                focusPoint = new Vector3(transform.position.x, baseY + 1.12f, transform.position.z);
                camOffset = new Vector3(0.72f, 0.56f, -2.52f);
            }
        }
    }
}
