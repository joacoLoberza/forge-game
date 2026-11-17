using UnityEngine;

namespace ForjaGame.Forge
{
    // Controlador térmico, herramientas medievales 3D interactivas (Martillo, Tajadera y Lija) y chispas del yunque.
    public class AnvilThermalController : MonoBehaviour
    {
        [SerializeField] private float initialTemperature = 25f, ambientTemperature = 25f, coolingRate = 5.5f, heatingRate = 175f;
        [SerializeField] private Vector3 reheatOffset = new Vector3(0f, 0.18f, 0.95f);

        private float _currentTemp = 25f, _strikeAnimTimer;
        private bool _isReheating, _mapsBound;
        private AnvilController _anvil;
        private Vector3 _baseLocalPos = Vector3.zero, _lastStrikePos;
        private Transform _hammerPivot, _hammerModel, _chiselModel, _sanderModel;
        private ParticleSystem _sparkSystem, _sawdustSystem;
        private MaterialPropertyBlock _propBlock;
        private Texture2D _thermalBaseTex, _thermalEmitTex;

        public float CurrentTemperature => _currentTemp;
        public float AmbientTemperature => ambientTemperature;
        public bool IsReheating => _isReheating;
        public Vector3 CurrentIngotOffset => _isReheating ? reheatOffset : Vector3.zero;
        public void ResetThermalTextureCache() => _mapsBound = false;

        private void OnDestroy()
        {
            if (_thermalBaseTex != null) Destroy(_thermalBaseTex);
            if (_thermalEmitTex != null) Destroy(_thermalEmitTex);
        }

        public void Initialize(AnvilController anvil)
        {
            _anvil = anvil; _currentTemp = initialTemperature; _isReheating = false;
            _propBlock = new MaterialPropertyBlock();
            if (_anvil != null && _anvil.IngotTransform != null) _baseLocalPos = _anvil.IngotTransform.localPosition;
            EnsureHammerAndSparks();
        }

        public void ResetThermal(float temp = 25f) { _currentTemp = temp; _isReheating = false; UpdateVisualColors(); }
        public void ToggleReheat() => SetReheating(!_isReheating);

        public void SetReheating(bool reheating)
        {
            var asm = _anvil != null ? _anvil.GetComponent<AnvilAssemblyController>() : null;
            if (_anvil == null || !_anvil.IsForging || _anvil.IsCarvingHandle || (asm != null && asm.IsAssembling)) { _isReheating = false; return; }
            _isReheating = reheating;
        }

        public void SetTemperature(float temp) => _currentTemp = Mathf.Clamp(temp, ambientTemperature, 1400f);

        public float GetMalleabilityMultiplier()
        {
            if (_currentTemp >= 800f) return Mathf.Lerp(1.0f, 1.35f, Mathf.Clamp01((_currentTemp - 800f) / 350f));
            if (_currentTemp >= 500f) return Mathf.Lerp(0.20f, 1.0f, (_currentTemp - 500f) / 300f);
            return _currentTemp >= 250f ? Mathf.Lerp(0.04f, 0.20f, (_currentTemp - 250f) / 250f) : 0.04f;
        }

        private void Update()
        {
            if (ForjaGame.UI.PauseMenuPanel.IsGamePaused) return;
            var asm = _anvil != null ? _anvil.GetComponent<AnvilAssemblyController>() : null;
            if (_anvil == null || !_anvil.IsForging || (asm != null && asm.IsAssembling))
            {
                if (_hammerPivot != null && _hammerPivot.gameObject.activeSelf) _hammerPivot.gameObject.SetActive(false);
                return;
            }
            if (!_anvil.IsCarvingHandle && (_anvil.QuenchDifferential == null || !_anvil.QuenchDifferential.IsQuenching))
            {
                bool inTut = ForjaGame.UI.ForgeTutorialDirector.Instance != null && ForjaGame.UI.ForgeTutorialDirector.Instance.IsTutorialActive;
                if (_isReheating)
                {
                    _currentTemp = Mathf.Min(1400f, _currentTemp + heatingRate * Time.deltaTime);
                    if (_currentTemp > 1240f) _anvil.ReduceQualityFromOverheat((inTut ? 0.015f : 0.025f) * Time.deltaTime);
                }
                else if (_currentTemp > ambientTemperature) _currentTemp = Mathf.Max(ambientTemperature, _currentTemp - coolingRate * (inTut ? 0.35f : 1f) * Time.deltaTime);
                UpdateIngotTransform();
            }
            if (_strikeAnimTimer > 0f) _strikeAnimTimer = Mathf.Max(0f, _strikeAnimTimer - Time.deltaTime);
            UpdateVisualColors();
        }

        private void UpdateIngotTransform()
        {
            if (_anvil.IngotTransform == null || (_anvil.QuenchDifferential != null && _anvil.QuenchDifferential.IsQuenching)) return;
            _anvil.IngotTransform.localPosition = Vector3.Lerp(_anvil.IngotTransform.localPosition, _baseLocalPos + (_isReheating ? reheatOffset : Vector3.zero), Time.deltaTime * 3.5f);
        }

        public void UpdateVisualColors()
        {
            if (_anvil == null || _anvil.IngotRenderer == null || _anvil.IsCarvingHandle) return;
            Color baseMetal = _anvil.CurrentMetalBaseColor;
            ThermalIngotGradientBuilder.EnsureTextures(ref _thermalBaseTex, ref _thermalEmitTex);
            ThermalIngotGradientBuilder.EvaluateContinuousThermalState(
                baseMetal, _currentTemp, out Color albedoTint, out Color emitCol, out float metallic, out float smoothness);

            Material mat = _anvil.IngotRenderer.material;
            if (mat != null)
            {
                if (!_mapsBound || mat.mainTexture != _thermalBaseTex)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.mainTexture = _thermalBaseTex;
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _thermalBaseTex);
                    if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", _thermalEmitTex);
                    _mapsBound = true;
                }
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            }

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _anvil.IngotRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetTexture("_BaseMap", _thermalBaseTex);
            _propBlock.SetTexture("_MainTex", _thermalBaseTex);
            _propBlock.SetTexture("_EmissionMap", _thermalEmitTex);
            _propBlock.SetColor("_BaseColor", albedoTint);
            _propBlock.SetColor("_Color", albedoTint);
            _propBlock.SetColor("_EmissionColor", emitCol);
            _anvil.IngotRenderer.SetPropertyBlock(_propBlock);
        }

        public void UpdateHammerHover(Vector3 worldHitPos, bool visible, AnvilToolMode mode = AnvilToolMode.Hammer, bool isActivelyPolishing = false, float cutYawDeg = 0f)
        {
            EnsureHammerAndSparks();
            if (_hammerPivot == null) return;
            bool shouldShow = visible && _anvil != null && _anvil.IsForging && !_anvil.IsIngotRuinedTooSmall && !_isReheating;
            if (_hammerPivot.gameObject.activeSelf != shouldShow) _hammerPivot.gameObject.SetActive(shouldShow);
            if (!shouldShow) return;

            if (_hammerModel != null) _hammerModel.gameObject.SetActive(mode == AnvilToolMode.Hammer);
            if (_chiselModel != null) _chiselModel.gameObject.SetActive(mode == AnvilToolMode.Cut);
            if (_sanderModel != null) _sanderModel.gameObject.SetActive(mode == AnvilToolMode.Polish);

            if (mode == AnvilToolMode.Polish)
            {
                float rub = isActivelyPolishing ? Mathf.Sin(Time.time * 32f) * 0.032f : 0f;
                Vector3 targetSander = worldHitPos + _anvil.transform.right * rub + Vector3.up * (isActivelyPolishing ? 0.018f : 0.065f);
                _hammerPivot.position = Vector3.Lerp(_hammerPivot.position, targetSander, Time.deltaTime * 25f);
                _hammerPivot.rotation = Quaternion.Slerp(_hammerPivot.rotation, _anvil.transform.rotation * Quaternion.Euler(isActivelyPolishing ? 4f : -12f, isActivelyPolishing ? Mathf.Sin(Time.time * 16f) * 8f : 0f, 0f), Time.deltaTime * 20f);
                return;
            }
            if (mode == AnvilToolMode.Cut)
            {
                float downArc = _strikeAnimTimer > 0f ? (1f - (_strikeAnimTimer / 0.17f) < 0.35f ? (1f - _strikeAnimTimer / 0.17f) / 0.35f : 1f - ((1f - _strikeAnimTimer / 0.17f) - 0.35f) / 0.65f) : 0f;
                Vector3 basePos = _strikeAnimTimer > 0f ? _lastStrikePos : worldHitPos;
                _hammerPivot.position = Vector3.Lerp(basePos + new Vector3(0f, 0.14f, 0f), basePos + new Vector3(0f, 0.015f, 0f), downArc);
                _hammerPivot.rotation = Quaternion.Slerp(_hammerPivot.rotation, _anvil.transform.rotation * Quaternion.Euler(Mathf.Lerp(-6f, 4f, downArc), cutYawDeg, 0f), Time.deltaTime * 22f);
                return;
            }
            if (_strikeAnimTimer > 0f)
            {
                float u = 1f - (_strikeAnimTimer / 0.17f), downArc = u < 0.32f ? (u / 0.32f) : (1f - (u - 0.32f) / 0.68f);
                _hammerPivot.position = Vector3.Lerp(_lastStrikePos + new Vector3(0.06f, 0.22f, -0.05f), _lastStrikePos + new Vector3(0.01f, 0.032f, -0.01f), downArc);
                _hammerPivot.rotation = _anvil.transform.rotation * Quaternion.Euler(Mathf.Lerp(-36f, 18f, downArc), -18f, 0f);
            }
            else
            {
                _hammerPivot.position = Vector3.Lerp(_hammerPivot.position, worldHitPos + new Vector3(0.06f, 0.22f, -0.05f), Time.deltaTime * 22f);
                _hammerPivot.rotation = Quaternion.Slerp(_hammerPivot.rotation, _anvil.transform.rotation * Quaternion.Euler(-36f, -18f, 0f), Time.deltaTime * 18f);
            }
        }

        public void TriggerHammerStrikeAndSparks(Vector3 worldHitPos, float quality = 0.85f, bool isCut = false)
        {
            EnsureHammerAndSparks();
            _lastStrikePos = worldHitPos; _strikeAnimTimer = 0.17f;
            bool isWood = _anvil != null && _anvil.IsCarvingHandle;
            ParticleSystem ps = isWood ? _sawdustSystem : _sparkSystem;
            if (ps != null)
            {
                ps.transform.position = worldHitPos + Vector3.up * 0.015f;
                int count = isWood ? (isCut ? 22 : 12) : (isCut ? 28 : Mathf.RoundToInt(Mathf.Lerp(24f, 48f, Mathf.Clamp01(quality)) * (_currentTemp >= 500f ? 1.25f : 0.55f)));
                ps.Emit(count);
            }
        }

        public void TriggerSawdustBurst(Vector3 worldPos, int count = 16)
        {
            EnsureHammerAndSparks();
            if (_sawdustSystem == null) return;
            _sawdustSystem.transform.position = worldPos + Vector3.up * 0.015f;
            _sawdustSystem.Emit(count);
        }

        private void EnsureHammerAndSparks()
        {
            if (_hammerPivot == null)
            {
                var root = new GameObject("VisualBlacksmithTools"); root.transform.SetParent(transform, false); _hammerPivot = root.transform;
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material ironMat = MakeMat(sh, new Color(0.22f, 0.23f, 0.26f), 0.90f, 0.56f), steelEdgeMat = MakeMat(sh, new Color(0.56f, 0.59f, 0.64f), 0.94f, 0.74f);
                Material woodMat = MakeMat(sh, new Color(0.44f, 0.27f, 0.13f), 0.08f, 0.35f), leatherMat = MakeMat(sh, new Color(0.24f, 0.13f, 0.07f), 0.10f, 0.28f), stoneSandMat = MakeMat(sh, new Color(0.62f, 0.57f, 0.47f), 0.05f, 0.18f);

                _hammerModel = new GameObject("MedievalCrossPeenHammer").transform; _hammerModel.SetParent(_hammerPivot, false);
                CreatePart("HeadCore", PrimitiveType.Cube, _hammerModel, new Vector3(0f, 0.046f, 0f), new Vector3(0.062f, 0.056f, 0.068f), ironMat);
                CreatePart("PollNeck", PrimitiveType.Cylinder, _hammerModel, new Vector3(0f, 0.013f, 0f), new Vector3(0.050f, 0.005f, 0.050f), ironMat);
                CreatePart("StrikingPollFace", PrimitiveType.Cylinder, _hammerModel, new Vector3(0f, 0.004f, 0f), new Vector3(0.062f, 0.004f, 0.062f), steelEdgeMat);
                CreatePart("CrossPeenWedge", PrimitiveType.Cube, _hammerModel, new Vector3(0f, 0.083f, 0f), new Vector3(0.056f, 0.018f, 0.032f), ironMat);
                CreatePart("CrossPeenCrest", PrimitiveType.Cylinder, _hammerModel, new Vector3(0f, 0.092f, 0f), new Vector3(0.018f, 0.027f, 0.018f), steelEdgeMat).localRotation = Quaternion.Euler(0f, 0f, 90f);
                CreatePart("EyeCollar", PrimitiveType.Cylinder, _hammerModel, new Vector3(0f, 0.046f, -0.043f), new Vector3(0.042f, 0.009f, 0.042f), steelEdgeMat).localRotation = Quaternion.Euler(90f, 0f, 0f);
                CreatePart("AshShaft", PrimitiveType.Cylinder, _hammerModel, new Vector3(0f, 0.046f, -0.212f), new Vector3(0.028f, 0.160f, 0.024f), woodMat).localRotation = Quaternion.Euler(90f, 0f, 0f);
                CreatePart("LeatherGrip", PrimitiveType.Cylinder, _hammerModel, new Vector3(0f, 0.046f, -0.286f), new Vector3(0.033f, 0.068f, 0.029f), leatherMat).localRotation = Quaternion.Euler(90f, 0f, 0f);
                CreatePart("PommelRing", PrimitiveType.Sphere, _hammerModel, new Vector3(0f, 0.046f, -0.382f), new Vector3(0.036f, 0.032f, 0.022f), ironMat);

                _chiselModel = new GameObject("MedievalHotCutChisel").transform; _chiselModel.SetParent(_hammerPivot, false);
                CreatePart("ChiselBladeWedge", PrimitiveType.Cube, _chiselModel, new Vector3(0f, 0.024f, 0f), new Vector3(0.014f, 0.046f, 0.078f), steelEdgeMat);
                CreatePart("ChiselBody", PrimitiveType.Cube, _chiselModel, new Vector3(0f, 0.068f, 0f), new Vector3(0.038f, 0.056f, 0.064f), ironMat);
                CreatePart("MushroomedPoll", PrimitiveType.Cylinder, _chiselModel, new Vector3(0f, 0.102f, 0f), new Vector3(0.048f, 0.010f, 0.048f), steelEdgeMat);
                CreatePart("IronRodCollar", PrimitiveType.Cylinder, _chiselModel, new Vector3(0.022f, 0.064f, 0f), new Vector3(0.028f, 0.018f, 0.028f), ironMat).localRotation = Quaternion.Euler(0f, 0f, 90f);
                CreatePart("SideWoodHandle", PrimitiveType.Cylinder, _chiselModel, new Vector3(0.165f, 0.064f, 0f), new Vector3(0.022f, 0.145f, 0.022f), woodMat).localRotation = Quaternion.Euler(0f, 0f, 90f);

                _sanderModel = new GameObject("MedievalWhetstoneSander").transform; _sanderModel.SetParent(_hammerPivot, false);
                CreatePart("CoarseSandstoneBase", PrimitiveType.Cube, _sanderModel, new Vector3(0f, 0.014f, 0f), new Vector3(0.068f, 0.026f, 0.145f), stoneSandMat);
                CreatePart("OakGripBlock", PrimitiveType.Cube, _sanderModel, new Vector3(0f, 0.038f, 0f), new Vector3(0.062f, 0.024f, 0.135f), woodMat);
                CreatePart("LeatherHandStrap", PrimitiveType.Cylinder, _sanderModel, new Vector3(0f, 0.054f, 0f), new Vector3(0.052f, 0.048f, 0.026f), leatherMat).localRotation = Quaternion.Euler(90f, 0f, 0f);
                CreatePart("FrontIronClamp", PrimitiveType.Cube, _sanderModel, new Vector3(0f, 0.026f, 0.072f), new Vector3(0.070f, 0.028f, 0.008f), ironMat);
                CreatePart("BackIronClamp", PrimitiveType.Cube, _sanderModel, new Vector3(0f, 0.026f, -0.072f), new Vector3(0.070f, 0.028f, 0.008f), ironMat);
                root.SetActive(false);
            }

            if (_sparkSystem == null) _sparkSystem = AnvilStationMeshBuilder.CreateBurstSystem(transform, false);
            if (_sawdustSystem == null) _sawdustSystem = AnvilStationMeshBuilder.CreateBurstSystem(transform, true);
        }

        private static Material MakeMat(Shader sh, Color col, float met, float sm)
        {
            var m = new Material(sh) { color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", met);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", sm);
            return m;
        }

        private static Transform CreatePart(string n, PrimitiveType pt, Transform p, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(pt); g.name = n;
            if (g.TryGetComponent<Collider>(out var c)) Destroy(c);
            g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat; return g.transform;
        }
    }
}
