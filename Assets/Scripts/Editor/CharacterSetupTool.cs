using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ForjaGame.Core;
using ForjaGame.Combat;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Herramienta de Editor para configurar el personaje del jugador con el modelo 3D del Paladín,
    /// locomoción física grounded y combate procedural con WeaponComboAnimator y PlayerCombatIKDriver.
    /// </summary>
    public static class CharacterSetupTool
    {
        private const string PaladinModelPath = "Assets/Models/Character/character.fbx";
        private const string PaladinMaterialPath = "Assets/Models/Character/Materials/Paladin_Mat.mat";
        private const string AnimatorControllerPath = "Assets/Animations/Character/PlayerAnimator.controller";

        [MenuItem("Forja/Abrir Escena de Forja (Furnace)")]
        public static void OpenFurnaceScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Furnace.unity");
        }

        [MenuItem("Forja/Configurar Modelo Paladín y Combate Procedural")]
        [MenuItem("Tools/Forja/Configurar Modelo Paladín y Combate Procedural")]
        public static void RunFullSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[CharacterSetup] Configurando modelo 3D del Paladín y combate procedural...");

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            SetupPlayerCapsuleInScene();
            SetupCameraInScene();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CharacterSetup] Configuración completada con éxito. Paladín activo con pies firmes en el suelo.");
        }

        public static void SetupPlayerCapsuleInScene()
        {
            var movement = Object.FindFirstObjectByType<PlayerMovement>();
            if (movement == null)
            {
                Debug.LogWarning("[CharacterSetup] No se encontró PlayerMovement en la escena.");
                return;
            }

            GameObject player = movement.gameObject;

            // 1. Ocultar el MeshRenderer de la cápsula blanca física
            var mr = player.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = false;
            }

            // 2. Configurar la cápsula física con dimensiones correctas
            var col = player.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.height = 2.0f;
                col.radius = 0.45f;
                col.center = Vector3.zero;
            }

            // 3. Buscar o instanciar el modelo del Paladín
            Transform paladinChild = player.transform.Find("PaladinVisual");
            GameObject paladinGo = paladinChild != null ? paladinChild.gameObject : null;

            if (paladinGo == null)
            {
                GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaladinModelPath);
                if (fbxPrefab != null)
                {
                    paladinGo = Object.Instantiate(fbxPrefab, player.transform);
                    paladinGo.name = "PaladinVisual";
                }
            }

            if (paladinGo != null)
            {
                paladinGo.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                paladinGo.transform.localRotation = Quaternion.identity;
                paladinGo.transform.localScale = Vector3.one;

                // Asignar material
                Material paladinMat = AssetDatabase.LoadAssetAtPath<Material>(PaladinMaterialPath);
                var renderers = paladinGo.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (paladinMat != null) r.sharedMaterial = paladinMat;
                }

                // Configurar Animator
                var anim = paladinGo.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.applyRootMotion = false;
                    var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
                    if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                    movement.SetAnimator(anim);
                }
            }

            // 4. Asegurar controladores esenciales en Player
            if (player.GetComponent<AttackController>() == null)
                player.AddComponent<AttackController>();

            if (player.GetComponent<PlayerEquipmentController>() == null)
                player.AddComponent<PlayerEquipmentController>();

            EditorUtility.SetDirty(player);
        }

        public static void SetupCameraInScene()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var follow = cam.GetComponent<PlayerCameraFollow>() ?? cam.gameObject.AddComponent<PlayerCameraFollow>();
            var movement = Object.FindFirstObjectByType<PlayerMovement>();
            if (movement != null)
                follow.Target = movement.transform;

            follow.TargetHeight = 1.75f;
            follow.Distance = 5.2f;

            EditorUtility.SetDirty(cam.gameObject);
        }
    }
}
