using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    public partial class GreenPlateauTerrainController
    {
        private const string StoneFloorMeshVersion = "ForgeStoneFloor_Perpendicular_V3";
        private static GameObject _stoneFloorObject;
        private static Texture2D _cachedStoneTexture;

        public static void CleanPreviousStoneWorkshop()
        {
            var old = GameObject.Find("[FORGE_STONE_WORKSHOP]");
            if (old != null) UnitySafeUtility.DestroyGameObjectSafe(old);
            _stoneFloorObject = null;
        }

        public static GameObject EnsureStoneWorkshopFloor()
        {
            if (_stoneFloorObject == null) _stoneFloorObject = GameObject.Find("[FORGE_STONE_WORKSHOP]");

            if (_stoneFloorObject == null)
            {
                _stoneFloorObject = new GameObject("[FORGE_STONE_WORKSHOP]", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                _stoneFloorObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _stoneFloorObject.transform.localScale = Vector3.one;
            }

            var filter = UnitySafeUtility.GetOrAdd<MeshFilter>(_stoneFloorObject);
            var renderer = UnitySafeUtility.GetOrAdd<MeshRenderer>(_stoneFloorObject);
            var col = UnitySafeUtility.GetOrAdd<MeshCollider>(_stoneFloorObject);

            if (filter.sharedMesh == null || filter.sharedMesh.name != StoneFloorMeshVersion)
            {
                Mesh mesh = BuildForgeStonePlatformMesh();
                filter.sharedMesh = mesh;
                col.sharedMesh = null;
                col.sharedMesh = mesh;
                col.enabled = true;
            }

            renderer.sharedMaterial = GetOrCreateStoneFloorMaterial();
            renderer.enabled = true;
            return _stoneFloorObject;
        }

        /// <summary>
        /// Plataforma de piedra perpendicular a la boca de la cueva: ubicada al Oeste (X = -14.0 a -5.0, Z = 3.2 a 16.8),
        /// abriendo su frente hacia el Este (+X) rumbo al sendero central y dejando el frente Norte libre hacia la cueva.
        /// </summary>
        private static Mesh BuildForgeStonePlatformMesh()
        {
            Mesh m = new Mesh { name = StoneFloorMeshVersion };
            List<Vector3> verts = new();
            List<Vector2> uvs = new();
            List<Color> colors = new();
            List<int> tris = new();

            const float xMin = -14.0f, xMax = -5.0f;
            const float zMin = 3.20f, zMax = 16.80f;
            const float topY = 0.075f, skirtY = -0.30f;
            const int cols = 10, rows = 15;
            float stepX = (xMax - xMin) / cols;
            float stepZ = (zMax - zMin) / rows;

            Color cBaseStone = new Color(0.52f, 0.50f, 0.47f, 1f);
            Color cSootStone = new Color(0.35f, 0.34f, 0.33f, 1f);
            Color cCurbStone = new Color(0.31f, 0.30f, 0.29f, 1f);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float cx0 = xMin + c * stepX, cx1 = cx0 + stepX;
                    float cz0 = zMin + r * stepZ, cz1 = cz0 + stepZ;
                    float y = topY + (Mathf.Sin(c * 2.7f + r * 3.3f) * 0.5f + 0.5f) * 0.010f;

                    float midX = (cx0 + cx1) * 0.5f, midZ = (cz0 + cz1) * 0.5f;
                    float dFurnace = Vector2.Distance(new Vector2(midX, midZ), new Vector2(-9.6f, 6.2f));
                    float dAnvil = Vector2.Distance(new Vector2(midX, midZ), new Vector2(-9.6f, 13.8f));
                    float soot = Mathf.Clamp01(Mathf.Max(1f - dFurnace / 2.8f, 1f - dAnvil / 2.4f));

                    AddQuad(verts, uvs, colors, tris,
                        new Vector3(cx0, y, cz0), new Vector3(cx1, y, cz0),
                        new Vector3(cx1, y, cz1), new Vector3(cx0, y, cz1),
                        Color.Lerp(cBaseStone, cSootStone, soot * 0.85f), 0.65f);
                }
            }

            // Bordillos sólidos perimetrales
            AddQuad(verts, uvs, colors, tris, new Vector3(xMin, skirtY, zMin), new Vector3(xMax, skirtY, zMin), new Vector3(xMax, topY, zMin), new Vector3(xMin, topY, zMin), cCurbStone, 0.3f);
            AddQuad(verts, uvs, colors, tris, new Vector3(xMax, skirtY, zMax), new Vector3(xMin, skirtY, zMax), new Vector3(xMin, topY, zMax), new Vector3(xMax, topY, zMax), cCurbStone, 0.3f);
            AddQuad(verts, uvs, colors, tris, new Vector3(xMin, skirtY, zMax), new Vector3(xMin, skirtY, zMin), new Vector3(xMin, topY, zMin), new Vector3(xMin, topY, zMax), cCurbStone, 0.3f);
            AddQuad(verts, uvs, colors, tris, new Vector3(xMax, skirtY, zMin), new Vector3(xMax, skirtY, zMax), new Vector3(xMax, topY, zMax), new Vector3(xMax, topY, zMin), cCurbStone, 0.3f);

            // Rampa de acceso frontal Este (+X, orientada hacia el camino central)
            const float rX0 = -5.0f, rX1 = -4.15f, rZ0 = 7.0f, rZ1 = 13.0f;
            AddQuad(verts, uvs, colors, tris,
                new Vector3(rX0, topY, rZ0), new Vector3(rX1, 0.012f, rZ0),
                new Vector3(rX1, 0.012f, rZ1), new Vector3(rX0, topY, rZ1),
                cBaseStone, 0.45f);

            m.vertices = verts.ToArray();
            m.uv = uvs.ToArray();
            m.colors = colors.ToArray();
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris,
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Color col, float uvScale)
        {
            int idx = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
            uvs.Add(new Vector2(v0.x * uvScale, v0.z * uvScale));
            uvs.Add(new Vector2(v1.x * uvScale, v1.z * uvScale));
            uvs.Add(new Vector2(v2.x * uvScale, v2.z * uvScale));
            uvs.Add(new Vector2(v3.x * uvScale, v3.z * uvScale));
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
            tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
        }

        public static Material GetOrCreateStoneFloorMaterial()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(s) { name = "Mat_ForgeStoneFloor", color = Color.white };
            Texture2D tex = GetOrCreateStoneFloorTexture();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.04f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.22f);
            return mat;
        }

        public static Texture2D GetOrCreateStoneFloorTexture()
        {
            if (_cachedStoneTexture != null) return _cachedStoneTexture;
            const int size = 256, brickW = 64, brickH = 32;
            _cachedStoneTexture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Tex_ForgeStoneFloor", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
            };

            Color cLight = new Color(0.55f, 0.53f, 0.50f);
            Color cDark = new Color(0.38f, 0.36f, 0.34f);
            Color cMortar = new Color(0.20f, 0.19f, 0.18f);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                int row = y / brickH, rowY = y % brickH;
                int shift = (row % 2 == 0) ? 0 : (brickW / 2);
                for (int x = 0; x < size; x++)
                {
                    int adjX = (x + shift) % size;
                    int col = adjX / brickW, colX = adjX % brickW;
                    bool mortar = rowY <= 2 || rowY >= brickH - 2 || colX <= 2 || colX >= brickW - 2;
                    float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.25f + Mathf.Sin(row * 4.3f + col * 2.7f) * 0.12f;
                    pixels[y * size + x] = mortar ? cMortar : Color.Lerp(cDark, cLight, 0.5f + n);
                }
            }
            _cachedStoneTexture.SetPixels(pixels);
            _cachedStoneTexture.Apply();
            return _cachedStoneTexture;
        }
    }
}
