using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    [ExecuteAlways]
    public partial class GreenPlateauTerrainController : MonoBehaviour
    {
        private const string GroundMeshVersionName = "MountainPlateauGround_V6";

        public static GreenPlateauTerrainController Instance { get; private set; }

        [Header("Configuración del Terreno de Meseta y Montaña")]
        [SerializeField] private float terrainSize = 340.0f;
        [SerializeField] private int resolution = 84;
        [SerializeField] private float maxElevation = 3.6f;
        [SerializeField] private Material grassMaterial;

        private GameObject _plateauObject;
        private static bool _isSubscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _isSubscribed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitGreenPlateau()
        {
            if (!_isSubscribed)
            {
                _isSubscribed = true;
                SceneManager.sceneLoaded += (scene, _) => CheckAndBuildSceneTerrain(scene);
            }
            CheckAndBuildSceneTerrain(SceneManager.GetActiveScene());
        }

        private static void CheckAndBuildSceneTerrain(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.name) || scene.name.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            var ctrl = FindFirstObjectByType<GreenPlateauTerrainController>();
            if (ctrl == null)
            {
                GameObject root = new GameObject("GreenPlateauTerrainController");
                ctrl = root.AddComponent<GreenPlateauTerrainController>();
            }
            ctrl.EnsurePlateau();
        }

        private void Awake() { Instance = this; EnsurePlateau(); }
        private void OnEnable() => EnsurePlateau();

        public static void CleanPreviousMapObjects()
        {
            var old = GameObject.Find("NaturalGreenPlateau");
            if (old != null) UnitySafeUtility.DestroyGameObjectSafe(old);
            var bounds = GameObject.Find("[PLATEAU_INVISIBLE_BOUNDARIES]");
            if (bounds != null) UnitySafeUtility.DestroyGameObjectSafe(bounds);
            CleanPreviousStoneWorkshop();
            CleanPreviousEnvironmentProps();
            CleanPreviousNatureVegetation();
        }

        public void EnsurePlateau()
        {
            var plane = GameObject.Find("Plane");
            if (plane != null) UnitySafeUtility.DestroyGameObjectSafe(plane);

            if (_plateauObject == null) _plateauObject = GameObject.Find("NaturalGreenPlateau");
            if (_plateauObject == null)
                _plateauObject = new GameObject("NaturalGreenPlateau", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));

            _plateauObject.SetActive(true);
            _plateauObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _plateauObject.transform.localScale = Vector3.one;

            var filter = UnitySafeUtility.GetOrAdd<MeshFilter>(_plateauObject);
            var renderer = UnitySafeUtility.GetOrAdd<MeshRenderer>(_plateauObject);
            var col = UnitySafeUtility.GetOrAdd<MeshCollider>(_plateauObject);

            float effSize = Mathf.Max(320f, terrainSize);
            int effRes = Mathf.Max(80, resolution);
            bool needsNewMesh = filter.sharedMesh == null || filter.sharedMesh.name != GroundMeshVersionName;
            if (needsNewMesh)
            {
                Mesh mesh = GenerateUndulatingMesh(effSize, effRes, maxElevation);
                filter.sharedMesh = mesh;
                col.sharedMesh = null;
                col.sharedMesh = mesh;
            }

            col.enabled = true;
            renderer.sharedMaterial = grassMaterial != null ? grassMaterial : CreateNaturalGrassMaterial();
            renderer.enabled = true;

            GameObject workshop = EnsureStoneWorkshopFloor();
            EnsureRusticWorkshopStructure(workshop != null ? workshop.transform : null);
            EnsureEnvironmentProps();
            EnsureNatureVegetation();
            EnsureAtmosphericLightingAndSky();
            EnsurePlateauInvisibleBoundaries();

            CaveDungeonBuilder.EnsureCaveBuiltInScene(forceRebuild: false);
            ForjaGame.Forge.ForgeStationModelSetup.SetupAllModelsInScene(false);
        }

        public static void EnsureAtmosphericLightingAndSky()
        {
            var sunObj = GameObject.Find("Directional Light") ?? GameObject.Find("Sun");
            if (sunObj == null) sunObj = new GameObject("Directional Light", typeof(Light));
            var sun = sunObj.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunObj.transform.rotation = Quaternion.Euler(48f, -36f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.46f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.12f);
            RenderSettings.ambientIntensity = 1.05f;

            Shader skyShader = Shader.Find("Skybox/Procedural") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (skyShader != null)
            {
                Material skyMat = new Material(skyShader) { name = "Mat_MedievalSky" };
                if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", new Color(0.24f, 0.44f, 0.76f));
                if (skyMat.HasProperty("_GroundColor")) skyMat.SetColor("_GroundColor", new Color(0.35f, 0.30f, 0.22f));
                if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", 1.02f);
                if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", 1.15f);
                if (skyMat.HasProperty("_SunSize")) skyMat.SetFloat("_SunSize", 0.04f);
                RenderSettings.skybox = skyMat;
            }
        }

        [ContextMenu("Reconstruir Meseta y Taller")]
        public void BuildGreenPlateau()
        {
            if (_plateauObject != null && _plateauObject.TryGetComponent<MeshFilter>(out var f))
                f.sharedMesh = null;

            CleanPreviousStoneWorkshop();
            CleanPreviousEnvironmentProps();
            CaveDungeonBuilder.EnsureCaveBuiltInScene(forceRebuild: true);
            EnsurePlateau();
        }
    }
}
