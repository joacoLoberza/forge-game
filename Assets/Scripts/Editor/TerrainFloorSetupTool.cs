#if UNITY_EDITOR
using System.IO;
using ForjaGame.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    [InitializeOnLoad]
    public static class TerrainFloorSetupTool
    {
        private const string MESH_PATH = "Assets/Meshes/NaturalGreenPlateau_Mesh.asset";
        private const string MESHES_DIR = "Assets/Meshes";

        static TerrainFloorSetupTool()
        {
            EditorApplication.delayCall += EnsureMapSetupOnCompile;
        }

        private static void EnsureMapSetupOnCompile()
        {
            if (Application.isPlaying) return;
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isLoaded && activeScene.name.Equals("Furnace", System.StringComparison.OrdinalIgnoreCase))
            {
                var ctrl = Object.FindFirstObjectByType<GreenPlateauTerrainController>();
                if (ctrl != null)
                {
                    RestoreTerrainAndFloor(silent: true);
                }
            }
        }

        [MenuItem("ForgeTools/2. Restaurar Suelo con Relieves y Textura PNG Directa", false, 2)]
        [MenuItem("Tools/Map/Restaurar Suelo con Relieves y Pasto", false, 11)]
        public static void RestoreTerrainMenu()
        {
            RestoreTerrainAndFloor(silent: false);
        }

        public static void RestoreTerrainAndFloor(bool silent = false)
        {
            if (!Directory.Exists(MESHES_DIR))
                Directory.CreateDirectory(MESHES_DIR);

            // 1. Crear o actualizar la Malla de relieves en disco (colinas naturales de 3.5m)
            Mesh plateauMesh = GreenPlateauTerrainController.GenerateUndulatingMesh(260f, 60, 3.5f);
            plateauMesh.name = "SimpleLowPolyGround_Mesh";

            Mesh existingAsset = AssetDatabase.LoadAssetAtPath<Mesh>(MESH_PATH);
            if (existingAsset != null)
            {
                existingAsset.name = "SimpleLowPolyGround_Mesh";
                existingAsset.Clear();
                existingAsset.indexFormat = plateauMesh.indexFormat;
                existingAsset.vertices = plateauMesh.vertices;
                existingAsset.normals = plateauMesh.normals;
                existingAsset.uv = plateauMesh.uv;
                existingAsset.triangles = plateauMesh.triangles;
                existingAsset.bounds = plateauMesh.bounds;
                EditorUtility.SetDirty(existingAsset);
                plateauMesh = existingAsset;
            }
            else
            {
                AssetDatabase.CreateAsset(plateauMesh, MESH_PATH);
            }
            AssetDatabase.SaveAssets();

            // 2. Material con textura PNG directa anclada
            Material grassMat = GreenPlateauTerrainController.CreateNaturalGrassMaterial();

            // 3. Desactivar el render del primitivo Plane para que no solape dos suelos
            var plane = GameObject.Find("Plane");
            if (plane != null)
            {
                var pr = plane.GetComponent<MeshRenderer>();
                if (pr != null) { pr.enabled = false; EditorUtility.SetDirty(pr); }
                plane.transform.position = new Vector3(0f, -1.0f, 0f);
                EditorUtility.SetDirty(plane);
            }

            // 4. Configurar el ÚNICO suelo del mapa: NaturalGreenPlateau
            GameObject plateauObj = GameObject.Find("NaturalGreenPlateau");
            if (plateauObj == null)
            {
                plateauObj = new GameObject("NaturalGreenPlateau", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            }
            plateauObj.transform.SetParent(null, true);
            plateauObj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            plateauObj.transform.localScale = Vector3.one;

            var filter = plateauObj.GetComponent<MeshFilter>() ?? plateauObj.AddComponent<MeshFilter>();
            var renderer = plateauObj.GetComponent<MeshRenderer>() ?? plateauObj.AddComponent<MeshRenderer>();
            var col = plateauObj.GetComponent<MeshCollider>() ?? plateauObj.AddComponent<MeshCollider>();

            filter.sharedMesh = plateauMesh;
            col.sharedMesh = null;
            col.sharedMesh = plateauMesh;
            col.enabled = true;
            renderer.sharedMaterial = grassMat;
            renderer.enabled = true;

            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(col);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(plateauObj);

            // 5. Plataforma de piedra de herrería y detalles de entorno
            GreenPlateauTerrainController.EnsureStoneWorkshopFloor();
            GreenPlateauTerrainController.EnsureEnvironmentProps();

            // 6. Guardar cambios en escena
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);

            if (!silent)
            {
                Debug.Log("[TerrainFloorSetupTool] Suelo único con relieves 3D naturales y textura PNG directa restaurado.");
            }
        }
    }
}
#endif
