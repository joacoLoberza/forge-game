using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.EditorTools
{
    public static class GolemSetupTool
    {
        public const string GolemFbxPath = "Assets/Models/Character/golem-animation/source/Model.fbx";
        public const string GolemTexturePath = "Assets/Models/Character/golem-animation/textures/GOLEM_SKIN_rock_12.png";
        public const string GolemMaterialPath = "Assets/Materials/Mat_GolemRock.mat";
        public const string GolemControllerPath = "Assets/Animations/GolemAnimator.controller";

        [MenuItem("Tools/Forja/Configurar Golem Enemigo", false, 3)]
        public static void SetupGolemEnemyMenu()
        {
            SetupGolemAssets();
            SpawnOrUpdateGolemInScene();
            Debug.Log("<color=green>[GolemSetupTool] Golem enemigo configurado exitosamente.</color>");
        }

        public static void SetupGolemAssets()
        {
            AssetDatabase.ImportAsset(GolemFbxPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(GolemTexturePath, ImportAssetOptions.ForceUpdate);

            // 1. Crear / actualizar Material del Golem
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(GolemMaterialPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s) { name = "Mat_GolemRock" };
                AssetDatabase.CreateAsset(mat, GolemMaterialPath);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GolemTexturePath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);

            // 2. Crear / actualizar AnimatorController
            BuildGolemAnimatorController();
            AssetDatabase.SaveAssets();
            System.IO.Directory.CreateDirectory("Assets/Resources/Animations");
            AssetDatabase.CopyAsset(GolemMaterialPath, "Assets/Resources/Mat_GolemRock.mat");
            AssetDatabase.CopyAsset(GolemControllerPath, "Assets/Resources/Animations/GolemAnimator.controller");
        }

        public static AnimatorController BuildGolemAnimatorController()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(GolemFbxPath);
            Dictionary<string, AnimationClip> clips = new();
            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    clips[c.name.ToLower()] = c;
                    Debug.Log($"[GolemSetupTool] Clip encontrado: {c.name}");
                }
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GolemControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(GolemControllerPath);
            }

            var rootSm = controller.layers[0].stateMachine;
            // Limpiar estados previos
            var childStates = rootSm.states;
            foreach (var cs in childStates) rootSm.RemoveState(cs.state);

            // Agregar estados principales
            AddStateIfClip(rootSm, "idle", clips, true, true);
            AddStateIfClip(rootSm, "walkNormal", clips, true, false);
            AddStateIfClip(rootSm, "roar", clips, false, false);
            AddStateIfClip(rootSm, "attack3", clips, false, false);
            AddStateIfClip(rootSm, "attack4", clips, false, false);
            AddStateIfClip(rootSm, "attack6", clips, false, false);
            AddStateIfClip(rootSm, "attack7", clips, false, false);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddStateIfClip(AnimatorStateMachine sm, string name, Dictionary<string, AnimationClip> clips, bool loop, bool isDefault)
        {
            AnimationClip clip = null;
            foreach (var kvp in clips)
            {
                if (kvp.Key.Contains(name.ToLower()))
                {
                    clip = kvp.Value;
                    break;
                }
            }

            var state = sm.AddState(name);
            if (clip != null)
            {
                state.motion = clip;
                if (loop)
                {
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    EditorUtility.SetDirty(clip);
                }
            }
            if (isDefault) sm.defaultState = state;
        }

        public static GameObject SpawnOrUpdateGolemInScene()
        {
            // Eliminar dummies de prueba antiguos
            var oldDummies = Object.FindObjectsByType<CombatDummyEnemy>(FindObjectsSortMode.None);
            foreach (var d in oldDummies)
            {
                if (d != null && d.gameObject != null)
                {
                    if (Application.isPlaying) Object.Destroy(d.gameObject);
                    else Object.DestroyImmediate(d.gameObject);
                }
            }

            GameObject oldCapsule = GameObject.Find("DummyEnemy_Capsule");
            if (oldCapsule != null)
            {
                if (Application.isPlaying) Object.Destroy(oldCapsule);
                else Object.DestroyImmediate(oldCapsule);
            }

            GameObject golem = GameObject.Find("GolemEnemy_Boss");
            if (golem == null)
            {
                golem = new GameObject("GolemEnemy_Boss");
            }

            var col = golem.GetComponent<CapsuleCollider>();
            if (col == null) col = golem.AddComponent<CapsuleCollider>();
            col.height = 2.10f;
            col.radius = 0.75f;
            col.center = new Vector3(0f, 1.05f, 0f);

            var rb = golem.GetComponent<Rigidbody>();
            if (rb == null) rb = golem.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 300f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var ctrl = golem.GetComponent<GolemEnemyController>();
            if (ctrl == null) ctrl = golem.AddComponent<GolemEnemyController>();

            ctrl.EnsureVisualsAndComponents();
            return golem;
        }
    }
}
