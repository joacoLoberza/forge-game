using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    public partial class GreenPlateauTerrainController
    {
        public const float PlateauMinZ = -120.0f;
        public const float PlateauMaxZ = 28.2f;
        public const float PlateauMinX = -120.0f;
        public const float PlateauMaxX = 120.0f;

        private static Material _safeGrassMat;

        public static float GetSimpleHeight(float wx, float wz, float maxH)
        {
            // 1. Sendero central que conecta el campamento con el umbral de la cueva
            if (wz >= -10.0f && wz <= PlateauMaxZ && Mathf.Abs(wx) <= 6.5f)
            {
                // Empalme milimétrico con el suelo de roca esculpida de la cueva (Y = 0.045m)
                float tRoad = Mathf.Clamp01((wz - 16.0f) / 10.0f);
                return Mathf.Lerp(0.0f, 0.045f, tRoad);
            }

            // 2. Explanada plana del taller exterior (Spawn en 0,0 y taller al oeste en X = -17..-4)
            float dxCamp = Mathf.Max(0f, wx < -17.5f ? (-17.5f - wx) : (wx > 13.5f ? (wx - 13.5f) : 0f));
            float dzCamp = Mathf.Max(0f, wz < -11.0f ? (-11.0f - wz) : (wz > 22.0f ? (wz - 22.0f) : 0f));
            float distCamp = Mathf.Sqrt(dxCamp * dxCamp + dzCamp * dzCamp);
            if (distCamp <= 0f) return 0f;

            float t = Mathf.Clamp01(distCamp / 16f);
            float blend = t * t * (3f - 2f * t);

            float wave1 = Mathf.Sin(wx * 0.036f + 0.7f) * Mathf.Cos(wz * 0.034f + 1.1f);
            float wave2 = Mathf.Sin(wx * 0.075f - wz * 0.060f + 1.8f) * 0.38f;
            float wave3 = Mathf.Sin(wx * 0.018f + wz * 0.020f) * 0.48f;
            float hills = Mathf.Clamp01(0.42f + wave1 * 0.42f + wave2 * 0.24f + wave3 * 0.24f);
            float baseH = hills * maxH;

            // Faldas montañosas que se elevan al acercarse a la pared rocosa exterior de la entrada
            if (wz >= 18.0f && Mathf.Abs(wx) > 6.5f)
            {
                float mtnRamp = Mathf.Clamp01((wz - 18.0f) / 10.0f);
                float sideRamp = Mathf.Clamp01((Mathf.Abs(wx) - 6.5f) / 12.0f);
                baseH += mtnRamp * sideRamp * 4.2f;
            }

            float distCenter = Mathf.Sqrt(wx * wx + wz * wz);
            float rim = Mathf.Clamp01((distCenter - 95f) / 30f);
            float rimH = rim * rim * 3.5f;

            return (baseH + rimH) * blend;
        }

        private static float GetRowZ(int row, int res)
        {
            if (row == res) return PlateauMaxZ;
            if (row == res - 1) return 27.60f;
            return Mathf.Lerp(PlateauMinZ, 27.60f, row / (float)(res - 1));
        }

        private static float GetPlateauMeshY(float wx, int row, int res, float maxH)
        {
            float wz = GetRowZ(row, res);
            float y = GetSimpleHeight(wx, wz, maxH);
            // Fila final tras Z=27.60m en la boca cavernosa: se hunde bajo el piso de roca sin crear baches previos
            if (row == res && Mathf.Abs(wx) <= 9.8f)
            {
                float xT = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(wx) - 7.8f) / 2.0f));
                y = Mathf.Lerp(y, -0.38f, xT);
            }
            return y;
        }

        public static Mesh GenerateUndulatingMesh(float size, int res, float maxH)
        {
            Mesh m = new Mesh { name = GroundMeshVersionName };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            float xStep = (PlateauMaxX - PlateauMinX) / res;

            int triVertexCount = res * res * 6;
            Vector3[] verts = new Vector3[triVertexCount];
            Vector2[] uvs = new Vector2[triVertexCount];
            int[] tris = new int[triVertexCount];

            int vIdx = 0;
            for (int z = 0; z < res; z++)
            {
                float z0 = GetRowZ(z, res);
                float z1 = GetRowZ(z + 1, res);
                for (int x = 0; x < res; x++)
                {
                    float x0 = PlateauMinX + x * xStep;
                    float x1 = PlateauMinX + (x + 1) * xStep;

                    float spanX = PlateauMaxX - PlateauMinX;
                    float spanZ = PlateauMaxZ - PlateauMinZ;
                    float u0 = (x0 - PlateauMinX) / spanX;
                    float u1 = (x1 - PlateauMinX) / spanX;
                    float v0 = (z0 - PlateauMinZ) / spanZ;
                    float v1 = (z1 - PlateauMinZ) / spanZ;

                    Vector3 p00 = new Vector3(x0, GetPlateauMeshY(x0, z, res, maxH), z0);
                    Vector3 p10 = new Vector3(x1, GetPlateauMeshY(x1, z, res, maxH), z0);
                    Vector3 p01 = new Vector3(x0, GetPlateauMeshY(x0, z + 1, res, maxH), z1);
                    Vector3 p11 = new Vector3(x1, GetPlateauMeshY(x1, z + 1, res, maxH), z1);

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

        public static void EnsurePlateauInvisibleBoundaries()
        {
            var existing = GameObject.Find("[PLATEAU_INVISIBLE_BOUNDARIES]");
            if (existing != null) UnitySafeUtility.DestroyGameObjectSafe(existing);

            var root = new GameObject("[PLATEAU_INVISIBLE_BOUNDARIES]");
            root.transform.position = Vector3.zero;

            // Paredes perimetrales invisibles reforzadas para impedir caídas al vacío en los extremos de la meseta
            CreateInvisibleBoundary(root.transform, "Boundary_South",     new Vector3(0f, 15f, -108f),       new Vector3(230f, 45f, 8f));
            CreateInvisibleBoundary(root.transform, "Boundary_West",      new Vector3(-108f, 15f, -40f),     new Vector3(8f, 45f, 144f));
            CreateInvisibleBoundary(root.transform, "Boundary_East",      new Vector3(108f, 15f, -40f),      new Vector3(8f, 45f, 144f));
            // Paredes norte en la falda montañosa que sellan el macizo y dejan libre el sendero de entrada (X = -6.5 a +6.5)
            CreateInvisibleBoundary(root.transform, "Boundary_NorthWest", new Vector3(-57.25f, 15f, 28.0f),  new Vector3(101.5f, 45f, 8f));
            CreateInvisibleBoundary(root.transform, "Boundary_NorthEast", new Vector3(57.25f, 15f, 28.0f),   new Vector3(101.5f, 45f, 8f));
            // Suelo de seguridad subterráneo anti-caída
            CreateInvisibleBoundary(root.transform, "Boundary_SubFloor",  new Vector3(0f, -10f, -40f),       new Vector3(240f, 4f, 160f));
        }

        private static void CreateInvisibleBoundary(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = 0;
            var col = go.GetComponent<BoxCollider>();
            col.isTrigger = false;
            col.size = size;
        }
    }
}
