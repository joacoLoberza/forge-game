using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Construye el macizo montañoso continuo (malla rocosa facetada alpina sin esferas ni árboles decorativos),
    /// abrazando el arco de la cueva en Z = 28m y el sendero en T que une Spawn, Taller Oeste y Cueva Norte.
    /// </summary>
    public partial class GreenPlateauTerrainController
    {
        private const string PlateauDetailsVersionTag = "PlateauMountainMassif_V3";
        private static Texture2D _mountainGraniteTex;

        public static void CleanPreviousEnvironmentProps()
        {
            var old = GameObject.Find("[PLATEAU_DETAILS]");
            if (old != null) UnitySafeUtility.DestroyGameObjectSafe(old);
        }

        public static void EnsureEnvironmentProps()
        {
            GameObject existing = GameObject.Find("[PLATEAU_DETAILS]");
            if (existing != null)
            {
                if (existing.transform.Find(PlateauDetailsVersionTag) != null) return;
                CleanPreviousEnvironmentProps();
            }

            GameObject root = new GameObject("[PLATEAU_DETAILS]");
            var marker = new GameObject(PlateauDetailsVersionTag);
            marker.transform.SetParent(root.transform, false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material dirtMat = MakeLitMat(lit, "Mat_MountainTrail", new Color(0.40f, 0.32f, 0.22f), 0f, 0.08f);
            Material woodMat = MakeLitMat(lit, "Mat_LanternPostWood", new Color(0.28f, 0.18f, 0.10f), 0f, 0.10f);

            BuildTrailAndLanterns(root.transform, dirtMat, woodMat);
            BuildContinuousMountainMassif(root.transform, lit);
        }

        private static void BuildTrailAndLanterns(Transform root, Material dirtMat, Material woodMat)
        {
            // Sendero recto Norte desde la plaza central (Z=2.2) hasta antes del umbral de piedra (Z=26.4)
            for (int i = 0; i < 10; i++)
            {
                float z = Mathf.Lerp(2.4f, 26.2f, i / 9f);
                float x = Mathf.Sin(i * 0.65f) * 0.28f;
                MakeBox(root, $"TrailNorth_{i}", new Vector3(x, 0.014f, z), new Vector3(3.4f, 0.024f, 2.6f),
                    Quaternion.Euler(0f, Mathf.Sin(i) * 3f, 0f), dirtMat, false);
            }

            // Ramal perpendicular Oeste hacia el Taller de Forja (Z=10.0, X=-4.0..-1.0)
            MakeBox(root, "TrailForgeSpur_0", new Vector3(-1.6f, 0.015f, 10.0f), new Vector3(2.4f, 0.024f, 3.6f), Quaternion.identity, dirtMat, false);
            MakeBox(root, "TrailForgeSpur_1", new Vector3(-3.4f, 0.015f, 10.0f), new Vector3(2.2f, 0.024f, 3.8f), Quaternion.identity, dirtMat, false);

            Vector3[] lanternPts =
            {
                new Vector3(-3.6f, 0f, 6.4f),
                new Vector3(-3.6f, 0f, 13.6f),
                new Vector3(2.5f, 0f, 16.5f),
                new Vector3(-2.6f, 0f, 23.2f)
            };

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material glowMat = MakeLitMat(lit, "Mat_PathLantern", new Color(1f, 0.72f, 0.28f), 0f, 0.3f);
            if (glowMat.HasProperty("_EmissionColor")) { glowMat.EnableKeyword("_EMISSION"); glowMat.SetColor("_EmissionColor", new Color(1f, 0.65f, 0.22f) * 2.2f); }

            for (int i = 0; i < lanternPts.Length; i++)
            {
                var p = new GameObject($"TrailLantern_{i}").transform;
                p.SetParent(root, false);
                p.localPosition = lanternPts[i];
                float dir = lanternPts[i].x < 0f ? 1f : -1f;
                MakeBox(p, "Post", new Vector3(0f, 1.25f, 0f), new Vector3(0.16f, 2.5f, 0.16f), Quaternion.identity, woodMat, true);
                MakeBox(p, "Arm", new Vector3(dir * 0.28f, 2.38f, 0f), new Vector3(0.62f, 0.12f, 0.12f), Quaternion.identity, woodMat, false);
                MakeBox(p, "Lantern", new Vector3(dir * 0.50f, 2.15f, 0f), new Vector3(0.22f, 0.30f, 0.22f), Quaternion.identity, glowMat, false);

                var lGo = new GameObject("Light", typeof(Light));
                lGo.transform.SetParent(p, false);
                lGo.transform.localPosition = new Vector3(dir * 0.50f, 2.10f, 0f);
                var l = lGo.GetComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.68f, 0.28f); l.range = 10.5f; l.intensity = 1.7f;
            }
        }

        private static void BuildContinuousMountainMassif(Transform root, Shader lit)
        {
            const int xSteps = 76, zSteps = 84;
            const float xHalf = 94.0f, zStart = CaveDungeonBuilder.CaveEntranceZ, zEnd = 242.0f;

            Vector3[,] grid = new Vector3[zSteps + 1, xSteps + 1];
            for (int iz = 0; iz <= zSteps; iz++)
            {
                float tz = iz / (float)zSteps;
                float zBase = Mathf.Lerp(zStart, zEnd, tz);

                for (int ix = 0; ix <= xSteps; ix++)
                {
                    float tx = ix / (float)xSteps;
                    float x = Mathf.Lerp(-xHalf, xHalf, tx);
                    float z = iz == 0 && Mathf.Abs(x) > 6.3f
                        ? zBase - Mathf.Sin(Mathf.Abs(x) * 0.12f) * 2.2f * Mathf.Clamp01((Mathf.Abs(x) - 6.3f) / 10f)
                        : zBase;

                    float y = SampleMountainElevation(x, z, iz, zSteps, xHalf);
                    grid[iz, ix] = new Vector3(x, y, z);
                }
            }

            int quadCount = xSteps * zSteps;
            Vector3[] verts = new Vector3[quadCount * 6];
            Vector2[] uvs = new Vector2[quadCount * 6];
            Color[] cols = new Color[quadCount * 6];
            int[] tris = new int[quadCount * 6];

            int vi = 0;
            for (int iz = 0; iz < zSteps; iz++)
            {
                for (int ix = 0; ix < xSteps; ix++)
                {
                    Vector3 p00 = grid[iz, ix], p10 = grid[iz, ix + 1];
                    Vector3 p01 = grid[iz + 1, ix], p11 = grid[iz + 1, ix + 1];
                    Color cA = EvaluateMountainStrataColor((p00.y + p01.y + p10.y) / 3f, p00.x, p00.z);
                    Color cB = EvaluateMountainStrataColor((p10.y + p01.y + p11.y) / 3f, p11.x, p11.z);

                    AppendTri(verts, uvs, cols, tris, ref vi, p00, p01, p10, cA);
                    AppendTri(verts, uvs, cols, tris, ref vi, p10, p01, p11, cB);
                }
            }

            Mesh m = new Mesh { name = "AlpineMountainMassif_V3", indexFormat = IndexFormat.UInt32 };
            m.vertices = verts; m.uv = uvs; m.colors = cols; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();

            var mtnGo = new GameObject("NorthernMountainMassif", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            mtnGo.transform.SetParent(root, false);
            mtnGo.GetComponent<MeshFilter>().sharedMesh = m;
            mtnGo.GetComponent<MeshRenderer>().sharedMaterial = CreateMountainGraniteMaterial(lit);
            mtnGo.GetComponent<MeshCollider>().sharedMesh = m;
        }

        private static float SampleMountainElevation(float x, float z, int iz, int zSteps, float xHalf)
        {
            float absX = Mathf.Abs(x);
            // Borde frontal exacto en Z = 28: traza el arco exterior de la cueva dejando 100% libre el acceso
            if (iz == 0)
            {
                if (absX <= 6.22f)
                {
                    float u = Mathf.Clamp01(absX / 6.22f);
                    return 0.08f + Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * 6.82f;
                }
                return -2.2f;
            }
            if (iz == zSteps || absX >= xHalf - 0.5f) return -2.4f;

            // Ruido multifractal de aristas rocosas reales (crestas agudas, no semicírculos)
            float r1 = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.026f + 13.4f, z * 0.022f + 7.1f) * 2f - 1f);
            float r2 = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.064f + 31.2f, z * 0.058f + 19.8f) * 2f - 1f);
            float r3 = Mathf.PerlinNoise(x * 0.14f + 5.5f, z * 0.14f + 41.0f);
            float ridge = r1 * r1 * 24.0f + r2 * 9.5f + r3 * 3.4f;

            float frontRise = Mathf.SmoothStep(0f, 1f, (z - CaveDungeonBuilder.CaveEntranceZ) / 22.0f);
            float backFall = 1f - Mathf.SmoothStep(0f, 1f, (z - 216f) / 26.0f);
            float sideFall = 1f - Mathf.SmoothStep(0f, 1f, (absX - 42f) / (xHalf - 42f));
            float envelope = frontRise * backFall * sideFall;

            float rawH = -2.0f + (12.5f + ridge) * envelope;

            // Garantizar espesor de roca maciza sobre toda la bóveda de la cueva
            if (z <= CaveDungeonBuilder.CaveEndZ + 4f)
            {
                float sx = CaveDungeonBuilder.GetSpineX(z);
                float hw = CaveDungeonBuilder.GetHalfWidthAtZ(z) + 5.0f;
                float overCave = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, Mathf.Abs(x - sx) - hw) / 10f);
                if (overCave > 0.001f)
                {
                    float minRoof = CaveDungeonBuilder.GetSpineFloorY(z) + CaveDungeonBuilder.GetCeilingHeightAtZ(z) + 3.2f;
                    rawH = Mathf.Max(rawH, Mathf.Lerp(-1.5f, minRoof + r2 * 5.5f, overCave));
                }
            }
            return rawH;
        }

        private static Color EvaluateMountainStrataColor(float y, float x, float z)
        {
            float strata = Mathf.Sin(y * 0.85f + Mathf.PerlinNoise(x * 0.08f, z * 0.08f) * 2.4f) * 0.5f + 0.5f;
            Color lowBasalt = new Color(0.24f, 0.23f, 0.21f);
            Color midGranite = new Color(0.38f, 0.37f, 0.35f);
            Color highSlate = new Color(0.56f, 0.56f, 0.58f);
            Color baseCol = y < 14f ? Color.Lerp(lowBasalt, midGranite, Mathf.Clamp01(y / 14f))
                                    : Color.Lerp(midGranite, highSlate, Mathf.Clamp01((y - 14f) / 18f));
            return Color.Lerp(baseCol * 0.86f, baseCol * 1.10f, strata);
        }

        private static void AppendTri(Vector3[] v, Vector2[] uv, Color[] c, int[] t, ref int i, Vector3 a, Vector3 b, Vector3 d, Color col)
        {
            v[i] = a; uv[i] = new Vector2(a.x * 0.08f, a.z * 0.08f); c[i] = col; t[i] = i; i++;
            v[i] = b; uv[i] = new Vector2(b.x * 0.08f, b.z * 0.08f); c[i] = col; t[i] = i; i++;
            v[i] = d; uv[i] = new Vector2(d.x * 0.08f, d.z * 0.08f); c[i] = col; t[i] = i; i++;
        }

        private static Material CreateMountainGraniteMaterial(Shader lit)
        {
            if (_mountainGraniteTex == null)
            {
                const int sz = 128;
                _mountainGraniteTex = new Texture2D(sz, sz, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
                Color[] px = new Color[sz * sz];
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        float n = Mathf.PerlinNoise(x * 0.14f, y * 0.14f) * 0.32f + Mathf.PerlinNoise(x * 0.42f, y * 0.22f) * 0.18f;
                        float g = 0.62f + n;
                        px[y * sz + x] = new Color(g, g * 0.98f, g * 0.95f, 1f);
                    }
                }
                _mountainGraniteTex.SetPixels(px);
                _mountainGraniteTex.Apply();
            }

            var mat = new Material(lit) { name = "Mat_AlpineGraniteMassif", color = Color.white };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _mountainGraniteTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _mountainGraniteTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.10f);
            return mat;
        }
    }
}
