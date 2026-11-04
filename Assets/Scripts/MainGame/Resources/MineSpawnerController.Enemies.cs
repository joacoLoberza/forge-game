using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Gestión de generadores de arañas, nidos de crías y oleadas de invocación ritual del Santuario.
    /// </summary>
    public partial class MineSpawnerController
    {
        public void SpawnCaveEnemies(bool forceRebuild = false)
        {
            foreach (var g in FindObjectsByType<GolemEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g != null) UnitySafeUtility.DestroyGameObjectSafe(g.gameObject);
            foreach (var sp in FindObjectsByType<SpiderEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (sp != null) UnitySafeUtility.DestroyGameObjectSafe(sp.gameObject);

            _broodNests.RemoveAll(n => n == null);
            var existingNests = _caveContainer != null ? _caveContainer.GetComponentsInChildren<SpiderBroodNest>(true) : System.Array.Empty<SpiderBroodNest>();
            if (!forceRebuild && existingNests.Length >= 8)
            {
                foreach (var gen in CaveCreatureGenerator.ActiveGenerators) gen.ResetGeneratorTimer();
                return;
            }

            for (int i = existingNests.Length - 1; i >= 0; i--) UnitySafeUtility.DestroyGameObjectSafe(existingNests[i].gameObject);
            _broodNests.Clear();

            (float z, float side, float delay, int count, int hp, int dmg, float span)[] nests =
            {
                (38.2f,  0.48f,  9f, 1, 115, 12, 1.72f),
                (52.5f, -0.60f, 11f, 1, 125, 13, 1.78f),
                (63.8f,  0.54f, 15f, 1, 130, 14, 1.80f),
                (78.8f,  0.46f, 10f, 1, 140, 15, 1.85f),
                (96.0f,  0.62f, 12f, 1, 150, 16, 1.90f),
                (111.5f,-0.58f, 16f, 1, 155, 16, 1.92f),
                (132.0f,-0.48f,  9f, 1, 165, 17, 1.95f),
                (144.8f, 0.48f, 14f, 1, 170, 18, 1.98f),
                (170.5f,-0.62f, 12f, 2, 180, 19, 2.02f),
                (190.2f,-0.46f, 10f, 1, 185, 19, 2.05f),
                (205.8f, 0.56f, 13f, 1, 195, 20, 2.08f)
            };

            for (int i = 0; i < nests.Length; i++)
            {
                var n = nests[i];
                float sx = CaveDungeonBuilder.GetSpineX(n.z);
                CaveDungeonBuilder.GetWallWidthsAtZ(n.z, out float lw, out float rw);
                float x = sx + n.side * (n.side < 0f ? lw : rw);
                Vector3 pos = new Vector3(x, CaveDungeonBuilder.GetFloorY(x, n.z) + 0.02f, n.z);

                var nestObj = new GameObject($"SpiderBroodNest_{i + 1}");
                if (_caveContainer != null) nestObj.transform.SetParent(_caveContainer, true);
                nestObj.transform.position = pos;
                var nest = nestObj.AddComponent<SpiderBroodNest>();
                var gen = nestObj.AddComponent<CaveCreatureGenerator>();
                gen.ConfigureBroodSpiderGenerator(nest, _caveContainer, n.delay, 34f, n.count, n.hp, n.dmg, n.span, n.z - 24f, n.z + 24f);
                _broodNests.Add(nest);
            }
        }

        public static void AwakenSanctumRitualWave(Vector3 altarPos)
        {
            Transform parent = Instance != null ? Instance._caveContainer : null;
            Transform target = CaveCreatureBase.FindClosestLivingPlayer(altarPos);

            Vector3 golemPos = new Vector3(altarPos.x, CaveDungeonBuilder.GetFloorY(altarPos.x, altarPos.z - 7.5f) + 0.05f, altarPos.z - 7.5f);
            var gObj = new GameObject("SanctumRitualGolem");
            if (parent != null) gObj.transform.SetParent(parent, true);
            gObj.transform.position = golemPos;
            var golem = gObj.AddComponent<GolemEnemyController>();
            golem.ConfigureGolem(golemPos, 340, 26, 1.08f, 196f, 224f);
            golem.ForceRitualAggro(target);

            float[] sideX = { -4.4f, 4.4f };
            for (int i = 0; i < sideX.Length; i++)
            {
                float sx = altarPos.x + sideX[i], sz = altarPos.z - 5.2f;
                Vector3 sPos = new Vector3(sx, CaveDungeonBuilder.GetFloorY(sx, sz) + 0.05f, sz);
                var sObj = new GameObject($"SanctumRitualSpider_{i + 1}");
                if (parent != null) sObj.transform.SetParent(parent, true);
                sObj.transform.position = sPos;
                var spider = sObj.AddComponent<SpiderEnemyController>();
                spider.ConfigureSpider(sPos, 175, 18, 2.0f, 196f, 224f);
                spider.ForceRitualAggro(target);
            }
        }

        public void ResetAndRespawnCaveEnemies()
        {
            FindFirstObjectByType<ConsecrationAltarInteractable>(FindObjectsInactive.Include)?.ResetAltarState();
            foreach (var g in FindObjectsByType<GolemEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g != null) UnitySafeUtility.DestroyGameObjectSafe(g.gameObject);
            foreach (var s in FindObjectsByType<SpiderEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null) UnitySafeUtility.DestroyGameObjectSafe(s.gameObject);
            foreach (var gen in CaveCreatureGenerator.ActiveGenerators)
                if (gen != null) gen.ResetGeneratorTimer();
        }
    }
}

