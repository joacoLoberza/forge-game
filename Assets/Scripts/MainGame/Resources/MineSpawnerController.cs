using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Orquestador ecológico de yacimientos minerales y hábitats en las 6 cámaras (V10):
    /// - Genera 48 vetas minerales claramente visibles desde el ingreso a la mina (Z = 44m)
    ///   hasta el Santuario Final (Z = 540m), integradas frente a los Filones Madre y en las terrazas.
    /// </summary>
    public partial class MineSpawnerController : MonoBehaviour
    {
        public static MineSpawnerController Instance { get; private set; }
        public static ResourceNodeController FirstCoalVein { get; private set; }
        public static ForjaGame.Combat.SpiderEnemyController FirstDoorSpider { get; private set; }

        [System.Serializable]
        public class OreTierSpawnConfig
        {
            public string resourceType = "Coal";
            public int count = 5;
            public float minZ = 42f, maxZ = 180f;
            public int maxHealth = 55;
            public int dropAmount = 3;
            public float respawnDelay = 25f;
            public float minLateralFactor = 0.22f, maxLateralFactor = 0.76f;

            public OreTierSpawnConfig() { }
            public OreTierSpawnConfig(string type, int cnt, float z0, float z1, int hp, int drop, float resp, float lat0, float lat1)
            {
                resourceType = type; count = cnt; minZ = z0; maxZ = z1;
                maxHealth = hp; dropAmount = drop; respawnDelay = resp;
                minLateralFactor = lat0; maxLateralFactor = lat1;
            }
        }

        [SerializeField] private List<OreTierSpawnConfig> oreTiers = new();
        private Transform _nodesContainer;
        private Transform _enemiesContainer;
        private readonly Dictionary<string, List<ResourceNodeController>> _nodesByOreType = new();

        private void Awake()
        {
            Instance = this;
            EnsureDefaultTierConfigs();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapMineSpawner()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") return;
            if (Instance == null && FindFirstObjectByType<MineSpawnerController>() == null)
                new GameObject("MineSpawnerController").AddComponent<MineSpawnerController>();
        }

        private void Start()
        {
            CaveDungeonBuilder.EnsureCaveBuiltInScene();
            SpawnAllCaveResources();
            SpawnAllCaveEnemies();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureDefaultTierConfigs()
        {
            oreTiers = new List<OreTierSpawnConfig>
            {
                new("Coal",  12,  44f, 185f,  45, 3, 20f, 0.24f, 0.54f),
                new("Copper",12,  58f, 280f,  70, 3, 26f, 0.25f, 0.56f),
                new("Iron",  14, 145f, 468f, 105, 3, 32f, 0.26f, 0.58f),
                new("Gold",  10, 245f, 478f, 150, 2, 42f, 0.28f, 0.60f)
            };
        }

        public void SpawnAllCaveResources()
        {
            EnsureDefaultTierConfigs();
            var existingByName = GameObject.Find("[CAVE_ORE_NODES]");
            if (existingByName != null) UnitySafeUtility.DestroyGameObjectSafe(existingByName);
            if (_nodesContainer != null) UnitySafeUtility.DestroyGameObjectSafe(_nodesContainer.gameObject);
            _nodesContainer = new GameObject("[CAVE_ORE_NODES]").transform;
            _nodesByOreType.Clear();

            // Limpiar cualquier nodo huérfano dentro de la cueva antes de generar los nuevos
            foreach (var old in FindObjectsByType<ResourceNodeController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (old != null && old.transform.position.z >= CaveDungeonBuilder.CaveEntranceZ - 1f)
                    UnitySafeUtility.DestroyGameObjectSafe(old.gameObject);
            }

            int[][] chamberByTier =
            {
                new[] { -1, -1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 },             // Coal:   Galería de entrada + Cámara 1 y Cámara 2
                new[] { 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2 },               // Copper: Cámara 1, Cámara 2 y Cámara 3
                new[] { 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4 },         // Iron:   Cámaras 2, 3, 4 y 5
                new[] { 2, 2, 3, 3, 3, 4, 4, 4, 4, 4 }                      // Gold:   Cámaras 3, 4 y 5 (Cámara 6 solo para el Titán)
            };

            int seed = 101;
            for (int t = 0; t < oreTiers.Count; t++)
            {
                var cfg = oreTiers[t];
                var list = new List<ResourceNodeController>();
                _nodesByOreType[cfg.resourceType] = list;
                int[] chMap = chamberByTier[Mathf.Clamp(t, 0, chamberByTier.Length - 1)];

                for (int i = 0; i < cfg.count; i++)
                {
                    seed += 41;
                    int chIdx = chMap[i % chMap.Length];
                    Vector3 spawnPos = ComputeGeologicalVeinClusterPosition(chIdx, i, seed);

                    var go = new GameObject($"OreVein_{cfg.resourceType}_{i}", typeof(ResourceNodeController));
                    go.transform.SetParent(_nodesContainer, false);
                    go.transform.position = spawnPos;
                    go.transform.rotation = Quaternion.Euler(0f, (seed * 53f) % 360f, 0f);

                    var node = go.GetComponent<ResourceNodeController>();
                    node.InitializeNode(cfg.resourceType, Mathf.Max(2, cfg.maxHealth / 25), cfg.dropAmount, cfg.dropAmount + 1);
                    if (cfg.resourceType == "Coal" && i == 0) FirstCoalVein = node;
                    list.Add(node);
                }
            }
        }

        private static Vector3 ComputeGeologicalVeinClusterPosition(int chIdx, int indexInTier, int seed)
        {
            if (chIdx < 0)
            {
                // Primera mena de carbón ANTES de la primera pared destruible (en lugar del nido eliminado en Z=40.5m)
                // y segunda mena introductoria justo después de la pared (Cámara 1, Z=51m).
                float ez = indexInTier == 0 ? 40.5f : 51.0f;
                float esx = CaveDungeonBuilder.GetSpineX(ez);
                float ex = esx + (indexInTier == 0 ? 2.4f : -2.6f);
                return new Vector3(ex, CaveDungeonBuilder.GetFloorY(ex, ez), ez);
            }

            if (chIdx >= 5) chIdx = 4; // Cámara 6 (Santuario Final) queda reservada exclusivamente para el combate con el Titán

            // En cada cámara (0..4): ubicar las vetas SIEMPRE en el flanco geológico opuesto a la gruta de arañas (-GetChamberExitSide),
            // garantizando que ninguna mena aparezca ni atraviese las cuevas de las arañas.
            float cz = CaveDungeonBuilder.AlcoveCenterZ[chIdx];
            CaveDungeonBuilder.GetWallWidthsAtZ(cz, out float chLw, out float chRw);
            float spiderSide = CaveDungeonBuilder.GetChamberExitSide(chIdx);
            float veinSideSign = -spiderSide; // Lado estrictamente opuesto a la gruta de arañas
            float sideSpan = veinSideSign > 0f ? chRw : chLw;

            float latRatio = Mathf.Lerp(0.25f, 0.52f, ((seed * 17 + indexInTier * 23) % 100) / 100f);
            float zOff = Mathf.Lerp(-12.5f, 12.0f, ((indexInTier * 31 + seed * 13) % 100) / 100f);

            float wz = cz + zOff;
            float wx = CaveDungeonBuilder.GetSpineX(wz) + veinSideSign * (sideSpan * latRatio);

            // Verificación estricta de distancia: mínimo 18m de cualquier nido de arañas
            Vector3 spiderNest = CaveDungeonBuilder.GetSpiderAlcoveWorldPosition(chIdx);
            Vector2 vPos = new Vector2(wx, wz), sPos = new Vector2(spiderNest.x, spiderNest.z);
            if (Vector2.Distance(vPos, sPos) < 18f)
            {
                wx = CaveDungeonBuilder.GetSpineX(wz) + veinSideSign * (sideSpan * 0.48f);
            }

            return new Vector3(wx, CaveDungeonBuilder.GetFloorY(wx, wz), wz);
        }

        public static void PurgeLegacyCampVeinsAndBosses()
        {
            var exp = GameObject.Find("[CAVE_EXPEDITION_AREA]");
            if (exp != null) UnitySafeUtility.DestroyGameObjectSafe(exp);
            var oldNodes = GameObject.Find("[CAVE_ORE_NODES]");
            if (oldNodes != null) UnitySafeUtility.DestroyGameObjectSafe(oldNodes);
            var oldEnemies = GameObject.Find("[CAVE_ECOLOGY_ENEMIES]");
            if (oldEnemies != null) UnitySafeUtility.DestroyGameObjectSafe(oldEnemies);
        }

        public void SpawnAllDefaultMineZones(bool forceRebuild = false)
        {
            SpawnAllCaveResources();
            SpawnAllCaveEnemies();
        }

        private ResourceNodeController GetRepresentativeVein(string oreType, int index)
        {
            if (_nodesByOreType.TryGetValue(oreType, out var list) && list != null && list.Count > 0)
                return list[Mathf.Clamp(index, 0, list.Count - 1)];
            return null;
        }
    }
}
