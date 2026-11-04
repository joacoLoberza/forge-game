using UnityEngine;

namespace ForjaGame.Environment
{
    public partial class GreenPlateauTerrainController
    {
        private static Material _safeGrassMat;

        public static float GetSimpleHeight(float wx, float wz, float maxH)
        {
            // 1. Explanada plana que abarca Spawn (0,0), Taller Oeste (X=-14..-5) y sendero hasta el portal (Z=28)
            float dxCamp = Mathf.Max(0f, wx < -17.5f ? (-17.5f - wx) : (wx > 13.5f ? (wx - 13.5f) : 0f));
            float dzCamp = Mathf.Max(0f, wz < -11.0f ? (-11.0f - wz) : (wz > 28.4f ? (wz - 28.4f) : 0f));
            float distCamp = Mathf.Sqrt(dxCamp * dxCamp + dzCamp * dzCamp);

            // 2. Huella subterránea de la gran caverna (Z = 28..226): hundida a -16.5m sin tocar el umbral exterior
            float spineX = wz >= 28f ? CaveDungeonBuilder.GetSpineX(wz) : 0f;
            float caveHalfW = wz >= 28f ? (CaveDungeonBuilder.GetHalfWidthAtZ(wz) + 4.5f) : 9.5f;
            float dxCave = Mathf.Max(0f, Mathf.Abs(wx - spineX) - caveHalfW);
            float dzCave = Mathf.Max(0f, wz < 29.8f ? (29.8f - wz) : (wz > 232f ? (wz - 232f) : 0f));
            float distCave = Mathf.Sqrt(dxCave * dxCave + dzCave * dzCave);

            if (wz >= 30.2f && distCave <= 0.01f)
                return -16.5f;

            // Transición limpia bajo la roca entre Z = 28.5 y Z = 30.2 (evita paredes diagonales de pasto en la puerta)
            if (wz > 28.5f && wz < 30.2f && dxCave <= 0.01f)
            {
                float sinkT = Mathf.SmoothStep(0f, 1f, (wz - 28.5f) / 1.7f);
                return Mathf.Lerp(0f, -16.5f, sinkT);
            }

            float distFromFlat = Mathf.Min(distCamp, distCave);
            if (distFromFlat <= 0f) return 0f;

            float t = Mathf.Clamp01(distFromFlat / 16f);
            float blend = t * t * (3f - 2f * t);

            float wave1 = Mathf.Sin(wx * 0.036f + 0.7f) * Mathf.Cos(wz * 0.034f + 1.1f);
            float wave2 = Mathf.Sin(wx * 0.075f - wz * 0.060f + 1.8f) * 0.38f;
            float wave3 = Mathf.Sin(wx * 0.018f + wz * 0.020f) * 0.48f;
            float hills = Mathf.Clamp01(0.42f + wave1 * 0.42f + wave2 * 0.24f + wave3 * 0.24f);
            float baseH = hills * maxH;

            float distCenter = Mathf.Sqrt(wx * wx + (wz - 30f) * (wz - 30f));
            float rim = Mathf.Clamp01((distCenter - 115f) / 40f);
            float rimH = rim * rim * 3.5f;

            return (baseH + rimH) * blend;
        }

        public static Mesh GenerateUndulatingMesh(float size, int res, float maxH)
        {
            Mesh m = new Mesh { name = GroundMeshVersionName };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            float step = size / res;
            float half = size * 0.5f;
            const float centerOffsetZ = 55.0f;

            int triVertexCount = res * res * 6;
            Vector3[] verts = new Vector3[triVertexCount];
            Vector2[] uvs = new Vector2[triVertexCount];
            int[] tris = new int[triVertexCount];

            int vIdx = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float x0 = (x * step) - half;
                    float x1 = ((x + 1) * step) - half;
                    float z0 = (z * step) - half + centerOffsetZ;
                    float z1 = ((z + 1) * step) - half + centerOffsetZ;

                    float u0 = (x0 + half) / 36f;
                    float u1 = (x1 + half) / 36f;
                    float v0 = (z0 + half) / 36f;
                    float v1 = (z1 + half) / 36f;

                    Vector3 p00 = new Vector3(x0, GetSimpleHeight(x0, z0, maxH), z0);
                    Vector3 p10 = new Vector3(x1, GetSimpleHeight(x1, z0, maxH), z0);
                    Vector3 p01 = new Vector3(x0, GetSimpleHeight(x0, z1, maxH), z1);
                    Vector3 p11 = new Vector3(x1, GetSimpleHeight(x1, z1, maxH), z1);

                    verts[vIdx] = p00; uvs[vIdx] = new Vector2(u0, v0); tris[vIdx] = vIdx; vIdx++;
                    verts[vIdx] = p01; uvs[vIdx] = new Vector2(u0, v1); tris[vIdx] = vIdx; vIdx++;
                    verts[vIdx] = p10; uvs[vIdx] = new Vector2(u1, v0); tris[vIdx] = vIdx; vIdx++;

                    verts[vIdx] = p10; uvs[vIdx] = new Vector2(u1, v0); tris[vIdx] = vIdx; vIdx++;
                    verts[vIdx] = p01; uvs[vIdx] = new Vector2(u0, v1); tris[vIdx] = vIdx; vIdx++;
                    verts[vIdx] = p11; uvs[vIdx] = new Vector2(u1, v1); tris[vIdx] = vIdx; vIdx++;
                }
            }

            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Material CreateNaturalGrassMaterial()
        {
            if (_safeGrassMat != null) return _safeGrassMat;

#if UNITY_EDITOR
            _safeGrassMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/NaturalGrass_Mat.mat");
            if (_safeGrassMat != null) return _safeGrassMat;
#endif
            Material matAsset = UnityEngine.Resources.Load<Material>("NaturalGrass_Mat");
            if (matAsset != null) { _safeGrassMat = matAsset; return _safeGrassMat; }

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _safeGrassMat = new Material(litShader) { name = "NaturalGrass_Mat_Runtime" };

            Texture2D grassTex = UnityEngine.Resources.Load<Texture2D>("Tex_NaturalGrassOrganic");
            if (grassTex == null)
            {
                string pngPath = System.IO.Path.Combine(Application.dataPath, "Materials/Tex_NaturalGrassOrganic.png");
                if (System.IO.File.Exists(pngPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(pngPath);
                    grassTex = new Texture2D(2, 2, TextureFormat.RGB24, true);
                    grassTex.LoadImage(bytes);
                    grassTex.wrapMode = TextureWrapMode.Repeat;
                }
            }

            if (grassTex != null)
            {
                if (_safeGrassMat.HasProperty("_BaseMap")) _safeGrassMat.SetTexture("_BaseMap", grassTex);
                if (_safeGrassMat.HasProperty("_MainTex")) _safeGrassMat.SetTexture("_MainTex", grassTex);
            }
            if (_safeGrassMat.HasProperty("_Smoothness")) _safeGrassMat.SetFloat("_Smoothness", 0.04f);
            return _safeGrassMat;
        }
    }
}
