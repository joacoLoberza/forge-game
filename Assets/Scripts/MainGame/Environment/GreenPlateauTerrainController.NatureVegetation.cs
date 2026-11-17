using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Genera la vegetación de bosque alpino (pinos, robles, rocas y arbustos del nature pack)
    /// a lo largo de toda la meseta verde, excluyendo estrictamente la forja, el spawn y el sendero a la cueva.
    /// </summary>
    public partial class GreenPlateauTerrainController
    {
        private const string ForestVersionTag = "PlateauNatureForest_V1";
        private static Material _natureAtlasMat;

        public static void CleanPreviousNatureVegetation()
        {
            var old = GameObject.Find("[PLATEAU_NATURE_FOREST]");
            if (old != null) UnitySafeUtility.DestroyGameObjectSafe(old);
        }

        public void EnsureNatureVegetation()
        {
            GameObject existing = GameObject.Find("[PLATEAU_NATURE_FOREST]");
            if (existing != null)
            {
                if (existing.transform.Find(ForestVersionTag) != null) return;
                CleanPreviousNatureVegetation();
            }

            GameObject root = new GameObject("[PLATEAU_NATURE_FOREST]");
            var marker = new GameObject(ForestVersionTag);
            marker.transform.SetParent(root.transform, false);

            Material mat = GetOrCreateNatureAtlasMaterial();

            // Semilla determinista para poblar un bosque alpino orgánico y consistente
            Random.InitState(42817);

            const float step = 8.2f;
            for (float z = -105f; z <= 24f; z += step)
            {
                for (float x = -105f; x <= 105f; x += step)
                {
                    float jx = x + Random.Range(-3.2f, 3.2f);
                    float jz = z + Random.Range(-3.2f, 3.2f);

                    if (IsInForestExclusionZone(jx, jz)) continue;

                    float y = GetSimpleHeight(jx, jz, maxElevation);
                    float roll = Random.value;

                    if (roll < 0.72f)
                    {
                        SpawnForestTree(root.transform, jx, y, jz, mat);
                    }
                    else if (roll < 0.88f)
                    {
                        SpawnForestRock(root.transform, jx, y, jz, mat);
                    }
                    else
                    {
                        SpawnForestBush(root.transform, jx, y, jz, mat);
                    }
                }
            }
        }

        public static bool IsInForestExclusionZone(float wx, float wz)
        {
            // 1. Taller de forja al oeste con perímetro de seguridad (X: -19..-2, Z: 0.5..19.5)
            if (wx >= -19.0f && wx <= -2.0f && wz >= 0.5f && wz <= 19.5f) return true;

            // 2. Plaza de Spawn en el origen
            if (wx * wx + wz * wz < 56.0f) return true;

            // 3. Sendero principal Norte hacia la cueva (X: -4.8..4.8, Z: -2..30)
            if (wz >= -2.0f && wz <= 30.0f && Mathf.Abs(wx) <= 4.8f) return true;

            // 4. Ramal Oeste hacia la forja (X: -8..1, Z: 7..13)
            if (wz >= 7.0f && wz <= 13.0f && wx >= -8.0f && wx <= 1.0f) return true;

            // 5. Umbral de entrada exterior a la cueva (Z >= 24, |X| <= 8.5)
            if (wz >= 24.0f && Mathf.Abs(wx) <= 8.5f) return true;

            // 6. Borde exterior del mapa (acantilado)
            if (wx * wx + wz * wz > 11800.0f) return true;

            return false;
        }

        private static void SpawnForestTree(Transform root, float x, float y, float z, Material mat)
        {
            bool nearMountainOrRidge = z > 8f || y > 2.6f;
            string prefix;
            float scale;

            if (nearMountainOrRidge)
            {
                prefix = Random.value < 0.80f ? "pine_tree" : "dead_tree";
                scale = prefix == "pine_tree" ? Random.Range(0.44f, 0.65f) : Random.Range(0.32f, 0.46f);
            }
            else
            {
                prefix = Random.value < 0.60f ? "pine_tree" : "oak";
                scale = prefix == "pine_tree" ? Random.Range(0.42f, 0.62f) : Random.Range(0.44f, 0.60f);
            }

            int variant = Random.Range(0, 6);
            string modelName = $"{prefix}_{variant}";
            Mesh mesh = LoadDecorationMesh(modelName);
            if (mesh == null) return;

            var go = new GameObject(modelName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, y - 0.05f, z);
            go.transform.rotation = Quaternion.Euler(Random.Range(-2f, 2f), Random.Range(0f, 360f), Random.Range(-2f, 2f));
            go.transform.localScale = Vector3.one * scale;

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.58f;
            col.height = 7.5f;
            col.center = new Vector3(0f, 3.8f, 0f);
        }

        private static void SpawnForestRock(Transform root, float x, float y, float z, Material mat)
        {
            int variant = Random.Range(0, 6);
            string modelName = $"rock_{variant}";
            Mesh mesh = LoadDecorationMesh(modelName);
            if (mesh == null) return;

            var go = new GameObject(modelName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, y - 0.25f, z);
            go.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
            go.transform.localScale = Vector3.one * Random.Range(0.85f, 1.45f);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 1.2f;
            col.center = new Vector3(0f, 0.6f, 0f);
        }

        private static void SpawnForestBush(Transform root, float x, float y, float z, Material mat)
        {
            int variant = Random.Range(0, 6);
            string modelName = $"bush_{variant}";
            Mesh mesh = LoadDecorationMesh(modelName);
            if (mesh == null) return;

            var go = new GameObject(modelName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, y - 0.08f, z);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * Random.Range(0.38f, 0.58f);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static readonly Dictionary<string, Mesh> _cachedMeshes = new();

        private static Mesh LoadDecorationMesh(string modelName)
        {
            if (_cachedMeshes.TryGetValue(modelName, out var cached) && cached != null) return cached;

            // 1. Carga desde Resources si ya fue importado
            var prefab = UnityEngine.Resources.Load<GameObject>($"Decorations/{modelName}");
            if (prefab != null)
            {
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    _cachedMeshes[modelName] = mf.sharedMesh;
                    return mf.sharedMesh;
                }
            }

#if UNITY_EDITOR
            // 2. Carga directa de AssetDatabase en Editor
            string objPath = $"Assets/Models/Decorations/Extracted/{modelName}.obj";
            var editorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (editorAsset != null)
            {
                var mf = editorAsset.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    _cachedMeshes[modelName] = mf.sharedMesh;
                    return mf.sharedMesh;
                }
            }
#endif
            return null;
        }

        public static Material GetOrCreateNatureAtlasMaterial()
        {
            if (_natureAtlasMat != null) return _natureAtlasMat;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _natureAtlasMat = new Material(lit) { name = "Mat_NatureAtlas_Organic" };

            Texture2D tex = UnityEngine.Resources.Load<Texture2D>("Tex_NatureAtlas");
            if (tex == null)
            {
                string pngPath = System.IO.Path.Combine(Application.dataPath, "Resources/Tex_NatureAtlas.png");
                if (!System.IO.File.Exists(pngPath))
                    pngPath = System.IO.Path.Combine(Application.dataPath, "Models/Decorations/embers-storm-free-nature-pack/textures/Magic Pack Texture_0.png");

                if (System.IO.File.Exists(pngPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(pngPath);
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    tex.LoadImage(bytes);
                    tex.wrapMode = TextureWrapMode.Repeat;
                }
            }

            if (tex != null)
            {
                if (_natureAtlasMat.HasProperty("_BaseMap")) _natureAtlasMat.SetTexture("_BaseMap", tex);
                if (_natureAtlasMat.HasProperty("_MainTex")) _natureAtlasMat.SetTexture("_MainTex", tex);
            }
            if (_natureAtlasMat.HasProperty("_Smoothness")) _natureAtlasMat.SetFloat("_Smoothness", 0.05f);
            return _natureAtlasMat;
        }
    }
}
