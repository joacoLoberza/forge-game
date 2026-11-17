using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Configura automáticamente el modelo Boss3_Titan.fbx, su textura Leviathan_Color15,
    /// sus materiales URP/Standard y su AnimatorController (StandIdle, WalkCycle, Attack1, Attack2, AttackHard, GetHit1, GetHit2, Die).
    /// </summary>
    public static class TitanSetupTool
    {
        private const string FbxPath = "Assets/Models/Character/titan-animation/source/Boss3_Titan.fbx";
        private const string TexPath = "Assets/Models/Character/titan-animation/textures/Leviathan_Color15.png";
        private const string MatPath = "Assets/Materials/Mat_TitanBoss.mat";
        private const string ControllerPath = "Assets/Animations/TitanAnimator.controller";

        private const string ResModelDir = "Assets/Resources/Models";
        private const string ResFbxPath = "Assets/Resources/Models/TitanModel.fbx";
        private const string ResMatPath = "Assets/Resources/Mat_TitanBoss.mat";
        private const string ResTexPath = "Assets/Resources/TitanTexture.png";
        private const string ResAnimDir = "Assets/Resources/Animations";
        private const string ResCtrlPath = "Assets/Resources/Animations/TitanAnimator.controller";

        [MenuItem("Tools/Titan/Configure Titan Boss Assets")]
        public static void SetupTitanAssets()
        {
            if (!File.Exists(FbxPath)) return;

            EnsureDir("Assets/Materials");
            EnsureDir("Assets/Animations");
            EnsureDir("Assets/Resources");
            EnsureDir(ResModelDir);
            EnsureDir(ResAnimDir);

            ConfigureLoopingTakes(FbxPath);
            if (!File.Exists(ResFbxPath))
            {
                AssetDatabase.CopyAsset(FbxPath, ResFbxPath);
                AssetDatabase.ImportAsset(ResFbxPath, ImportAssetOptions.ForceSynchronousImport);
            }
            ConfigureLoopingTakes(ResFbxPath);

            if (File.Exists(TexPath) && !File.Exists(ResTexPath))
            {
                AssetDatabase.CopyAsset(TexPath, ResTexPath);
                AssetDatabase.ImportAsset(ResTexPath, ImportAssetOptions.ForceSynchronousImport);
            }

            CreateOrUpdateTitanMaterial(MatPath);
            CreateOrUpdateTitanMaterial(ResMatPath);
            CreateOrUpdateTitanAnimator(ControllerPath);
            CreateOrUpdateTitanAnimator(ResCtrlPath);

            AssetDatabase.SaveAssets();
        }

        private static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void ConfigureLoopingTakes(string fbxAssetPath)
        {
            var importer = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
            if (importer == null) return;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool shouldLoop = n.Contains("idle") || n.Contains("walk");
                if (clips[i].loopTime != shouldLoop)
                {
                    clips[i].loopTime = shouldLoop;
                    changed = true;
                }
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static void CreateOrUpdateTitanMaterial(string targetMatPath)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath)
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>(ResTexPath);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(targetMatPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(targetMatPath) };
                AssetDatabase.CreateAsset(mat, targetMatPath);
            }
            mat.color = Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_Roughness")) mat.SetFloat("_Roughness", 0.72f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.28f);
            EditorUtility.SetDirty(mat);
        }

        private static void CreateOrUpdateTitanAnimator(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);

            AnimatorState defaultState = null;
            foreach (Object asset in allAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    AnimatorState st = sm.AddState(clip.name);
                    st.motion = clip;
                    if (clip.name.ToLowerInvariant().Contains("idle")) defaultState = st;
                }
            }
            if (defaultState != null) sm.defaultState = defaultState;
            EditorUtility.SetDirty(ctrl);
        }
    }
}

