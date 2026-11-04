using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ForjaGame.Environment;
using ForjaGame.Resources;
using ForjaGame.UI;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Herramienta de Editor para generar la planicie verde, la Cueva Minera progresiva
    /// (Carbón -> Cobre -> Oro -> Hierro + Arañas y Golems + Altar de Victoria), modelos 3D de estaciones y UI de libro.
    /// </summary>
    [InitializeOnLoad]
    public static class CaveMapSetupTool
    {
        static CaveMapSetupTool()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.name.Equals("Furnace", System.StringComparison.OrdinalIgnoreCase))
                {
                    GolemSetupTool.SetupGolemAssets();
                    SpiderSetupTool.SetupSpiderAssets();
                    EnsureGrassMaterialAsset();
                    EnsureStoneFloorMaterialAsset();
                    GenerateMapAndCaveInScene(forceRebuildCave: true);
                    EnsureCaveExpeditionInScene(forceRebuild: true);
                    ForjaGame.Forge.ForgeStationModelSetup.SetupAllModelsInScene(forceRebuild: true);
                    ApplyBookThemeInScene();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            };
        }

        [MenuItem("Tools/Forja/Bake Completo de Escena (Terreno, Cueva, Enemigos, Modelos 3D, UI)", false, 1)]
        public static void BakeAllInScene()
        {
            GolemSetupTool.SetupGolemAssets();
            SpiderSetupTool.SetupSpiderAssets();
            EnsureGrassMaterialAsset();
            EnsureStoneFloorMaterialAsset();
            GenerateMapAndCaveInScene(forceRebuildCave: true);
            EnsureCaveExpeditionInScene(forceRebuild: true);
            ForjaGame.Forge.ForgeStationModelSetup.SetupAllModelsInScene(forceRebuild: true);
            ApplyBookThemeInScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[CaveMapSetupTool] Bake completo de Cueva, Arañas, Golems, Estaciones y UI completado.</color>");
        }

        [MenuItem("Tools/Forja/Implementar Modelos 3D (Horno, Prensa, Yunque)", false, 2)]
        public static void SetupStationModelsMenu()
        {
            ForjaGame.Forge.ForgeStationModelSetup.SetupAllModelsInScene(forceRebuild: true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Map/Generar Planicie Verde y Cueva Minera", false, 10)]
        public static void GenerateMapInScene()
        {
            GenerateMapAndCaveInScene(forceRebuildCave: true);
            EnsureCaveExpeditionInScene(forceRebuild: true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static void GenerateMapAndCaveInScene(bool forceRebuildCave = false)
        {
            EnsureGrassMaterialAsset();
            GreenPlateauTerrainController.CleanPreviousMapObjects();

            var existing = Object.FindFirstObjectByType<GreenPlateauTerrainController>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject root = new GameObject("GreenPlateauTerrainController");
            var ctrl = root.AddComponent<GreenPlateauTerrainController>();
            ctrl.BuildGreenPlateau();
            CaveDungeonBuilder.EnsureCaveBuiltInScene(forceRebuild: forceRebuildCave);
        }

        public static void EnsureCaveExpeditionInScene(bool forceRebuild = false)
        {
            MineSpawnerController.PurgeLegacyCampVeinsAndBosses();
            var spawner = Object.FindFirstObjectByType<MineSpawnerController>();
            if (spawner == null)
            {
                GameObject go = new GameObject("MineSpawnerController");
                spawner = go.AddComponent<MineSpawnerController>();
            }
            spawner.SpawnAllDefaultMineZones(forceRebuild: forceRebuild);
        }

        public static Material EnsureGrassMaterialAsset()
        {
            string matPath = "Assets/Materials/NaturalGrass_Mat.mat";
            string texPath = "Assets/Materials/Tex_NaturalGrassOrganic.png";

            if (!System.IO.File.Exists(texPath))
            {
                Texture2D gen = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                Color c = new Color(0.28f, 0.48f, 0.20f, 1f);
                Color[] cols = new Color[32 * 32];
                for (int i = 0; i < cols.Length; i++) cols[i] = c;
                gen.SetPixels(cols);
                gen.Apply();
                System.IO.File.WriteAllBytes(texPath, gen.EncodeToPNG());
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Tex_NaturalGrass_Normal.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s) { name = "NaturalGrass_Mat" };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (normalTex != null)
            {
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.02f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.18f);
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            var plane = GameObject.Find("Plane");
            if (plane != null)
            {
                plane.transform.position = new Vector3(0f, -0.05f, 0f);
                plane.transform.localScale = new Vector3(25f, 1f, 25f);
            }
            return mat;
        }

        public static Material EnsureStoneFloorMaterialAsset()
        {
            string matPath = "Assets/Materials/Mat_ForgeStoneFloor.mat";
            string texPath = "Assets/Materials/Tex_ForgeStoneFloor.png";

            Texture2D gen = GreenPlateauTerrainController.GetOrCreateStoneFloorTexture();
            System.IO.File.WriteAllBytes(texPath, gen.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s) { name = "Mat_ForgeStoneFloor" };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.04f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        [MenuItem("Tools/Map/Limpiar Entorno de Mapa", false, 12)]
        public static void CleanMapInScene()
        {
            GreenPlateauTerrainController.CleanPreviousMapObjects();
            var existing = Object.FindFirstObjectByType<GreenPlateauTerrainController>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var stone = GameObject.Find("[FORGE_STONE_WORKSHOP]");
            if (stone != null) Object.DestroyImmediate(stone);
            var cave = GameObject.Find("[CAVE_STRUCTURE]");
            if (cave != null) Object.DestroyImmediate(cave);
            var exp = GameObject.Find("[CAVE_EXPEDITION_AREA]");
            if (exp != null) Object.DestroyImmediate(exp);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/UI/Aplicar Estilo Unificado a Todo el Canvas", false, 20)]
        public static void ApplyBookThemeInScene()
        {
            BookJournalStyler.ApplyBookTheme();
            ForgeCanvasThemeStyler.ApplyGlobalCanvasTheme();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
