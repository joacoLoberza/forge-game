using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ForjaGame.Core;

namespace ForjaGame.EditorTools
{
    [InitializeOnLoad]
    public static partial class CharacterSetupTool
    {
        static CharacterSetupTool()
        {
            EditorApplication.update += RunOnEditorReady;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update += RunOnEditorReady;
            }
        }

        private static void RunOnEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorApplication.update -= RunOnEditorReady;
            CheckAndRunSetup();
        }

        [MenuItem("Forja/Abrir Escena de Forja (Furnace)")]
        public static void OpenFurnaceScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Furnace.unity");
        }

        private static void CheckAndRunSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            SetupCombatAnimatorAndOverrides();

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != "Assets/Scenes/Furnace.unity" && System.IO.File.Exists("Assets/Scenes/Furnace.unity"))
            {
                activeScene = EditorSceneManager.OpenScene("Assets/Scenes/Furnace.unity");
            }

            if (activeScene.IsValid() && activeScene.path == "Assets/Scenes/Furnace.unity")
            {
                RunFullSetup();
            }
        }

        [MenuItem("Forja/Configurar Personaje y Camara")]
        [MenuItem("Tools/Forja/Configurar Personaje y Camara")]
        public static void RunFullSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[CharacterSetup] Iniciando configuración de personaje, animaciones, texturas y cámara...");

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            // 1. Extraer texturas del modelo y configurar material Paladín
            ExtractCharacterTextures();

            // 2. Configurar loop y bloquear root motion en las animaciones FBX
            ConfigureFbxLoopAndBake("Assets/Animations/Character/Breathing Idle.fbx");
            ConfigureFbxLoopAndBake("Assets/Animations/Character/Walking.fbx");
            ConfigureFbxLoopAndBake("Assets/Animations/Character/Running.fbx");

            // 3. Crear o cargar el Animator Controller con Blend Tree
            RuntimeAnimatorController controller = GetOrCreateAnimatorController();

            // 4. Configurar el Player en la escena actual
            SetupPlayerInScene(controller);

            // 5. Configurar la cámara orbital en tercera persona
            SetupCameraInScene();

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }

            Debug.Log("<color=#33FFAA><b>[CharacterSetup] ¡Configuración de Personaje y Cámara completada con éxito!</b></color>");
        }

        private static RuntimeAnimatorController GetOrCreateAnimatorController()
        {
            string controllerPath = "Assets/Animations/Character/PlayerAnimator.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null) return existing;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var rootSm = controller.layers[0].stateMachine;
            var state = rootSm.AddState("Locomotion");

            var blendTree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed"
            };

            AnimationClip idleClip = LoadFirstClip("Assets/Animations/Character/Breathing Idle.fbx");
            AnimationClip walkClip = LoadFirstClip("Assets/Animations/Character/Walking.fbx");
            AnimationClip runClip = LoadFirstClip("Assets/Animations/Character/Running.fbx");

            if (idleClip != null) blendTree.AddChild(idleClip, 0.0f);
            if (walkClip != null) blendTree.AddChild(walkClip, 1.0f);
            if (runClip != null) blendTree.AddChild(runClip, 2.0f);

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            state.motion = blendTree;
            rootSm.defaultState = state;

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        private static void SetupPlayerInScene(RuntimeAnimatorController controller)
        {
            var movement = Object.FindFirstObjectByType<PlayerMovement>();
            if (movement == null) return;

            GameObject player = movement.gameObject;

            // Ocultar la malla de la cápsula primitiva
            var mr = player.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            // Ocultar la esfera de rango de interacción si existe
            Transform ir = player.transform.Find("InteractionRange");
            if (ir != null)
            {
                var irMr = ir.GetComponent<MeshRenderer>();
                if (irMr != null) irMr.enabled = false;
            }

            // Comprobar o instanciar el visual del personaje
            Transform visualTrans = player.transform.Find("CharacterVisual");
            GameObject visualObj = visualTrans != null ? visualTrans.gameObject : null;

            if (visualObj == null)
            {
                string modelPath = "Assets/Models/Character/character.fbx";
                var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (modelPrefab != null)
                {
                    visualObj = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                    if (visualObj != null)
                    {
                        visualObj.name = "CharacterVisual";
                        visualObj.transform.SetParent(player.transform, false);
                        visualObj.transform.localRotation = Quaternion.identity;
                    }
                }
            }

            if (visualObj != null)
            {
                var smr = visualObj.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    Material paladinMat = GetOrCreatePaladinMaterial();
                    if (paladinMat != null) smr.sharedMaterial = paladinMat;
                }
                // Escala aumentada en un 40% respecto al valor base (1.08f * 1.40 = 1.512f)
                float targetScale = 1.512f;
                visualObj.transform.localScale = Vector3.one * targetScale;
                visualObj.transform.localPosition = new Vector3(0f, -1.33f, 0f);

                var anim = visualObj.GetComponent<Animator>() ?? visualObj.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false; // Evita conflicto con Rigidbody

                Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Models/Character/character.fbx");
                if (avatar != null)
                    anim.avatar = avatar;

                movement.SetAnimator(anim);

                var col = player.GetComponent<CapsuleCollider>();
                if (col != null)
                {
                    col.height = 2.66f;
                    col.radius = 0.58f;
                    col.center = Vector3.zero;
                }

                var eq = player.GetComponent<ForjaGame.Combat.PlayerEquipmentController>() ?? player.AddComponent<ForjaGame.Combat.PlayerEquipmentController>();
                SerializedObject eqSo = new SerializedObject(eq);
                eqSo.Update();
                var animProp = eqSo.FindProperty("characterAnimator");
                if (animProp != null) animProp.objectReferenceValue = anim;
                var atkProp = eqSo.FindProperty("attackController");
                if (atkProp != null) atkProp.objectReferenceValue = player.GetComponent<ForjaGame.Combat.AttackController>();
                var baseProp = eqSo.FindProperty("baseController");
                if (baseProp != null) baseProp.objectReferenceValue = controller;
                var swordProp = eqSo.FindProperty("swordOverride");
                if (swordProp != null) swordProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Character/Sword1H_Override.overrideController");
                var axeProp = eqSo.FindProperty("axeOverride");
                if (axeProp != null) axeProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Character/Axe1H_Override.overrideController");
                var hammerProp = eqSo.FindProperty("hammerOverride");
                if (hammerProp != null) hammerProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Character/Hammer1H_Override.overrideController");
                var heavy2HProp = eqSo.FindProperty("heavy2HOverride");
                if (heavy2HProp != null) heavy2HProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Character/Heavy2H_Override.overrideController");
                var polearmProp = eqSo.FindProperty("polearmOverride");
                if (polearmProp != null) polearmProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Character/Polearm_Override.overrideController");
                var swordPrefProp = eqSo.FindProperty("swordPrefab");
                if (swordPrefProp != null) swordPrefProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/Human_Sword.prefab");
                var hammerPrefProp = eqSo.FindProperty("hammerPrefab");
                if (hammerPrefProp != null) hammerPrefProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/Human_Warhammer.prefab");
                var axePrefProp = eqSo.FindProperty("axePrefab");
                if (axePrefProp != null) axePrefProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Melee Animations/Prefabs/Weapons/Human_Greatsword.prefab");
                eqSo.ApplyModifiedProperties();

                var procCombat = player.GetComponent<ForjaGame.Combat.ProceduralCombatController>() ?? player.AddComponent<ForjaGame.Combat.ProceduralCombatController>();
                procCombat.InitializeSkeletonAndPivot();

                var ikBridge = anim.GetComponent<ForjaGame.Combat.PlayerIKBridge>() ?? anim.gameObject.AddComponent<ForjaGame.Combat.PlayerIKBridge>();
                ikBridge.SetProceduralController(procCombat);

                var testBench = player.GetComponent<ForjaGame.Combat.ProceduralCombatTestBench>() ?? player.AddComponent<ForjaGame.Combat.ProceduralCombatTestBench>();

                var atkCtrl = player.GetComponent<ForjaGame.Combat.AttackController>();
                if (atkCtrl != null)
                {
                    SerializedObject atkSo = new SerializedObject(atkCtrl);
                    atkSo.Update();
                    var atkAnimProp = atkSo.FindProperty("characterAnimator");
                    if (atkAnimProp != null) atkAnimProp.objectReferenceValue = anim;
                    atkSo.ApplyModifiedProperties();
                }
            }

            EditorUtility.SetDirty(player);
        }

        private static void SetupCameraInScene()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            var follow = cam.GetComponent<PlayerCameraFollow>() ?? cam.gameObject.AddComponent<PlayerCameraFollow>();
            var movement = Object.FindFirstObjectByType<PlayerMovement>();
            if (movement != null)
                follow.Target = movement.transform;

            follow.TargetHeight = 2.05f;
            follow.Distance = 6.2f;

            EditorUtility.SetDirty(cam.gameObject);
        }
    }
}
