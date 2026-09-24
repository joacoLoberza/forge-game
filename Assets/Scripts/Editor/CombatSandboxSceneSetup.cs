using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Combat;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Generador automático de la escena Sandbox E1 para pruebas de trayectoria y rig del arma.
    /// </summary>
    public static class CombatSandboxSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/CombatSandbox_E1.unity";

        [MenuItem("Tools/Forge Game/Setup E1 Combat Sandbox Scene")]
        public static void SetupE1Sandbox()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Iluminación y Cámara
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            GameObject camGo = new GameObject("Main Camera");
            Camera cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0.35f, 1.45f, -1.85f);
            camGo.transform.rotation = Quaternion.Euler(12f, -8f, 0f);
            camGo.AddComponent<AudioListener>();

            // 2. Suelo sutil de referencia
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ReferenceFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = Vector3.one * 0.5f;
            Renderer floorR = floor.GetComponent<Renderer>();
            if (floorR != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.color = new Color(0.12f, 0.14f, 0.18f);
                floorR.sharedMaterial = m;
            }

            // 3. Controlador Sandbox E1
            GameObject sandboxGo = new GameObject("CombatSandbox_E1");
            CombatSandboxE1 sandbox = sandboxGo.AddComponent<CombatSandboxE1>();
            sandbox.SetupDefaultHierarchy();
            sandbox.BakeMotion();

            // 4. Guardar escena
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"<color=#32CD32><b>[CombatSandboxSceneSetup]</b> Escena E1 creada y guardada exitosamente en {ScenePath}</color>");
            Selection.activeGameObject = sandboxGo;
        }
    }
}
