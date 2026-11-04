using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    [ExecuteAlways]
    public partial class GreenPlateauTerrainController : MonoBehaviour
    {
        private const string GroundMeshVersionName = "MountainPlateauGround_V4";

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
            CleanPreviousStoneWorkshop();
            CleanPreviousEnvironmentProps();
        }

        public void EnsurePlateau()
        {
            var plane = GameObject.Find("Plane");
            if (plane != null)
            {
                if (plane.TryGetComponent<MeshRenderer>(out var pr)) pr.enabled = false;
                plane.transform.position = new Vector3(0f, -1.5f, 0f);
            }

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

            CaveDungeonBuilder.EnsureCaveBuiltInScene(forceRebuild: false);
            ForjaGame.Forge.ForgeStationModelSetup.SetupAllModelsInScene(false);
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
