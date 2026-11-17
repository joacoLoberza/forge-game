using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Hábitats Geológicos Integrados (V10) en cada una de las cámaras de múltiples pisos:
    /// 1) Gruta Techada de Arañas (SpiderBroodHollow): sub-habitación con techo bajo de roca,
    ///    columnas en reloj de arena, capullos colgantes y telarañas tensadas en la oscuridad.
    /// 2) Santuario del Filón Madre (GolemMotherVein): veta gigante incrustada en la pared y suelo
    ///    de la que brotan los minerales y donde habitan los Gólems de ese mineral.
    /// 3) Traviesas de madera minera sobre la rampa diagonal integrada en el terreno (GetFloorY).
    /// </summary>
    public static partial class CaveDungeonBuilder
    {
        public const float ChamberDeckElevation = 5.8f;

        public static float GetChamberExitSide(int chamberIndex) => (chamberIndex % 2 == 0) ? 1f : -1f;

        /// <summary>
        /// Posición exacta del Nido 1 dentro de la Gruta Techada de Arañas (bajo el techo de roca).
        /// </summary>
        public static Vector3 GetSpiderAlcoveWorldPosition(int alcoveIndex)
        {
            int idx = Mathf.Clamp(alcoveIndex, 0, AlcoveCenterZ.Length - 1);
            float cz = AlcoveCenterZ[idx], sx = GetSpineX(cz);
            GetWallWidthsAtZ(cz, out float lw, out float rw);
            float sideSign = GetChamberExitSide(idx);
            float sideSpan = sideSign > 0f ? rw : lw;
            // En salas de subida (0,2,4) la gruta está en la parte baja frontal-derecha/izquierda; en salas de bajada (1,3) bajo el balcón
            float zOff = (idx % 2 == 0) ? -10.5f : 8.5f;
            float wz = cz + zOff;
            float wx = GetSpineX(wz) + sideSign * (sideSpan * 0.56f);
            return new Vector3(wx, GetFloorY(wx, wz), wz);
        }

        /// <summary>
        /// Posición del Nido 2 en la cavidad lateral secundaria de la cámara.
        /// </summary>
        public static Vector3 GetSecondarySpiderNestWorldPosition(int alcoveIndex)
        {
            int idx = Mathf.Clamp(alcoveIndex, 0, AlcoveCenterZ.Length - 1);
            Vector3 mainHollow = GetSpiderAlcoveWorldPosition(idx);
            float sideSign = GetChamberExitSide(idx);
            float wx = mainHollow.x - sideSign * 3.8f;
            float wz = mainHollow.z + 4.2f;
            return new Vector3(wx, GetFloorY(wx, wz), wz);
        }

        /// <summary>
        /// Posición en el Santuario del Filón Madre (donde se agrupan las vetas y Gólems, lejos de la gruta de arañas).
        /// </summary>
        public static Vector3 GetElevatedDeckWorldPosition(int chamberIndex, float lateralRatio = 0.48f, float zOffset = 3.5f)
        {
            int idx = Mathf.Clamp(chamberIndex, 0, AlcoveCenterZ.Length - 1);
            float cz = AlcoveCenterZ[idx];
            GetWallWidthsAtZ(cz, out float lw, out float rw);
            float golemSide = -GetChamberExitSide(idx);
            float sideSpan = golemSide > 0f ? rw : lw;
            float wz = cz + Mathf.Clamp(zOffset, -12f, 12f);
            float wx = GetSpineX(wz) + golemSide * (sideSpan * Mathf.Clamp(lateralRatio, 0.30f, 0.72f));
            return new Vector3(wx, GetFloorY(wx, wz), wz);
        }

        private static void BuildMultiLevelChamberPlatforms(Transform parent)
        {
            var habitatsRoot = new GameObject("ChamberIntegratedHabitats").transform;
            habitatsRoot.SetParent(parent, false);

            Material rockMat = GetCaveRockMaterial();
            Material woodMat = GetMineTimberMaterial();

            for (int i = 0; i < AlcoveCenterZ.Length; i++)
            {
                var chRoot = new GameObject($"Chamber_{i + 1}_Ecosystem").transform;
                chRoot.SetParent(habitatsRoot, false);

                BuildRampTimberPathAndCliffColumns(chRoot, i, rockMat, woodMat);
                BuildSpiderBroodHollowSubCave(chRoot, i, rockMat);
                BuildGolemMotherVeinSanctuary(chRoot, i, rockMat);
            }
        }

        private static void BuildRampTimberPathAndCliffColumns(Transform chRoot, int idx, Material rockMat, Material woodMat)
        {
            float cz = AlcoveCenterZ[idx];
            GetWallWidthsAtZ(cz, out float lw, out float rw);
            float sideSign = GetChamberExitSide(idx);
            float sideW = sideSign > 0f ? rw : lw;
            float oppW = sideSign > 0f ? lw : rw;

            // 1) Traviesas de madera minera a lo largo de la rampa diagonal integrada en GetFloorY
            const int plankCount = 14;
            for (int p = 0; p < plankCount; p++)
            {
                float t = p / (float)(plankCount - 1);
                float wz = cz + Mathf.Lerp(-15f, 15f, t);
                float sx = GetSpineX(wz);
                float wx = sx + sideSign * Mathf.Lerp(-oppW * 0.32f, sideW * 0.38f, t);
                float wy = GetFloorY(wx, wz);

                float nextZ = wz + 1.2f;
                float nextX = GetSpineX(nextZ) + sideSign * Mathf.Lerp(-oppW * 0.32f, sideW * 0.38f, Mathf.Clamp01(t + 0.06f));
                Vector3 tangent = new Vector3(nextX - wx, GetFloorY(nextX, nextZ) - wy, nextZ - wz).normalized;
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);

                CreateBeam(chRoot, $"RampTimberSleeper_{p}", new Vector3(wx, wy + 0.04f, wz),
                    new Vector3(3.4f, 0.16f, 0.42f), rot, woodMat, false);
            }

            // 2) Columnas orgánicas en forma de reloj de arena que canalizan el flujo por el desnivel
            float ceilH = GetCeilingHeightAtZ(cz);
            for (int c = 0; c < 4; c++)
            {
                float t = c / 3f;
                float wz = cz - 18f + c * 2.2f;
                float wx = GetSpineX(wz) + Mathf.Lerp(sideSign * 1.8f, sideSign * (sideW * 0.85f), t);
                BuildHourglassColumn(chRoot, $"NaturalDivideColumn_{c}", wx, wz, ceilH, 1.45f + (c % 2) * 0.35f, 3100 + idx * 71 + c * 19, rockMat);
            }
        }

        private static void BuildSpiderBroodHollowSubCave(Transform chRoot, int idx, Material rockMat)
        {
            Vector3 hollowCenter = GetSpiderAlcoveWorldPosition(idx);
            float sideSign = GetChamberExitSide(idx);
            var hollowRoot = new GameObject("SpiderBroodSubCave").transform;
            hollowRoot.SetParent(chRoot, false);
            hollowRoot.localPosition = hollowCenter;

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material webMat = new Material(s) { name = "Mat_HollowCaveWeb", color = new Color(0.78f, 0.77f, 0.72f, 1f) };
            if (webMat.HasProperty("_BaseColor")) webMat.SetColor("_BaseColor", webMat.color);

            // 1) Bóveda cavernosa de arañas con holgura vertical amplia (sin colisiones que traben a criaturas)
            for (int r = 0; r < 3; r++)
            {
                var roof = new GameObject($"SubCaveOverhangRoof_{r}", typeof(MeshFilter), typeof(MeshRenderer));
                roof.transform.SetParent(hollowRoot, false);
                roof.transform.localPosition = new Vector3((r - 1) * 2.6f, 6.2f + (r % 2) * 0.45f, r * 2.1f);
                roof.transform.localScale = new Vector3(4.8f, 0.95f, 5.2f);
                roof.transform.localRotation = Quaternion.Euler(6f * (r - 1), r * 37f, -5f * sideSign);
                Mesh chunk = OreMeshGenerator.CreateFacetedChunkMesh(1.45f, 6100 + idx * 43 + r * 13);
                roof.GetComponent<MeshFilter>().sharedMesh = chunk;
                roof.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
            }

            // 2) Pilares de entrada abiertos en los extremos para dejar un portal amplio y despejado
            for (int p = 0; p < 2; p++)
            {
                Vector3 pillarLocal = new Vector3(-sideSign * 5.4f, 0f, p == 0 ? -4.5f : 4.8f);
                SpawnRockFormation(hollowRoot, $"HollowGatePillar_{p}", pillarLocal, 1.10f, 4.8f, false, 6400 + idx * 29 + p * 17, rockMat, true);

                // Capullos de seda suspendidos en lo alto de la gruta
                var sac = new GameObject($"HangingPreyCocoon_{p}", typeof(MeshFilter), typeof(MeshRenderer));
                sac.transform.SetParent(hollowRoot, false);
                sac.transform.localPosition = new Vector3((p - 0.5f) * 2.4f, 4.85f, p * 2.2f);
                sac.transform.localScale = new Vector3(0.55f, 1.25f, 0.55f);
                sac.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.52f, 6600 + idx * 19 + p);
                sac.GetComponent<MeshRenderer>().sharedMaterial = webMat;
            }

            // Hebras de telaraña elevadas en la bóveda
            for (int w = 0; w < 6; w++)
            {
                Vector3 a = new Vector3(-sideSign * 4.2f, 4.6f + (w % 2) * 0.6f, -3.8f + w * 1.5f);
                Vector3 b = new Vector3(sideSign * 2.4f, 5.4f - (w % 3) * 0.4f, -2.5f + w * 1.3f);
                Vector3 diff = b - a;
                CreateBeam(hollowRoot, $"CaveWebStrand_{w}", (a + b) * 0.5f, new Vector3(0.045f, diff.magnitude, 0.045f),
                    Quaternion.FromToRotation(Vector3.up, diff.normalized), webMat, false);
            }
        }

        private static void BuildGolemMotherVeinSanctuary(Transform chRoot, int idx, Material rockMat)
        {
            Vector3 sanctuaryPos = GetElevatedDeckWorldPosition(idx, 0.58f, 0f);
            float golemSide = -GetChamberExitSide(idx);
            var veinRoot = new GameObject("GolemMotherVeinSanctuary").transform;
            veinRoot.SetParent(chRoot, false);
            veinRoot.localPosition = sanctuaryPos;

            // Color geológico del Filón Madre según la profundidad de la cámara (Carbón -> Cobre -> Hierro -> Oro)
            Color oreColor = idx switch
            {
                0 => new Color(0.14f, 0.14f, 0.16f),
                1 => new Color(0.76f, 0.42f, 0.20f),
                2 => new Color(0.54f, 0.58f, 0.64f),
                3 => new Color(0.62f, 0.66f, 0.72f),
                _ => new Color(0.95f, 0.76f, 0.22f)
            };
            Color glowColor = idx switch
            {
                0 => new Color(0.85f, 0.32f, 0.06f) * 0.7f,
                1 => new Color(0.18f, 0.72f, 0.56f) * 0.9f,
                2 => new Color(0.45f, 0.62f, 0.85f) * 0.9f,
                3 => new Color(0.65f, 0.75f, 0.95f) * 1.1f,
                _ => new Color(1.0f, 0.78f, 0.22f) * 1.6f
            };

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material seamMat = new Material(s) { name = $"Mat_MotherVein_{idx}", color = oreColor };
            if (seamMat.HasProperty("_BaseColor")) seamMat.SetColor("_BaseColor", oreColor);
            if (seamMat.HasProperty("_Metallic")) seamMat.SetFloat("_Metallic", idx >= 2 ? 0.88f : 0.45f);
            if (seamMat.HasProperty("_Smoothness")) seamMat.SetFloat("_Smoothness", 0.72f);
            if (seamMat.HasProperty("_EmissionColor")) { seamMat.DisableKeyword("_EMISSION"); seamMat.SetColor("_EmissionColor", Color.black); }

            // Formación rocosa madre incrustada en la pared lateral con cristales/vetas gigantes emergiendo de la roca
            for (int k = 0; k < 5; k++)
            {
                float zOff = (k - 2) * 2.8f;
                float xOff = golemSide * (1.8f + (k % 2) * 0.9f);
                SpawnRockFormation(veinRoot, $"MotherWallRock_{k}", new Vector3(xOff, -0.4f, zOff),
                    1.6f + (k % 2) * 0.5f, 4.2f + (k % 3) * 1.1f, false, 7100 + idx * 53 + k * 17, rockMat, true);
                SpawnRockFormation(veinRoot, $"EmbeddedMineralSeam_{k}", new Vector3(xOff - golemSide * 0.55f, 0.2f, zOff + 0.3f),
                    0.95f + (k % 2) * 0.35f, 2.8f + (k % 2) * 0.8f, false, 7500 + idx * 61 + k * 23, seamMat, false);
            }

            var lObj = new GameObject("MotherVeinResonanceLight", typeof(Light));
            lObj.transform.SetParent(veinRoot, false);
            lObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var l = lObj.GetComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.95f, 0.72f, 0.36f); l.range = 22f; l.intensity = 1.7f;
        }
    }
}
