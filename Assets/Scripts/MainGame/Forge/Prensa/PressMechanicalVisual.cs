using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Anima mecánicamente los componentes 3D de la Prensa: el torno/rosca gira mientras baja y sube,
    /// la pala solo sube y baja sin girar (sin palanca), y coordina la Lupa -> Lingote sobre la mesada.
    /// </summary>
    public partial class PressMechanicalVisual : MonoBehaviour
    {
        [Header("Referencias de Componentes Móviles")]
        [SerializeField] private Transform ramTransform;
        [SerializeField] private Transform palaTransform;
        [SerializeField] private PressController pressController;

        [Header("Calibración de Recorrido Mecánico")]
        [SerializeField] private float strokeDistance = 2.14f;
        [SerializeField] private float screwRotationDegrees = 720.0f;
        [SerializeField] private float restElevationOffset = 0.42f;

        private Transform _stationRoot;
        private Transform _ramPivot;
        private Transform _palaPivot;
        private Vector3 _baseRamLocalPos;
        private Vector3 _basePalaLocalPos;
        private Vector3 _ramRestLocalPos;
        private Quaternion _ramRestLocalRot;
        private Vector3 _palaRestLocalPos;
        private Quaternion _palaRestLocalRot;
        private float _smoothProgress;
        private bool _isInitialized;

        private void Awake()
        {
            if (pressController == null) pressController = GetComponent<PressController>();
            if (pressController == null) pressController = GetComponentInChildren<PressController>();
        }

        private void OnEnable()
        {
            if (pressController == null) pressController = GetComponent<PressController>();
            if (pressController != null)
            {
                pressController.PressReset += OnPressReset;
                pressController.PressCompleted += OnPressCompleted3D;
            }
        }

        private void OnDisable()
        {
            if (pressController != null)
            {
                pressController.PressReset -= OnPressReset;
                pressController.PressCompleted -= OnPressCompleted3D;
            }
        }

        public void InitializeScrewPress(Transform visualRoot, Transform ram, Transform pala, Transform unusedPalanca = null)
        {
            _stationRoot = visualRoot;
            ramTransform = ram;
            palaTransform = pala;
            _ramPivot = null;
            _palaPivot = null;
            _isInitialized = false;
            SetupPivots();
        }

        private void SetupPivots()
        {
            if (_stationRoot == null) _stationRoot = transform.Find("VisualModel_Prensa");
            if (_stationRoot == null) _stationRoot = transform;

            Transform oldPalanca = ForgeStationModelSetup.FindDeepChild(_stationRoot, "Part_Palanca");
            if (oldPalanca != null)
            {
                oldPalanca.SetParent(null);
                oldPalanca.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(oldPalanca.gameObject);
                else DestroyImmediate(oldPalanca.gameObject);
            }

            if (ramTransform == null) ramTransform = ForgeStationModelSetup.FindDeepChild(_stationRoot, "Part_Ram");
            if (palaTransform == null) palaTransform = ForgeStationModelSetup.FindDeepChild(_stationRoot, "Part_Pala");
            if (ramTransform == null) return;

            ramTransform.gameObject.SetActive(true);
            if (palaTransform != null) palaTransform.gameObject.SetActive(true);

            // 1. Pivote del Torno (Part_Ram) centrado en el eje vertical del tornillo
            Transform existingRamPivot = _stationRoot.Find("RamPivot");
            if (existingRamPivot != null)
            {
                _ramPivot = existingRamPivot;
            }
            else
            {
                GameObject go = new GameObject("RamPivot");
                go.transform.SetParent(_stationRoot, false);
                Bounds b = GetCombinedBounds(ramTransform.GetComponentsInChildren<Renderer>(true));
                go.transform.position = b.center;
                _ramPivot = go.transform;
            }

            if (ramTransform.parent != _ramPivot) ramTransform.SetParent(_ramPivot, true);

            // 2. Pivote de la Pala (Part_Pala): movimiento puramente vertical (subir y bajar sin rotar)
            float halfPalaHeight = 0.12f;
            if (palaTransform != null)
            {
                Transform existingPalaPivot = _stationRoot.Find("PalaPivot");
                if (existingPalaPivot != null)
                {
                    _palaPivot = existingPalaPivot;
                }
                else
                {
                    GameObject go = new GameObject("PalaPivot");
                    go.transform.SetParent(_stationRoot, false);
                    Bounds pb = GetCombinedBounds(palaTransform.GetComponentsInChildren<Renderer>(true));
                    go.transform.position = new Vector3(_ramPivot.position.x, pb.center.y, _ramPivot.position.z);
                    _palaPivot = go.transform;
                }

                if (palaTransform.parent != _palaPivot) palaTransform.SetParent(_palaPivot, true);
                Bounds palaBounds = GetCombinedBounds(palaTransform.GetComponentsInChildren<Renderer>(true));
                halfPalaHeight = Mathf.Max(0.04f, palaBounds.extents.y);
            }

            SetupWorkpieceBedAnchor(_stationRoot);

            // Calibrar recorrido vertical exacto (eje Y):
            // BedWorldFocusPoint es la cara superior de la mesa donde descansa la pieza.
            // En reposo (progress = 0), la pala debe despejar la Lupa (altura ~0.35m + margen ~0.38m).
            // Al prensar (progress = 1), la pala desciende hasta hacer contacto con el lingote (0.085m de grosor).
            Vector3 bedPos = BedWorldFocusPoint;
            float worldScaleY = _stationRoot.lossyScale.y > 0.001f ? _stationRoot.lossyScale.y : 0.1964f;

            float targetPalaMinY_Pressed = bedPos.y + 0.085f;
            float restOffset = (restElevationOffset >= 0.35f) ? restElevationOffset : 0.38f;
            float targetPalaMinY_Rest = bedPos.y + restOffset;
            float strokeWorld = targetPalaMinY_Rest - targetPalaMinY_Pressed;

            strokeDistance = strokeWorld / worldScaleY;

            if (_palaPivot != null)
            {
                Vector3 pressedPalaWorld = new Vector3(_palaPivot.position.x, targetPalaMinY_Pressed + halfPalaHeight, _palaPivot.position.z);
                _basePalaLocalPos = _stationRoot.InverseTransformPoint(pressedPalaWorld);
                _palaRestLocalPos = _basePalaLocalPos + Vector3.up * strokeDistance;
                _palaRestLocalRot = _palaPivot.localRotation;
            }

            if (_ramPivot != null)
            {
                _baseRamLocalPos = _ramPivot.localPosition;
                _ramRestLocalPos = _baseRamLocalPos + Vector3.up * strokeDistance;
                _ramRestLocalRot = _ramPivot.localRotation;
            }

            _isInitialized = true;
            ApplyProgress(0f);
        }

        private void Update()
        {
            if (!_isInitialized || ramTransform == null || _ramPivot == null)
            {
                ForgeStationModelSetup.SetupPressVisual(forceRebuild: false);
                SetupPivots();
                if (!_isInitialized) return;
            }

            float targetProgress = 0f;
            if (pressController != null && pressController.HasBloom)
            {
                // Al completar el prensado vuelve al 0% (reposo elevado) para revelar el lingote terminado
                targetProgress = pressController.IsCompleted ? 0f : Mathf.Clamp01(pressController.PressProgress);
            }

            float speed = (targetProgress < _smoothProgress) ? 6.5f : 14.0f;
            _smoothProgress = Mathf.MoveTowards(_smoothProgress, targetProgress, Time.deltaTime * speed);

            ApplyProgress(_smoothProgress);
            UpdateWorkpieceVisuals();
        }

        private void ApplyProgress(float progress)
        {
            Vector3 verticalDisplacement = Vector3.up * (strokeDistance * progress);
            Quaternion spin = Quaternion.Euler(0f, progress * screwRotationDegrees, 0f);

            // El torno gira sobre su eje mientras baja y sube
            if (_ramPivot != null)
            {
                _ramPivot.localPosition = _ramRestLocalPos - verticalDisplacement;
                _ramPivot.localRotation = _ramRestLocalRot * spin;
            }

            // La pala únicamente sube y baja, sin girar
            if (_palaPivot != null)
            {
                _palaPivot.localPosition = _palaRestLocalPos - verticalDisplacement;
                _palaPivot.localRotation = _palaRestLocalRot;
            }
        }

        private void OnPressReset()
        {
            _smoothProgress = 0f;
            ApplyProgress(0f);
            ClearWorkpieceVisual();
        }

        private Bounds GetCombinedBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0) return new Bounds(transform.position, Vector3.one * 0.1f);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].enabled) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
