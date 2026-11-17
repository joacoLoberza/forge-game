using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Constructor procedural 3D de la Mina Subterránea Viva (V10):
    /// - 6 Cámaras independientes conectadas en ángulos laterales reales (±55° a ±70°).
    /// - Desniveles reales integrados en el propio terreno (GetFloorY): las salas impares (1, 3, 5)
    ///   suben de una planta baja a un 2do piso donde nace el túnel lateral; las salas pares (2, 4)
    ///   entran por un balcón alto y descienden por una rampa a una fosa profunda donde está la salida.
    /// </summary>
    public static partial class CaveDungeonBuilder
    {
        public const float CaveEntranceZ = 28.0f;
        public const float CaveEndZ = 560.0f;
        public const float VictoryAltarZ = 532.0f;
        public const string CaveVersionMarker = "OrganicCavern_V16";

        public static readonly float[] ChamberCenterZ = { 75f, 165f, 260f, 355f, 450f, 532f };
        public static readonly float[] AlcoveCenterZ = { 75f, 165f, 260f, 355f, 450f };
        public static readonly float[] ChamberEntryY = { -1.2f, +4.6f, -2.4f, +4.2f, -3.2f, +3.2f };
        public static readonly float[] ChamberExitY  = { +4.6f, -2.4f, +4.2f, -3.2f, +3.2f, +3.2f };

        private static Material _caveRockMat, _caveFloorMat, _mineTimberMat;

        public static GameObject EnsureCaveBuiltInScene(bool forceRebuild = false)
        {
            GameObject existing = GameObject.Find("[CAVE_STRUCTURE]");
            if (existing != null && !forceRebuild && existing.transform.Find(CaveVersionMarker) != null)
            {
                var walls = existing.transform.Find("DestructibleTunnelWalls");
                if (walls != null) UnitySafeUtility.DestroyGameObjectSafe(walls.gameObject);
                BuildDestructibleTunnelWalls(existing.transform);
                ConfigureVictoryAndDefeatTriggers(existing.transform);
                return existing;
            }
            if (existing != null) UnitySafeUtility.DestroyGameObjectSafe(existing);

            GameObject root = new GameObject("[CAVE_STRUCTURE]");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            new GameObject(CaveVersionMarker).transform.SetParent(root.transform, false);

            BuildOrganicCaveFloor(root.transform);
            BuildCaveVaultShell(root.transform);
            BuildEntranceFacadeAndPortal(root.transform);
            BuildMultiLevelChamberPlatforms(root.transform);
            BuildCaveTorchesAndCrystals(root.transform);
            BuildDestructibleTunnelWalls(root.transform);
            BuildVictorySanctumAltar(root.transform);
            ConfigureVictoryAndDefeatTriggers(root.transform);
            return root;
        }

        private static float Gauss(float z, float center, float sigma)
        {
            float d = (z - center) / sigma;
            return Mathf.Exp(-d * d);
        }

        public static float GetSpineX(float z)
        {
            if (z <= CaveEntranceZ + 1.5f) return 0f;
            float w = Mathf.SmoothStep(0f, 1f, (z - (CaveEntranceZ + 1.5f)) / 14.0f);
            float endTaper = 1f - Mathf.SmoothStep(0f, 1f, (z - 492f) / 30f);

            float curve = -28.0f * Gauss(z,  75f, 24.0f)
                        +  38.0f * Gauss(z, 165f, 25.0f)
                        -  40.0f * Gauss(z, 260f, 26.0f)
                        +  36.0f * Gauss(z, 355f, 25.0f)
                        -  36.0f * Gauss(z, 450f, 26.0f)
                        +  14.0f * Gauss(z, 500f, 18.0f);
            return curve * w * endTaper;
        }

        public static float GetSpineFloorY(float z)
        {
            if (z <= CaveEntranceZ + 0.5f) return 0.045f;
            float y = 0.045f + Mathf.SmoothStep(0f, 1f, (z - 29f) / 16f) * ChamberEntryY[0];
            for (int i = 0; i < AlcoveCenterZ.Length; i++)
            {
                float cz = AlcoveCenterZ[i];
                float tRoom = Mathf.SmoothStep(0f, 1f, (z - (cz - 18f)) / 36f);
                y += (ChamberExitY[i] - ChamberEntryY[i]) * tRoom;
            }
            return y + 1.35f * Gauss(z, VictoryAltarZ, 10.5f);
        }

        public static void GetWallWidthsAtZ(float z, out float leftWidth, out float rightWidth)
        {
            float chambers = 13.5f * Gauss(z, 75f, 22.0f) + 18.0f * Gauss(z, 165f, 24.0f)
                           + 21.0f * Gauss(z, 260f, 25.5f) + 17.5f * Gauss(z, 355f, 24.0f)
                           + 20.0f * Gauss(z, 450f, 25.0f) + 16.5f * Gauss(z, 532f, 22.0f);
            float lobeL = (Mathf.Sin(z * 0.12f + 1.1f) * 1.6f + Mathf.Cos(z * 0.25f) * 0.8f) * Mathf.Clamp01((z - 36f) / 10f);
            float lobeR = (Mathf.Cos(z * 0.11f + 2.3f) * 1.6f + Mathf.Sin(z * 0.22f) * 0.8f) * Mathf.Clamp01((z - 36f) / 10f);
            float endPinch = 1f - 0.65f * Mathf.SmoothStep(0f, 1f, (z - 548f) / 11.0f);
            float baseW = 5.40f + chambers + 1.2f * Gauss(z, CaveEntranceZ, 4.0f);

            leftWidth = Mathf.Max(4.4f, (baseW + lobeL) * endPinch);
            rightWidth = Mathf.Max(4.4f, (baseW + lobeR) * endPinch);
        }

        public static float GetHalfWidthAtZ(float z)
        {
            GetWallWidthsAtZ(z, out float lw, out float rw);
            return (lw + rw) * 0.5f;
        }

        public static float GetFloorY(float x, float z)
        {
            if (z <= CaveEntranceZ + 0.8f) return 0.056f;
            float sx = GetSpineX(z);
            GetWallWidthsAtZ(z, out float lw, out float rw);
            float lx = x - sx;

            float yBase = GetSpineFloorY(z);
            for (int i = 0; i < AlcoveCenterZ.Length; i++)
            {
                float cz = AlcoveCenterZ[i], dz = z - cz;
                if (Mathf.Abs(dz) <= 28f)
                {
                    float roomBlend = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Max(0f, (Mathf.Abs(dz) - 17f) / 11f));
                    float sideSign = GetChamberExitSide(i);
                    float halfSpan = Mathf.Max(8f, sideSign > 0f ? rw : lw);
                    float diagProgress = ((dz + 14f) / 28f) * 0.58f + ((lx * sideSign) / halfSpan + 0.45f) * 0.42f;
                    float terraceT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(diagProgress));
                    float twoStoryY = 0.045f + Mathf.Lerp(ChamberEntryY[i], ChamberExitY[i], terraceT);
                    yBase = Mathf.Lerp(yBase, twoStoryY, roomBlend);
                    break;
                }
            }

            float norm = Mathf.Clamp01(Mathf.Abs(lx) / Mathf.Max(4.0f, lx < 0f ? lw : rw));
            float bank = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (norm - 0.80f) / 0.20f)) * 0.65f;
            float microRoughness = (Mathf.PerlinNoise(x * 0.08f + 17f, z * 0.08f + 9f) - 0.5f) * 0.26f * norm;
            float fade = Mathf.Clamp01((z - 32f) / 8f) * (1f - 0.85f * Gauss(z, VictoryAltarZ, 10.0f));
            return yBase + (bank + microRoughness) * fade;
        }

        public static float GetCeilingHeightAtZ(float z)
        {
            float dome =  9.5f * Gauss(z, 75f, 24f) + 14.5f * Gauss(z, 165f, 26f)
                       + 18.5f * Gauss(z, 260f, 28f) + 16.5f * Gauss(z, 355f, 26f)
                       + 20.5f * Gauss(z, 450f, 27f) + 17.5f * Gauss(z, 532f, 23f);
            float wave = Mathf.Sin(z * 0.09f) * 0.85f * Mathf.Clamp01((z - 35f) / 8f);
            float endDome = 1f - 0.45f * Mathf.SmoothStep(0f, 1f, (z - 548f) / 11.0f);
            return (9.5f + dome + wave) * endDome;
        }

        private static void BuildOrganicCaveFloor(Transform parent)
        {
            const int zSteps = 360, xSteps = 36;
            List<Vector3> verts = new(); List<Vector2> uvs = new(); List<int> tris = new();

            // Fila frontal (-1) ancla el umbral rocoso bajo el pasto sin z-fighting ni grietas
            for (int iz = -1; iz <= zSteps; iz++)
            {
                float z = iz < 0 ? CaveEntranceZ - 0.56f : Mathf.Lerp(CaveEntranceZ - 0.35f, CaveEndZ + 2.0f, iz / (float)zSteps);
                float sx = GetSpineX(z);
                GetWallWidthsAtZ(z, out float lw, out float rw);
                for (int ix = 0; ix <= xSteps; ix++)
                {
                    float x = sx + Mathf.Lerp(-(lw + 2.4f), rw + 2.4f, ix / (float)xSteps);
                    verts.Add(new Vector3(x, iz < 0 ? -0.18f : GetFloorY(x, z), z));
                    uvs.Add(new Vector2(x * 0.16f, z * 0.16f));
                }
            }

            int stride = xSteps + 1;
            for (int iz = 0; iz < zSteps + 1; iz++)
            {
                for (int ix = 0; ix < xSteps; ix++)
                {
                    int i0 = iz * stride + ix, i1 = i0 + 1, i2 = (iz + 1) * stride + ix, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            Mesh floorMesh = BuildFlatRockMesh(verts, uvs, tris, "OrganicCaveFloorMesh_V11");
            var floorObj = new GameObject("CaveSculptedRockFloor", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            floorObj.transform.SetParent(parent, false);
            floorObj.GetComponent<MeshFilter>().sharedMesh = floorMesh;
            floorObj.GetComponent<MeshRenderer>().sharedMaterial = GetCaveFloorMaterial();
            floorObj.GetComponent<MeshCollider>().sharedMesh = floorMesh;
        }

        private static void BuildCaveVaultShell(Transform parent)
        {
            const int rings = 300, arcPts = 25;
            List<Vector3> verts = new(); List<Vector2> uvs = new(); List<int> tris = new();

            for (int r = 0; r <= rings; r++)
            {
                float z = Mathf.Lerp(CaveEntranceZ, CaveEndZ, r / (float)rings);
                float sx = GetSpineX(z), baseY = Mathf.Min(GetSpineFloorY(z), -4.5f);
                float entranceBlend = Mathf.SmoothStep(0f, 1f, (z - CaveEntranceZ) / 18.0f);
                float ceilH = Mathf.Lerp(5.75f, GetCeilingHeightAtZ(z), entranceBlend) + entranceBlend * 1.5f;
                GetWallWidthsAtZ(z, out float lw, out float rw);
                bool isEntranceRing = r <= 3;

                for (int a = 0; a < arcPts; a++)
                {
                    float tA = a / (float)(arcPts - 1), ang = Mathf.Lerp(Mathf.PI, 0f, tA);
                    float cosA = Mathf.Cos(ang), sinA = Mathf.Sin(ang);
                    float sideRadius = Mathf.Lerp(4.85f, cosA < 0f ? lw : rw, entranceBlend);
                    float crag = (a == 0 || a == arcPts - 1 || isEntranceRing) ? 0f : (Mathf.PerlinNoise(a * 0.55f + 4.1f, r * 0.28f + 9.3f) - 0.5f) * 2.15f;
                    float x = sx + cosA * (sideRadius + crag * 0.75f);
                    float y = (a == 0 || a == arcPts - 1) ? (baseY - 4.5f) : (GetSpineFloorY(z) + sinA * ceilH + crag * 0.7f);
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(tA * 8f, z * 0.14f));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int a = 0; a < arcPts - 1; a++)
                {
                    int i0 = r * arcPts + a, i1 = i0 + 1, i2 = (r + 1) * arcPts + a, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }

            int backFloorAnchor = verts.Count;
            verts.Add(new Vector3(GetSpineX(CaveEndZ), GetSpineFloorY(CaveEndZ) - 4.0f, CaveEndZ + 1.8f));
            uvs.Add(new Vector2(0.5f, 0f));

            int lastRing = rings * arcPts;
            for (int a = 0; a < arcPts - 1; a++) { tris.Add(lastRing + a); tris.Add(lastRing + a + 1); tris.Add(backFloorAnchor); }

            Mesh vaultMesh = BuildFlatRockMesh(verts, uvs, tris, "CaveVaultSolidMesh_V10");
            var tunnelObj = new GameObject("CaveRockVault", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            tunnelObj.transform.SetParent(parent, false);
            tunnelObj.GetComponent<MeshFilter>().sharedMesh = vaultMesh;
            var mr = tunnelObj.GetComponent<MeshRenderer>();
            mr.sharedMaterial = GetCaveRockMaterial();
            mr.shadowCastingMode = ShadowCastingMode.TwoSided;
            tunnelObj.GetComponent<MeshCollider>().sharedMesh = vaultMesh;
        }
    }
}
