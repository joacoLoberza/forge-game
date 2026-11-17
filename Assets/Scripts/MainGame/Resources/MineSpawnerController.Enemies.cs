using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Distribución ecológica reforzada de Gólems y Arañas en las 6 cámaras (Z = 28..560m):
    /// - 29 Gólems repartidos en todas las cámaras (3 en Ch1, 4 en Ch2, 5 en Ch3, 5 en Ch4, 6 en Ch5
    ///   y 6 Gólems custodiando el Santuario Final en Ch6).
    /// - 16 Nidos de Arañas (SpiderBroodNest) y 38 Arañas repartidas tanto DENTRO de las cámaras
    ///   (bajo las cornisas y en rincones oscuros laterales) como en los túneles intermedios.
    /// </summary>
    public partial class MineSpawnerController
    {
        public void SpawnAllCaveEnemies()
        {
            var existingByName = GameObject.Find("[CAVE_ECOLOGY_ENEMIES]");
            if (existingByName != null) UnitySafeUtility.DestroyGameObjectSafe(existingByName);
            if (_enemiesContainer != null) UnitySafeUtility.DestroyGameObjectSafe(_enemiesContainer.gameObject);
            _enemiesContainer = new GameObject("[CAVE_ECOLOGY_ENEMIES]").transform;

            PurgeLegacySceneEnemies();
            ConsecrationAltarInteractable.LockAndHideSanctumAltar();
            SpawnVeinGuardianGolems();
            SpawnSanctumTitanBoss();
            SpawnAlcoveAndTunnelSpiders();
            SpawnWallGuardians();
        }

        private void PurgeLegacySceneEnemies()
        {
            foreach (var t in FindObjectsByType<TitanBossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && (_enemiesContainer == null || !t.transform.IsChildOf(_enemiesContainer)))
                    UnitySafeUtility.DestroyGameObjectSafe(t.gameObject);

            foreach (var g in FindObjectsByType<GolemEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g != null && (_enemiesContainer == null || !g.transform.IsChildOf(_enemiesContainer)))
                    UnitySafeUtility.DestroyGameObjectSafe(g.gameObject);

            foreach (var s in FindObjectsByType<SpiderEnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s != null && (_enemiesContainer == null || !s.transform.IsChildOf(_enemiesContainer)))
                    UnitySafeUtility.DestroyGameObjectSafe(s.gameObject);
        }

        private void SpawnSanctumTitanBoss()
        {
            float tz = CaveDungeonBuilder.VictoryAltarZ - 8.5f;
            float tx = CaveDungeonBuilder.GetSpineX(tz);
            Vector3 titanPos = new Vector3(tx, CaveDungeonBuilder.GetFloorY(tx, tz), tz);

            var go = new GameObject("TitanBoss_SanctumOverlord", typeof(TitanBossController));
            go.transform.SetParent(_enemiesContainer, false);
            go.GetComponent<TitanBossController>().ConfigureTitan(titanPos, 496f, 554f);
        }

        private void SpawnVeinGuardianGolems()
        {
            // CÁMARA 1 (Z = 44..118m): 0 Gólems. Cueva inicial reservada exclusivamente para arañas comunes/scouts.

            // CÁMARA 2 (Z = 118..212m): Primeros Gólems (Menores líticos de Carbón y Cobre - tamaño colosal aumentado)
            SpawnBoundGolem("Golem_Minor_Ch2_A", GetRepresentativeVein("Coal", 6),    95,  8, 1.38f, 118f, 212f, GolemTier.MinorVein);
            SpawnBoundGolem("Golem_Minor_Ch2_B", GetRepresentativeVein("Copper", 2), 105,  9, 1.42f, 118f, 212f, GolemTier.MinorVein);

            // CÁMARA 3 (Z = 212..306m): Introducción de Gólems Guardianes Férreos (blindados, custodian Hierro)
            SpawnBoundGolem("Golem_Iron_Ch3_A", GetRepresentativeVein("Iron", 2), 160, 12, 1.62f, 212f, 306f, GolemTier.IronGuardian);
            SpawnBoundGolem("Golem_Iron_Ch3_B", GetRepresentativeVein("Iron", 3), 175, 13, 1.66f, 212f, 306f, GolemTier.IronGuardian);

            // CÁMARA 4 (Z = 306..402m): Introducción del primer Coloso Élite (Oro) + Guardianes Férreos
            SpawnBoundGolem("Golem_Colossus_Ch4_A", GetRepresentativeVein("Gold", 2), 250, 16, 1.95f, 306f, 402f, GolemTier.EliteColossus);
            SpawnBoundGolem("Golem_Iron_Ch4_B",     GetRepresentativeVein("Iron", 6), 180, 13, 1.62f, 306f, 402f, GolemTier.IronGuardian);
            SpawnBoundGolem("Golem_Iron_Ch4_C",     GetRepresentativeVein("Iron", 7), 190, 14, 1.65f, 306f, 402f, GolemTier.IronGuardian);

            // CÁMARA 5 (Z = 402..496m): Desafío Pre-Santuario: Doble Coloso Élite de Oro + Guardián Férreo
            SpawnBoundGolem("Golem_Colossus_Ch5_A", GetRepresentativeVein("Gold", 6),  270, 17, 2.05f, 402f, 496f, GolemTier.EliteColossus);
            SpawnBoundGolem("Golem_Colossus_Ch5_B", GetRepresentativeVein("Gold", 7),  280, 17, 2.05f, 402f, 496f, GolemTier.EliteColossus);
            SpawnBoundGolem("Golem_Iron_Ch5_C",     GetRepresentativeVein("Iron", 10), 195, 14, 1.68f, 402f, 496f, GolemTier.IronGuardian);
        }

        private void SpawnBoundGolem(string name, ResourceNodeController vein, int hp, int dmg, float scale, float zMin, float zMax, GolemTier tier)
        {
            Vector3 pos;
            if (vein != null)
            {
                float vz = vein.transform.position.z;
                float sx = CaveDungeonBuilder.GetSpineX(vz);
                float gx = Mathf.Lerp(vein.transform.position.x, sx, 0.60f);
                float gz = vz - 1.2f;
                float gy = CaveDungeonBuilder.GetFloorY(gx, gz);
                pos = new Vector3(gx, gy, gz);
            }
            else
            {
                float midZ = (zMin + zMax) * 0.5f;
                float sx = CaveDungeonBuilder.GetSpineX(midZ);
                pos = new Vector3(sx, CaveDungeonBuilder.GetFloorY(sx, midZ), midZ);
            }
            CreateGolemInstance(name, pos, hp, dmg, scale, zMin, zMax, vein, tier);
        }

        private void CreateGolemInstance(
            string name, Vector3 pos, int hp, int dmg, float scale,
            float zMin, float zMax, ResourceNodeController vein, GolemTier tier)
        {
            var go = new GameObject(name, typeof(GolemEnemyController));
            go.transform.SetParent(_enemiesContainer, false);
            go.GetComponent<GolemEnemyController>().ConfigureGolem(pos, hp, dmg, scale, zMin, zMax, vein, tier);
        }

        private void SpawnAlcoveAndTunnelSpiders()
        {
            float[] chMinZ = { 44f, 118f, 212f, 306f, 402f };
            float[] chMaxZ = { 118f, 212f, 306f, 402f, 496f };

            // 1) Cámara 1: Nido de Seda Inicial
            Vector3 ledge1 = CaveDungeonBuilder.GetSpiderAlcoveWorldPosition(0);
            var nest1 = CreateSpiderNest("SpiderBroodNest_Ch1_Main", ledge1);
            Vector3 cPos1 = GetAlcoveClearancePos(ledge1, 0);
            CreateSpider("Spider_Common_Ch1_A", cPos1 + new Vector3(-1.2f, 0f, 0f), 45, 6, 2.45f, chMinZ[0], chMaxZ[0], nest1, SpiderTier.Common);
            CreateSpider("Spider_Common_Ch1_B", cPos1 + new Vector3(1.2f, 0f, 0f),  50, 6, 2.50f, chMinZ[0], chMaxZ[0], nest1, SpiderTier.Common);
            CreateSpider("Spider_Scout_Ch1_C",  cPos1 + new Vector3(0f, 0f, -1.8f), 25, 4, 1.65f, chMinZ[0], chMaxZ[0], nest1, SpiderTier.Scout);

            // 2) Cámara 2: Nido moderado
            Vector3 ledge2 = CaveDungeonBuilder.GetSpiderAlcoveWorldPosition(1);
            var nest2 = CreateSpiderNest("SpiderBroodNest_Ch2_Main", ledge2);
            Vector3 cPos2 = GetAlcoveClearancePos(ledge2, 1);
            CreateSpider("Spider_Common_Ch2_A", cPos2 + new Vector3(-1.0f, 0f, 0f), 50, 6, 2.55f, chMinZ[1], chMaxZ[1], nest2, SpiderTier.Common);
            CreateSpider("Spider_Scout_Ch2_B",  cPos2 + new Vector3(1.0f, 0f, 0f),  28, 4, 1.70f, chMinZ[1], chMaxZ[1], nest2, SpiderTier.Scout);

            // 3) Cámara 3: Araña Matriarca Élite generada en terreno despejado frente al nido
            Vector3 ledge3 = CaveDungeonBuilder.GetSpiderAlcoveWorldPosition(2);
            var nest3 = CreateSpiderNest("SpiderBroodNest_Ch3_Main", ledge3);
            Vector3 cPos3 = GetAlcoveClearancePos(ledge3, 2);
            CreateSpider("Spider_Matriarch_Ch3", cPos3, 140, 11, 3.80f, chMinZ[2], chMaxZ[2], nest3, SpiderTier.EliteMatriarch);
            CreateSpider("Spider_Common_Ch3_A",  cPos3 + new Vector3(-1.8f, 0f, -1.2f), 55, 7, 2.60f, chMinZ[2], chMaxZ[2], nest3, SpiderTier.Common);

            // 4) Cámara 4: Nido en la cornisa de oro
            Vector3 ledge4 = CaveDungeonBuilder.GetSpiderAlcoveWorldPosition(3);
            var nest4 = CreateSpiderNest("SpiderBroodNest_Ch4_Main", ledge4);
            Vector3 cPos4 = GetAlcoveClearancePos(ledge4, 3);
            CreateSpider("Spider_Common_Ch4_A", cPos4 + new Vector3(-1.1f, 0f, 0f), 55, 7, 2.60f, chMinZ[3], chMaxZ[3], nest4, SpiderTier.Common);
            CreateSpider("Spider_Scout_Ch4_B",  cPos4 + new Vector3(1.1f, 0f, 0f),  30, 4, 1.75f, chMinZ[3], chMaxZ[3], nest4, SpiderTier.Scout);

            // 5) Cámara 5: Nido pre-jefe con Gran Matriarca Élite en terreno abierto
            Vector3 ledge5 = CaveDungeonBuilder.GetSpiderAlcoveWorldPosition(4);
            var nest5 = CreateSpiderNest("SpiderBroodNest_Ch5_Main", ledge5);
            Vector3 cPos5 = GetAlcoveClearancePos(ledge5, 4);
            CreateSpider("Spider_Matriarch_Ch5", cPos5, 155, 12, 4.05f, chMinZ[4], chMaxZ[4], nest5, SpiderTier.EliteMatriarch);
            CreateSpider("Spider_Common_Ch5_A",  cPos5 + new Vector3(-1.8f, 0f, -1.0f), 60, 7, 2.65f, chMinZ[4], chMaxZ[4], nest5, SpiderTier.Common);
        }

        private static Vector3 GetAlcoveClearancePos(Vector3 ledge, int chIdx)
        {
            float sx = CaveDungeonBuilder.GetSpineX(ledge.z);
            float gx = Mathf.Lerp(ledge.x, sx, 0.40f);
            float gz = ledge.z - 2.8f;
            return new Vector3(gx, CaveDungeonBuilder.GetFloorY(gx, gz), gz);
        }

        private void SpawnWallGuardians()
        {
            // Antes de la 1ra pared destruible (Z = 44m): SIN spawner de arañas; solo la primera araña (junto a la 1ra mena de carbón).
            FirstDoorSpider = SpawnFirstEntranceSpider(44f, 45, 5, 2.45f, SpiderTier.Common);
            SpawnDoorSpider(118f, 2, 50, 6, 2.50f, SpiderTier.Common);
            SpawnDoorGolem("Golem_Guardian_Rock_1", 212f, 185, 13, 1.70f, GolemTier.IronGuardian);
            SpawnDoorSpider(306f, 4, 55, 7, 2.60f, SpiderTier.Common);
            SpawnDoorSpider(402f, 5, 55, 7, 2.60f, SpiderTier.Common);
            SpawnDoorGolem("Golem_Guardian_Rock_2", 496f, 260, 16, 1.95f, GolemTier.EliteColossus);
        }

        private SpiderEnemyController SpawnFirstEntranceSpider(float wallZ, int hp, int dmg, float span, SpiderTier tier)
        {
            float gz = wallZ - 6.2f, gx = CaveDungeonBuilder.GetSpineX(gz);
            Vector3 spiderPos = new Vector3(gx - 1.1f, CaveDungeonBuilder.GetFloorY(gx - 1.1f, gz), gz);
            var go = new GameObject("Spider_Door_1", typeof(SpiderEnemyController));
            go.transform.SetParent(_enemiesContainer, false);
            var sp = go.GetComponent<SpiderEnemyController>();
            sp.ConfigureSpider(spiderPos, hp, dmg, span, 29.5f, wallZ - 0.8f, null, tier);
            var dropper = sp.GetComponent<EnemyLootDropper>() ?? sp.gameObject.AddComponent<EnemyLootDropper>();
            dropper.SetLootTable(new List<LootDropEntry> { new("Iron", 0.70f, 1, 2), new("Copper", 0.50f, 1, 2) });
            return sp;
        }

        private void SpawnDoorSpider(float wallZ, int id, int hp, int dmg, float span, SpiderTier tier)
        {
            float gz = wallZ - 4.5f, gx = CaveDungeonBuilder.GetSpineX(gz);
            Vector3 nestPos = new Vector3(gx + 2.4f, CaveDungeonBuilder.GetFloorY(gx + 2.4f, gz), gz);
            Vector3 spiderPos = new Vector3(gx - 1.2f, CaveDungeonBuilder.GetFloorY(gx - 1.2f, gz), gz);
            var nest = CreateSpiderNest($"SpiderNest_Door_{id}", nestPos);
            CreateSpider($"Spider_Door_{id}", spiderPos, hp, dmg, span, wallZ - 25f, wallZ + 5f, nest, tier);
        }

        private void SpawnDoorGolem(string name, float wallZ, int hp, int dmg, float scale, GolemTier tier)
        {
            float gz = wallZ - 4.5f, gx = CaveDungeonBuilder.GetSpineX(gz);
            Vector3 pos = new Vector3(gx, CaveDungeonBuilder.GetFloorY(gx, gz), gz);
            CreateGolemInstance(name, pos, hp, dmg, scale, wallZ - 25f, wallZ + 5f, null, tier);
        }

        private SpiderBroodNest CreateSpiderNest(string name, Vector3 pos)
        {
            var go = new GameObject(name, typeof(SpiderBroodNest));
            go.transform.SetParent(_enemiesContainer, false);
            var nest = go.GetComponent<SpiderBroodNest>();
            nest.InitializeNest(pos);
            return nest;
        }

        private void CreateSpider(
            string name, Vector3 pos, int hp, int dmg, float span,
            float zMin, float zMax, SpiderBroodNest nest, SpiderTier tier)
        {
            var go = new GameObject(name, typeof(SpiderEnemyController));
            go.transform.SetParent(_enemiesContainer, false);
            go.GetComponent<SpiderEnemyController>().ConfigureSpider(pos, hp, dmg, span, zMin, zMax, nest, tier);
        }

        public void ResetAndRespawnCaveEnemies() => SpawnAllCaveEnemies();

        public static void AwakenSanctumRitualWave(Vector3 altarPos)
        {
            Transform target = CaveCreatureBase.FindClosestLivingPlayer(altarPos);
            float rSq = 48f * 48f;
            var golems = GolemEnemyController.ActiveGolems;
            for (int i = 0; i < golems.Count; i++)
                if (golems[i] != null && golems[i].IsAlive && (golems[i].transform.position - altarPos).sqrMagnitude <= rSq)
                    golems[i].ForceRitualAggro(target);
            var spiders = SpiderEnemyController.ActiveSpiders;
            for (int i = 0; i < spiders.Count; i++)
                if (spiders[i] != null && spiders[i].IsAlive && (spiders[i].transform.position - altarPos).sqrMagnitude <= rSq)
                    spiders[i].ForceRitualAggro(target);
        }
    }
}
