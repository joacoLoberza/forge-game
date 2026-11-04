using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Orquestador de la Expedición en la Gran Caverna (Z = 28m..226m).
    /// Mantiene estrictamente 24 menas preciosas (6 Carbón, 6 Cobre, 6 Oro, 6 Hierro) repartidas
    /// entre cámaras Y corredores de conexión para evitar tramos vacíos.
    /// </summary>
    public partial class MineSpawnerController : MonoBehaviour
    {
        private const string ExpeditionVersionMarker = "OrganicExpedition_V5";

        public static MineSpawnerController Instance { get; private set; }

        [Header("Configuración General")]
        [SerializeField] private bool autoSpawnOnStart = true;
        [SerializeField] private bool spawnEnemiesInCave = true;

        [Header("Anclas Opcionales de Minerales")]
        [SerializeField] private Transform[] coalSpawnPoints;
        [SerializeField] private Transform[] copperSpawnPoints;
        [SerializeField] private Transform[] goldSpawnPoints;
        [SerializeField] private Transform[] ironSpawnPoints;
        [SerializeField] private Transform[] enemySpawnPoints;

        private readonly List<ResourceNodeController> _spawnedNodes = new();
        private readonly List<SpiderBroodNest> _broodNests = new();
        private Transform _caveContainer;
        private static bool _sceneHooked;

        private readonly struct VeinSpec
        {
            public readonly string Id; public readonly float Z; public readonly float SideRatio;
            public readonly int Hp; public readonly int MinDrop; public readonly int MaxDrop;
            public readonly bool HasGolemGenerator; public readonly float GolemDelay;
            public VeinSpec(string id, float z, float side, int hp, int minD, int maxD, bool golem, float delay = 1.5f)
            { Id = id; Z = z; SideRatio = side; Hp = hp; MinDrop = minD; MaxDrop = maxD; HasGolemGenerator = golem; GolemDelay = delay; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _sceneHooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitSceneMines()
        {
            if (!_sceneHooked) { _sceneHooked = true; SceneManager.sceneLoaded += (s, _) => EnsureSpawnerInScene(s); }
            EnsureSpawnerInScene(SceneManager.GetActiveScene());
        }

        private static void EnsureSpawnerInScene(Scene scene)
        {
            string name = scene.name;
            if (string.IsNullOrEmpty(name) || name.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0) return;
            var spawner = FindFirstObjectByType<MineSpawnerController>() ?? new GameObject("MineSpawnerController").AddComponent<MineSpawnerController>();
            spawner.SpawnAllDefaultMineZones(forceRebuild: false);
        }

        private void Awake() { if (Instance == null) Instance = this; ForjaGame.Forge.ForgeStationModelSetup.RemoveMineVisual(); }
        private void Start() { ForjaGame.Forge.ForgeStationModelSetup.RemoveMineVisual(); if (autoSpawnOnStart) SpawnAllDefaultMineZones(false); }

        public void SpawnAllDefaultMineZones(bool forceRebuild = false)
        {
            CaveDungeonBuilder.EnsureCaveBuiltInScene(forceRebuild: forceRebuild);
            PurgeLegacyCampVeinsAndBosses();

            GameObject container = GameObject.Find("[CAVE_EXPEDITION_AREA]");
            bool isOutdated = container != null && container.transform.Find(ExpeditionVersionMarker) == null;
            if (container != null && (forceRebuild || isOutdated))
            {
                UnitySafeUtility.DestroyGameObjectSafe(container);
                container = null;
            }
            if (container == null)
            {
                container = new GameObject("[CAVE_EXPEDITION_AREA]");
                new GameObject(ExpeditionVersionMarker).transform.SetParent(container.transform, false);
            }
            _caveContainer = container.transform;

            _spawnedNodes.RemoveAll(n => n == null);
            var existingNodes = container.GetComponentsInChildren<ResourceNodeController>(true);
            if (forceRebuild || isOutdated || existingNodes.Length != 24)
            {
                for (int i = existingNodes.Length - 1; i >= 0; i--) UnitySafeUtility.DestroyGameObjectSafe(existingNodes[i].gameObject);
                _spawnedNodes.Clear();
                BuildPreciousVeinsAndGolemGenerators();
            }
            else if (_spawnedNodes.Count == 0) _spawnedNodes.AddRange(existingNodes);

            if (spawnEnemiesInCave) SpawnCaveEnemies(forceRebuild || isOutdated);
        }

        private void BuildPreciousVeinsAndGolemGenerators()
        {
            VeinSpec[] specs =
            {
                new("Coal",   35.0f, -0.58f, 3, 3, 5, false),
                new("Coal",   40.8f,  0.56f, 3, 3, 5, true,  1.2f),
                new("Coal",   49.2f, -0.64f, 3, 3, 5, false),
                new("Coal",   55.0f,  0.62f, 3, 3, 5, true,  1.5f),
                new("Coal",   61.0f, -0.55f, 3, 3, 5, false),
                new("Coal",   66.5f,  0.58f, 3, 3, 5, false),
                new("Copper", 74.8f, -0.56f, 3, 2, 4, true,  1.2f),
                new("Copper", 81.8f,  0.56f, 3, 2, 4, false),
                new("Copper", 92.5f, -0.66f, 3, 2, 4, false),
                new("Copper", 99.8f,  0.64f, 3, 2, 4, true,  1.5f),
                new("Copper", 108.0f,-0.60f, 3, 2, 4, false),
                new("Copper", 115.2f, 0.58f, 3, 2, 4, true,  1.8f),
                new("Gold",   127.5f,-0.55f, 4, 2, 3, false),
                new("Gold",   137.8f, 0.56f, 4, 2, 3, true,  1.2f),
                new("Gold",   147.2f,-0.56f, 4, 2, 3, false),
                new("Gold",   159.5f, 0.65f, 4, 2, 3, true,  1.6f),
                new("Gold",   168.8f,-0.64f, 4, 2, 3, false),
                new("Gold",   177.5f, 0.60f, 4, 2, 3, true,  1.8f),
                new("Iron",   187.2f,-0.55f, 5, 2, 4, false),
                new("Iron",   192.8f, 0.56f, 5, 2, 4, true,  1.2f),
                new("Iron",   197.8f,-0.55f, 5, 2, 4, false),
                new("Iron",   203.5f,-0.62f, 5, 2, 4, false),
                new("Iron",   208.5f, 0.62f, 5, 2, 4, true,  1.5f),
                new("Iron",   211.2f,-0.52f, 5, 2, 4, false)
            };

            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                float sx = CaveDungeonBuilder.GetSpineX(s.Z);
                CaveDungeonBuilder.GetWallWidthsAtZ(s.Z, out float lw, out float rw);
                float x = sx + s.SideRatio * (s.SideRatio < 0f ? lw : rw);
                Vector3 pos = new Vector3(x, CaveDungeonBuilder.GetFloorY(x, s.Z) + 0.04f, s.Z);

                GameObject veinObj = SpawnVein(pos, s.Id, s.Hp, s.MinDrop, s.MaxDrop, needPickaxe: true);
                if (_caveContainer != null) veinObj.transform.SetParent(_caveContainer, true);

                if (s.HasGolemGenerator && veinObj.TryGetComponent<ResourceNodeController>(out var node))
                {
                    var gen = veinObj.AddComponent<CaveCreatureGenerator>();
                    int hp = 260 + Mathf.RoundToInt((s.Z - 35f) * 0.65f);
                    int dmg = 22 + Mathf.RoundToInt((s.Z - 35f) * 0.05f);
                    float scale = Mathf.Lerp(0.92f, 1.08f, (s.Z - 35f) / 180f);
                    gen.ConfigureVeinGolemGenerator(node, _caveContainer, s.GolemDelay, 38f, hp, dmg, scale, s.Z - 22f, s.Z + 22f);
                }
            }
        }

        public static void PurgeLegacyCampVeinsAndBosses()
        {
            UnitySafeUtility.DestroyGameObjectSafe(GameObject.Find("[TESTING_MINERAL_VEINS]"));
            UnitySafeUtility.DestroyGameObjectSafe(GameObject.Find("GolemEnemy_Boss"));
            var stray = GameObject.Find("Mineral");
            if (stray != null && stray.GetComponent<ResourceNodeController>() != null) UnitySafeUtility.DestroyGameObjectSafe(stray);
            foreach (var n in FindObjectsByType<ResourceNodeController>(FindObjectsSortMode.None))
                if (n != null && n.transform.position.z < 22f) UnitySafeUtility.DestroyGameObjectSafe(n.gameObject);
        }

        public static GameObject SpawnVein(Vector3 position, string itemId, int health = 3, int minDrop = 2, int maxDrop = 4, bool needPickaxe = true)
        {
            GameObject nodeObj = new GameObject($"Vein_{itemId}", typeof(BoxCollider));
            nodeObj.transform.position = position;
            var node = nodeObj.AddComponent<ResourceNodeController>();
            node.InitializeNode(itemId, health, minDrop, maxDrop, needPickaxe);
            node.EnsureSolidCollider();
            node.EnsureVisuals();
            if (Instance != null) Instance._spawnedNodes.Add(node);
            return nodeObj;
        }
    }
}
