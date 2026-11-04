using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Controlador visual 3D del Horno: genera fuego interno proporcional al calor ingresado,
    /// muestra vista previa 3D de minerales seleccionados, anima su entrada por la boca superior
    /// y expone el modelo 3D de la Lupa en la boca inferior al finalizar la fundición.
    /// </summary>
    public partial class FurnaceVisualController : MonoBehaviour
    {
        public static FurnaceVisualController Instance { get; private set; }

        [SerializeField] private FurnaceController furnaceController;
        [SerializeField] private FurnaceStationController stationController;

        private Transform _fxRoot;
        private Transform _stagedPreviewRoot;
        private Transform _crucibleOresRoot;
        private Transform _bottomBloomRoot;

        private ParticleSystem _hearthFlamePs;
        private ParticleSystem _chimneySparksPs;
        private Light _hearthLight;
        private Light _crucibleLight;
        private readonly List<Material> _emberMaterials = new();
        private Material _interiorFireboxMat;
        private Material _interiorCrucibleMat;
        private static Texture2D _softFlameTex;

        private float _furnaceBaseY;
        private bool _isBuilt;

        public bool IsAnimatingTopDrop { get; private set; }
        public bool HasBottomBloomVisible => _activeBloomObj != null;

        private void Awake() { Instance = this; ResolveControllers(); }
        private void Start() => EnsureBuilt();

        private void OnEnable()
        {
            Instance = this;
            ResolveControllers();
            if (furnaceController != null) furnaceController.SmeltCompleted += OnFurnaceSmeltCompleted;
        }

        private void OnDisable()
        {
            if (furnaceController != null) furnaceController.SmeltCompleted -= OnFurnaceSmeltCompleted;
        }

        private void ResolveControllers()
        {
            if (furnaceController == null) furnaceController = GetComponent<FurnaceController>() ?? GetComponentInChildren<FurnaceController>();
            if (stationController == null) stationController = GetComponent<FurnaceStationController>() ?? GetComponentInParent<FurnaceStationController>();
        }

        public void EnsureBuilt()
        {
            ResolveControllers();
            MeasureFurnaceBaseY();

            if (_fxRoot == null)
            {
                UnitySafeUtility.DestroyChildrenNamed(transform, "Furnace3D_FX");

                _fxRoot = new GameObject("Furnace3D_FX").transform;
                _fxRoot.SetParent(transform, false);
                _fxRoot.position = new Vector3(transform.position.x, _furnaceBaseY, transform.position.z);
                _fxRoot.localRotation = Quaternion.identity;

                _stagedPreviewRoot = CreateSubRoot("StagedOresPreview");
                _crucibleOresRoot = CreateSubRoot("CrucibleInsertedOres");
                _bottomBloomRoot = CreateSubRoot("BottomBloomOutput");

                BuildCoalsAndFireEffects();
            }

            BindInteriorSubmeshMaterials();
            _isBuilt = true;
        }

        private Transform CreateSubRoot(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_fxRoot, false);
            t.localPosition = Vector3.zero;
            return t;
        }

        private void MeasureFurnaceBaseY()
        {
            Transform modelRoot = transform.Find("VisualModel_Horno");
            if (modelRoot != null)
            {
                var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    _furnaceBaseY = b.min.y;
                    return;
                }
            }
            _furnaceBaseY = ForgeStationModelSetup.GetGroundHeightAt(transform.position.x, transform.position.z) + 0.02f;
        }

        private void BindInteriorSubmeshMaterials()
        {
            Transform modelHorno = transform.Find("VisualModel_Horno/Model_Horno");
            if (modelHorno == null) return;
            var mr = modelHorno.GetComponentInChildren<MeshRenderer>(true);
            if (mr == null || mr.sharedMaterials == null || mr.sharedMaterials.Length < 4) return;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mats = mr.sharedMaterials;
            _interiorFireboxMat = new Material(lit) { name = "Mat_HornoInteriorFogon" };
            _interiorFireboxMat.color = new Color(0.11f, 0.09f, 0.08f);
            if (_interiorFireboxMat.HasProperty("_BaseColor")) _interiorFireboxMat.SetColor("_BaseColor", new Color(0.11f, 0.09f, 0.08f));
            if (_interiorFireboxMat.HasProperty("_Smoothness")) _interiorFireboxMat.SetFloat("_Smoothness", 0.12f);
            _interiorFireboxMat.EnableKeyword("_EMISSION");

            mats[3] = _interiorFireboxMat;
            if (mats.Length >= 5)
            {
                _interiorCrucibleMat = new Material(_interiorFireboxMat) { name = "Mat_HornoInteriorFusion" };
                mats[4] = _interiorCrucibleMat;
            }
            mr.sharedMaterials = mats;
        }

        private void BuildCoalsAndFireEffects()
        {
            _emberMaterials.Clear();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Lecho de carbones en el fogón inferior (visible por el arco frontal -Z)
            for (int i = 0; i < 8; i++)
            {
                var coal = new GameObject($"HearthCoal_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                coal.transform.SetParent(_fxRoot, false);
                float ang = i * Mathf.PI * 0.25f;
                float rad = (i % 2 == 0) ? 0.14f : 0.26f;
                coal.transform.localPosition = new Vector3(Mathf.Cos(ang) * rad, 0.07f + (i % 3) * 0.025f, -0.24f + Mathf.Sin(ang) * rad * 0.85f);
                coal.transform.localRotation = Quaternion.Euler(15f * i, ang * Mathf.Rad2Deg, 22f * i);
                coal.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.085f, 700 + i * 17);

                var m = new Material(lit) { name = $"Mat_Ember_{i}" };
                m.color = new Color(0.10f, 0.10f, 0.11f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color);
                m.EnableKeyword("_EMISSION");
                coal.GetComponent<MeshRenderer>().sharedMaterial = m;
                _emberMaterials.Add(m);
            }

            _hearthFlamePs = CreateFireParticleSystem("HearthFlames", new Vector3(0f, 0.14f, -0.24f), isChimney: false);
            _chimneySparksPs = CreateFireParticleSystem("ChimneyHeatPlume", new Vector3(0f, 2.16f, 0f), isChimney: true);

            var lObj = new GameObject("HearthFireLight", typeof(Light));
            lObj.transform.SetParent(_fxRoot, false);
            lObj.transform.localPosition = new Vector3(0f, 0.44f, -0.34f);
            _hearthLight = lObj.GetComponent<Light>();
            _hearthLight.type = LightType.Point;
            _hearthLight.range = 4.6f;
            _hearthLight.intensity = 0f;

            var cObj = new GameObject("TopCrucibleLight", typeof(Light));
            cObj.transform.SetParent(_fxRoot, false);
            cObj.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            _crucibleLight = cObj.GetComponent<Light>();
            _crucibleLight.type = LightType.Point;
            _crucibleLight.range = 2.4f;
            _crucibleLight.intensity = 0f;
        }

        private ParticleSystem CreateFireParticleSystem(string name, Vector3 localPos, bool isChimney)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_fxRoot, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                          ?? Shader.Find("Particles/Standard Unlit")
                          ?? Shader.Find("Sprites/Default");
            Material pMat = new Material(pShader) { name = $"Mat_{name}" };
            Texture2D tex = GetSoftFlameTexture();
            if (pMat.HasProperty("_BaseMap")) pMat.SetTexture("_BaseMap", tex);
            if (pMat.HasProperty("_MainTex")) pMat.SetTexture("_MainTex", tex);
            psr.sharedMaterial = pMat;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = isChimney ? new ParticleSystem.MinMaxCurve(0.45f, 0.85f) : new ParticleSystem.MinMaxCurve(0.32f, 0.65f);
            main.startSpeed = isChimney ? new ParticleSystem.MinMaxCurve(0.45f, 0.95f) : new ParticleSystem.MinMaxCurve(0.45f, 0.92f);
            main.startSize = isChimney ? new ParticleSystem.MinMaxCurve(0.05f, 0.14f) : new ParticleSystem.MinMaxCurve(0.16f, 0.34f);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = isChimney ? 14f : 18f;
            shape.radius = isChimney ? 0.13f : 0.20f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.86f, 0.32f), 0f), new GradientColorKey(new Color(1f, 0.38f, 0.06f), 0.55f), new GradientColorKey(new Color(0.35f, 0.08f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.88f, 0.18f), new GradientAlphaKey(0f, 1f) });
            colLife.color = grad;
            return ps;
        }

        private static Texture2D GetSoftFlameTexture()
        {
            if (_softFlameTex != null) return _softFlameTex;
            const int S = 32;
            _softFlameTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - 15.5f) / 15.5f, dy = (y - 15.5f) / 15.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    _softFlameTex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            _softFlameTex.Apply();
            return _softFlameTex;
        }
    }
}
