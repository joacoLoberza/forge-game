using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Constructor procedural 3D de la Gran Caverna Subterránea (Z = 28m a Z = 226m).
    /// Genera 5 grandes cámaras asimétricas de formas orgánicas variadas, túneles de distinta
    /// longitud, ascensos y descensos pronunciados en altura (Y), umbral sin solapamiento
    /// de capas y ábside final herméticamente sellado.
    /// </summary>
    public static partial class CaveDungeonBuilder
    {
        public const float CaveEntranceZ = 28.0f;
        public const float CaveEndZ = 226.0f;
        public const float VictoryAltarZ = 215.0f;
        public const string CaveVersionMarker = "OrganicCavern_V3";

        private static Material _caveRockMat;
        private static Material _caveFloorMat;

        public static GameObject EnsureCaveBuiltInScene(bool forceRebuild = false)
        {
            GameObject existing = GameObject.Find("[CAVE_STRUCTURE]");
            if (existing != null && !forceRebuild && existing.transform.Find(CaveVersionMarker) != null)
            {
                ConfigureVictoryAndDefeatTriggers(existing.transform);
                return existing;
            }
            if (existing != null)
                UnitySafeUtility.DestroyGameObjectSafe(existing);

            GameObject root = new GameObject("[CAVE_STRUCTURE]");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var marker = new GameObject(CaveVersionMarker);
            marker.transform.SetParent(root.transform, false);

            BuildOrganicCaveFloor(root.transform);
            BuildCaveVaultShell(root.transform);
            BuildEntranceFacadeAndPortal(root.transform);
            BuildCaveTorchesAndCrystals(root.transform);
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
            float w = Mathf.SmoothStep(0f, 1f, (z - (CaveEntranceZ + 1.5f)) / 9.0f);
            float endTaper = 1f - Mathf.SmoothStep(0f, 1f, (z - 196f) / 15f);

            // Espina serpenteante amplia con tramos de distinta longitud
            float curve = -10.8f * Gauss(z, 56f, 13.5f)
                        +  14.2f * Gauss(z, 104f, 16.5f)
                        +   4.8f * Mathf.Sin((z - 118f) * 0.11f) * Gauss(z, 138f, 14f)
                        -  13.6f * Gauss(z, 169f, 15.0f)
                        +   5.4f * Gauss(z, 193f, 8.5f);
            return curve * w * endTaper;
        }

        public static float GetSpineFloorY(float z)
        {
            if (z <= CaveEntranceZ + 0.4f) return 0.045f;

            // 1) Entrada -> Cámara 1 (-4.2m)
            float c1 = Mathf.SmoothStep(0f, 1f, (z - 28.4f) / 22f) * -4.2f;
            // 2) Garganta corta abrupta -> Cámara 2 profunda (-10.2m)
            float plunge2 = Mathf.SmoothStep(0f, 1f, (z - 70f) / 17f) * -6.0f;
            // 3) Largo corredor serpenteante EN SUBIDA -> Cámara 3 elevada (-1.8m)
            float climb3 = Mathf.SmoothStep(0f, 1f, (z - 121f) / 31f) * 8.4f;
            // 4) Desfiladero medio en bajada -> Santuario (-5.4m, subiendo a -4.6m en el altar)
            float drop4 = Mathf.SmoothStep(0f, 1f, (z - 183f) / 18f) * -3.8f;
            float dais = 0.95f * Gauss(z, VictoryAltarZ, 8.5f);
            float micro = Mathf.Sin((z - 30f) * 0.13f) * 0.22f * Mathf.Clamp01((z - 30f) / 8f);

            return 0.045f + c1 + plunge2 + climb3 + drop4 + dais + micro;
        }

        public static void GetWallWidthsAtZ(float z, out float leftWidth, out float rightWidth)
        {
            // Cámaras mucho más amplias y asimétricas + corredores de ancho y largo variable
            float ch1L = 12.8f * Gauss(z, 57f, 13.5f), ch1R = 8.4f * Gauss(z, 54f, 11.5f);
            float ch2L = 11.5f * Gauss(z, 101f, 15.5f), ch2R = 15.6f * Gauss(z, 106f, 16.5f);
            float ch3L = 14.2f * Gauss(z, 167f, 14.0f), ch3R = 11.8f * Gauss(z, 171f, 13.5f);
            float ch4L = 12.6f * Gauss(z, 212f, 11.5f), ch4R = 12.6f * Gauss(z, 212f, 11.5f);

            // Lóbulos orgánicos irregulares para romper cualquier silueta circular
            float lobeL = (Mathf.Sin(z * 0.19f + 1.2f) * 1.45f + Mathf.Cos(z * 0.37f) * 0.75f) * Mathf.Clamp01((z - 34f) / 8f);
            float lobeR = (Mathf.Cos(z * 0.17f + 2.5f) * 1.45f + Mathf.Sin(z * 0.33f) * 0.75f) * Mathf.Clamp01((z - 34f) / 8f);

            // Cierre progresivo del ábside trasero (Z = 219..226)
            float endPinch = 1f - 0.62f * Mathf.SmoothStep(0f, 1f, (z - 220f) / 6.0f);
            float portalFlare = 1.25f * Gauss(z, CaveEntranceZ, 3.0f);

            float baseCorridor = 4.95f;
            leftWidth = Mathf.Max(3.2f, (baseCorridor + ch1L + ch2L + ch3L + ch4L + lobeL + portalFlare) * endPinch);
            rightWidth = Mathf.Max(3.2f, (baseCorridor + ch1R + ch2R + ch3R + ch4R + lobeR + portalFlare) * endPinch);
        }

        public static float GetHalfWidthAtZ(float z)
        {
            GetWallWidthsAtZ(z, out float lw, out float rw);
            return (lw + rw) * 0.5f;
        }

        public static float GetFloorY(float x, float z)
        {
            float baseY = GetSpineFloorY(z);
            if (z <= CaveEntranceZ + 0.8f) return baseY;

            float sx = GetSpineX(z);
            GetWallWidthsAtZ(z, out float lw, out float rw);
            float sideW = x < sx ? lw : rw;
            float norm = Mathf.Clamp01(Mathf.Abs(x - sx) / Mathf.Max(2.5f, sideW));

            // Terrazas naturales suaves en bordes + depresiones orgánicas transitables
            float bank = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, (norm - 0.52f) / 0.48f)) * 0.95f;
            float terrace = (Mathf.PerlinNoise(x * 0.09f + 17f, z * 0.09f + 9f) - 0.5f) * 0.65f * norm;
            float micro = (Mathf.PerlinNoise(x * 0.22f + 5f, z * 0.22f + 3f) - 0.5f) * 0.18f;
            float fade = Mathf.Clamp01((z - 29f) / 6f) * (1f - 0.7f * Gauss(z, VictoryAltarZ, 4.5f));
            return baseY + (bank + terrace + micro) * fade;
        }

        public static float GetCeilingHeightAtZ(float z)
        {
            float portalH = 6.75f;
            float dome = 4.8f * Gauss(z, 56f, 14f)
                       + 8.2f * Gauss(z, 104f, 16.5f)
                       + 9.4f * Gauss(z, 169f, 14.5f)
                       + 9.2f * Gauss(z, 212f, 12.0f);
            float wave = Mathf.Sin(z * 0.14f) * 0.65f * Mathf.Clamp01((z - 33f) / 6f);
            float endDome = 1f - 0.45f * Mathf.SmoothStep(0f, 1f, (z - 221f) / 5.0f);
            return (portalH + dome + wave) * endDome;
        }

        private static void BuildOrganicCaveFloor(Transform parent)
        {
            const int zSteps = 168, xSteps = 22;
            List<Vector3> verts = new(); List<Vector2> uvs = new(); List<int> tris = new();

            for (int iz = 0; iz <= zSteps; iz++)
            {
                float z = Mathf.Lerp(CaveEntranceZ - 0.35f, CaveEndZ + 1.6f, iz / (float)zSteps);
                float sx = GetSpineX(z);
                GetWallWidthsAtZ(z, out float lw, out float rw);

                for (int ix = 0; ix <= xSteps; ix++)
                {
                    float tx = ix / (float)xSteps;
                    float x = sx + Mathf.Lerp(-(lw + 1.6f), rw + 1.6f, tx);
                    float y = iz == 0 ? -0.22f : GetFloorY(x, z);
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(x * 0.22f, z * 0.22f));
                }
            }

            int stride = xSteps + 1;
            for (int iz = 0; iz < zSteps; iz++)
            {
                for (int ix = 0; ix < xSteps; ix++)
                {
                    int i0 = iz * stride + ix, i1 = i0 + 1, i2 = (iz + 1) * stride + ix, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
            }

            Mesh floorMesh = BuildFlatRockMesh(verts, uvs, tris, "OrganicCaveFloorMesh_V3");
            var floorObj = new GameObject("CaveSculptedRockFloor", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            floorObj.transform.SetParent(parent, false);
            floorObj.GetComponent<MeshFilter>().sharedMesh = floorMesh;
            floorObj.GetComponent<MeshRenderer>().sharedMaterial = GetCaveFloorMaterial();
            floorObj.GetComponent<MeshCollider>().sharedMesh = floorMesh;
        }

        private static void BuildCaveVaultShell(Transform parent)
        {
            const int rings = 156, arcPts = 19;
            List<Vector3> verts = new(); List<Vector2> uvs = new(); List<int> tris = new();

            for (int r = 0; r <= rings; r++)
            {
                float z = Mathf.Lerp(CaveEntranceZ, CaveEndZ, r / (float)rings);
                float sx = GetSpineX(z), baseY = GetSpineFloorY(z), ceilH = GetCeilingHeightAtZ(z);
                GetWallWidthsAtZ(z, out float lw, out float rw);
                bool isEntranceRing = r <= 1;

                for (int a = 0; a < arcPts; a++)
                {
                    float tA = a / (float)(arcPts - 1);
                    float ang = Mathf.Lerp(Mathf.PI, 0f, tA);
                    float cosA = Mathf.Cos(ang), sinA = Mathf.Sin(ang);
                    float sideRadius = cosA < 0f ? lw : rw;
                    float crag = (a == 0 || a == arcPts - 1 || isEntranceRing)
                        ? 0f
                        : (Mathf.PerlinNoise(a * 0.52f + 4.1f, r * 0.28f + 9.3f) - 0.5f) * 1.45f;

                    float x = sx + cosA * (sideRadius + crag * 0.7f);
                    float y = (a == 0 || a == arcPts - 1) ? (baseY - 1.6f) : (baseY + sinA * ceilH + crag * 0.65f);
                    verts.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(tA * 6f, z * 0.16f));
                }
            }

            // Caras internas únicas (sin caras duplicadas coplanares que causaban Z-fighting)
            for (int r = 0; r < rings; r++)
            {
                for (int a = 0; a < arcPts - 1; a++)
                {
                    int i0 = r * arcPts + a, i1 = i0 + 1, i2 = (r + 1) * arcPts + a, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            }

            // Cierre trasero 100% sellado desde la bóveda hasta por debajo del suelo rocoso
            int backFloorAnchor = verts.Count;
            verts.Add(new Vector3(GetSpineX(CaveEndZ), GetSpineFloorY(CaveEndZ) - 1.8f, CaveEndZ + 1.4f));
            uvs.Add(new Vector2(0.5f, 0f));

            int lastRing = rings * arcPts;
            for (int a = 0; a < arcPts - 1; a++)
            {
                tris.Add(lastRing + a);
                tris.Add(lastRing + a + 1);
                tris.Add(backFloorAnchor);
            }

            Mesh vaultMesh = BuildFlatRockMesh(verts, uvs, tris, "CaveVaultSolidMesh_V3");
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
