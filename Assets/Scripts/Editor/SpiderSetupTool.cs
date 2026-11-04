using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.EditorTools
{
    public static class SpiderSetupTool
    {
        public const string SpiderFbxPath = "Assets/Models/Character/animated-spider/source/Model.fbx";
        public const string SpiderResourceFbxPath = "Assets/Resources/Models/SpiderModel.fbx";
        public const string SpiderTexturePath = "Assets/Models/Character/animated-spider/textures/Texture.png";
        public const string SpiderMaterialPath = "Assets/Materials/Mat_Spider.mat";
        public const string SpiderResourceMatPath = "Assets/Resources/Mat_Spider.mat";
        public const string SpiderControllerPath = "Assets/Animations/SpiderAnimator.controller";
        public const string SpiderResourceControllerPath = "Assets/Resources/Animations/SpiderAnimator.controller";

        [MenuItem("Tools/Forja/Configurar Araña Enemiga", false, 4)]
        public static void SetupSpiderEnemyMenu()
        {
            SetupSpiderAssets();
            Debug.Log("<color=green>[SpiderSetupTool] Assets y Animator de la Araña configurados exitosamente.</color>");
        }

        public static void SetupSpiderAssets()
        {
            EnsureDirectories();
            if (File.Exists(SpiderFbxPath)) AssetDatabase.ImportAsset(SpiderFbxPath, ImportAssetOptions.ForceUpdate);
            if (File.Exists(SpiderResourceFbxPath)) AssetDatabase.ImportAsset(SpiderResourceFbxPath, ImportAssetOptions.ForceUpdate);
            if (File.Exists(SpiderTexturePath)) AssetDatabase.ImportAsset(SpiderTexturePath, ImportAssetOptions.ForceUpdate);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SpiderMaterialPath);
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s) { name = "Mat_Spider" };
                AssetDatabase.CreateAsset(mat, SpiderMaterialPath);
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SpiderTexturePath)
                         ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/SpiderTexture.png");
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);

            BuildSpiderAnimatorController();
            AssetDatabase.SaveAssets();

            AssetDatabase.CopyAsset(SpiderMaterialPath, SpiderResourceMatPath);
            AssetDatabase.CopyAsset(SpiderControllerPath, SpiderResourceControllerPath);
        }

        public static AnimatorController BuildSpiderAnimatorController()
        {
            string sourcePath = File.Exists(SpiderResourceFbxPath) ? SpiderResourceFbxPath : SpiderFbxPath;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sourcePath);
            Dictionary<string, AnimationClip> clips = new();
            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                    clips[c.name.ToLowerInvariant()] = c;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SpiderControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(SpiderControllerPath);

            var rootSm = controller.layers[0].stateMachine;
            foreach (var cs in rootSm.states) rootSm.RemoveState(cs.state);

            AddSpiderState(rootSm, "idle", "walk tight", clips, loop: true, isDefault: true, speed: 0.18f);
            AddSpiderState(rootSm, "walk", "armature|walk", clips, loop: true, isDefault: false, speed: 1.0f);
            AddSpiderState(rootSm, "largeWalk", "large walk", clips, loop: true, isDefault: false, speed: 1.0f);
            AddSpiderState(rootSm, "run", "run", clips, loop: true, isDefault: false, speed: 1.0f);
            AddSpiderState(rootSm, "walkBack", "walk back", clips, loop: true, isDefault: false, speed: 1.0f);
            AddSpiderState(rootSm, "attack", "attack", clips, loop: false, isDefault: false, speed: 1.0f);
            AddSpiderState(rootSm, "die", "die", clips, loop: false, isDefault: false, speed: 1.0f);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddSpiderState(
            AnimatorStateMachine sm, string stateName, string matchKey,
            Dictionary<string, AnimationClip> clips, bool loop, bool isDefault, float speed)
        {
            AnimationClip clip = null;
            foreach (var kvp in clips)
            {
                if (stateName == "walk" && (kvp.Key.Contains("large") || kvp.Key.Contains("back") || kvp.Key.Contains("tight")))
                    continue;
                if (kvp.Key.Contains(matchKey) || (stateName == "walk" && kvp.Key.EndsWith("walk")))
                {
                    clip = kvp.Value;
                    break;
                }
            }

            var state = sm.AddState(stateName);
            state.speed = speed;
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

        public static GameObject CreateSpiderInstance(string name, Vector3 spawnPos, int hp, int biteDmg, float legSpan, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var ctrl = go.AddComponent<SpiderEnemyController>();
            ctrl.ConfigureSpider(spawnPos, hp, biteDmg, legSpan);
            return go;
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory("Assets/Materials");
            Directory.CreateDirectory("Assets/Animations");
            Directory.CreateDirectory("Assets/Resources/Models");
            Directory.CreateDirectory("Assets/Resources/Animations");
        }
    }
}

