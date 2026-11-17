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

            // 1. Setup Pala (Part_Pala):
            Transform existingPalaPivot = _stationRoot.Find("PalaPivot");
            if (existingPalaPivot != null) _palaPivot = existingPalaPivot;
            else
            {
                GameObject goPala = new GameObject("PalaPivot");
                goPala.transform.SetParent(_stationRoot, false);
                Bounds pb = GetCombinedBounds(palaTransform.GetComponentsInChildren<Renderer>(true));
                goPala.transform.position = pb.center;
                _palaPivot = goPala.transform;
            }
            if (palaTransform != null && palaTransform.parent != _palaPivot) palaTransform.SetParent(_palaPivot, true);

            // 2. Setup Torno (Part_Ram):
            Transform existingRamPivot = _stationRoot.Find("RamPivot");
            if (existingRamPivot != null) _ramPivot = existingRamPivot;
            else
            {
                GameObject goRam = new GameObject("RamPivot");
                goRam.transform.SetParent(_stationRoot, false);
                Bounds rb = GetCombinedBounds(ramTransform.GetComponentsInChildren<Renderer>(true));
                goRam.transform.position = rb.center;
                _ramPivot = goRam.transform;
            }
            if (ramTransform.parent != _ramPivot) ramTransform.SetParent(_ramPivot, true);

            _palaPivot.localRotation = Quaternion.identity;
            _ramPivot.localRotation = Quaternion.identity;

            SetupWorkpieceBedAnchor(_stationRoot);

            // 3. Calibrar posición de la pala prensada (progress = 1)
            Vector3 bedPos = BedWorldFocusPoint;
            float worldScaleY = _stationRoot.lossyScale.y > 0.001f ? _stationRoot.lossyScale.y : 0.1964f;
            float targetPalaMinY_Pressed = bedPos.y + 0.085f;

            Bounds palaBounds = GetCombinedBounds(palaTransform.GetComponentsInChildren<Renderer>(true));
            float halfPalaHeight = Mathf.Max(0.04f, palaBounds.extents.y);

            // Posicionar la pala en su punto más bajo (contacto con el lingote)
            Vector3 pressedPalaWorld = new Vector3(bedPos.x, targetPalaMinY_Pressed + halfPalaHeight, bedPos.z);
            _palaPivot.position = pressedPalaWorld;
            palaBounds = GetCombinedBounds(palaTransform.GetComponentsInChildren<Renderer>(true));

            // 4. Conectar Part_Ram en contacto directo sobre Part_Pala (centrado y apoyado sobre la pala)
            Bounds ramBounds = GetCombinedBounds(ramTransform.GetComponentsInChildren<Renderer>(true));
            float verticalShift = (palaBounds.max.y - 0.015f) - ramBounds.min.y;
            _ramPivot.position = new Vector3(bedPos.x, _ramPivot.position.y + verticalShift, bedPos.z);

            // 5. Guardar posiciones base prensadas (progress = 1)
            _basePalaLocalPos = _palaPivot.localPosition;
            _baseRamLocalPos = _ramPivot.localPosition;

            // 6. Recorrido mecánico (stroke)
            float restOffset = (restElevationOffset >= 0.35f) ? restElevationOffset : 0.38f;
            float targetPalaMinY_Rest = bedPos.y + restOffset;
            float strokeWorld = targetPalaMinY_Rest - targetPalaMinY_Pressed;
            strokeDistance = strokeWorld / worldScaleY;

            _palaRestLocalPos = _basePalaLocalPos + Vector3.up * strokeDistance;
            _ramRestLocalPos = _baseRamLocalPos + Vector3.up * strokeDistance;
            _palaRestLocalRot = Quaternion.identity;
            _ramRestLocalRot = Quaternion.identity;

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

            // El torno gira sobre su eje mientras baja y sube, siempre conectado a la pala
            if (_ramPivot != null)
            {
                _ramPivot.localPosition = _ramRestLocalPos - verticalDisplacement;
                _ramPivot.localRotation = _ramRestLocalRot * spin;
            }

            // La pala baja y sube conectada al Ram, pero sin rotar
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
