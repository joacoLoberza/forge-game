using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Configura automáticamente la animación Humanoide Roll (CC0 Quaternius UAL1_Standard.fbx)
    /// con bloqueo de ejes Y/XZ y altura desde los pies (sin atravesar el suelo ni girar en Y).
    /// </summary>
    [InitializeOnLoad]
    public static class RollAnimatorSetupTool
    {
        private const string RollFbxPath = "Assets/Resources/Animations/Roll/UAL1_Standard.fbx";
        private const string BlockFbxPath = "Assets/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx";
        private static readonly string[] ControllerPaths =
        {
            "Assets/Animations/Character/PlayerAnimator.controller",
            "Assets/Resources/PlayerAnimator.controller"
        };

        static RollAnimatorSetupTool() => EditorApplication.delayCall += EnsureRollInstalledOnControllers;

        [MenuItem("Tools/Forja/Instalar Animaciones Roll y Bloqueo (UAL1+UAL2)")]
        public static void EnsureRollInstalledOnControllers()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (System.IO.File.Exists(RollFbxPath))
            {
                var importer = AssetImporter.GetAtPath(RollFbxPath) as ModelImporter;
                if (importer != null && EnsureImporterConfigured(importer, new[] { "Roll", "Jump_Start", "Jump_Loop", "Jump_Land" }, "Jump_Loop"))
                    importer.SaveAndReimport();
            }
            if (System.IO.File.Exists(BlockFbxPath))
            {
                var bImporter = AssetImporter.GetAtPath(BlockFbxPath) as ModelImporter;
                if (bImporter != null && EnsureImporterConfigured(bImporter, new[] { "Sword_Block" }, "Sword_Block"))
                    bImporter.SaveAndReimport();
            }

            AnimationClip rollClip = FindClip(RollFbxPath, "Roll");
            AnimationClip jumpStart = FindClip(RollFbxPath, "Jump_Start"), jumpLoop = FindClip(RollFbxPath, "Jump_Loop"), jumpLand = FindClip(RollFbxPath, "Jump_Land");
            AnimationClip blockClip = FindClip(BlockFbxPath, "Sword_Block");
            if (rollClip == null && jumpStart == null && blockClip == null) return;

            bool anyChanged = false;
            foreach (string path in ControllerPaths)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ctrl != null && ConfigureController(ctrl, rollClip, jumpStart, jumpLoop, jumpLand, blockClip)) anyChanged = true;
            }
            if (anyChanged) AssetDatabase.SaveAssets();
        }

        private static bool EnsureImporterConfigured(ModelImporter imp, string[] targetKeys, string loopKey)
        {
            bool dirty = false;
            if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; dirty = true; }
            if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }

            var clips = imp.clipAnimations != null && imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    string targetName = MatchAnimTargetName(c, targetKeys, loopKey, out bool loop);
                    if (targetName == null) continue;
                    if (c.name != targetName || !c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ || !c.keepOriginalPositionY || !c.heightFromFeet || c.loopTime != loop)
                    {
                        c.name = targetName;
                        c.lockRootRotation = true; c.keepOriginalOrientation = true;
                        c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = true;
                        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true; c.loopTime = loop;
                        clips[i] = c; dirty = true;
                    }
                }
                if (dirty) imp.clipAnimations = clips;
            }
            return dirty;
        }

        private static string MatchAnimTargetName(ModelImporterClipAnimation c, string[] keys, string loopKey, out bool loop)
        {
            loop = false;
            foreach (string key in keys)
            {
                if (c.name == key || c.takeName == key || c.takeName.EndsWith("|" + key) || c.name.EndsWith("|" + key))
                { loop = key == loopKey; return key; }
            }
            return null;
        }

        private static AnimationClip FindClip(string fbxPath, string key)
        {
            AnimationClip fallback = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    if (clip.name == key) return clip;
                    if (clip.name.EndsWith("|" + key) || clip.name.Contains(key)) fallback = clip;
                }
            }
            return fallback;
        }

        private static bool ConfigureController(AnimatorController ctrl, AnimationClip rollClip, AnimationClip jStart, AnimationClip jLoop, AnimationClip jLand, AnimationClip blockClip)
        {
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            AnimatorState existingRoll = FindState(sm, "Roll"), existingJump = FindState(sm, "Jump_Start");
            AnimatorStateMachine upperSm = ctrl.layers.Length > 1 ? ctrl.layers[1].stateMachine : null;
            AnimatorState existingBlock = upperSm != null ? FindState(upperSm, "Block") : null;
            if ((rollClip == null || (existingRoll != null && existingRoll.motion == rollClip))
                && (jStart == null || (existingJump != null && existingJump.motion == jStart))
                && (blockClip == null || (existingBlock != null && existingBlock.motion == blockClip))) return false;

            EnsureParam(ctrl, "RollSpeed", AnimatorControllerParameterType.Float, 1.60f);
            EnsureParam(ctrl, "IsRolling", AnimatorControllerParameterType.Bool, 0f);
            EnsureParam(ctrl, "IsGrounded", AnimatorControllerParameterType.Bool, 1f);
            EnsureParam(ctrl, "IsBlocking", AnimatorControllerParameterType.Bool, 0f);

            AnimatorState loco = FindState(sm, "Locomotion");
            if (rollClip != null)
            {
                AnimatorState rollState = existingRoll ?? sm.AddState("Roll", new Vector3(250f, 240f, 0f));
                rollState.speed = 1f; rollState.speedParameterActive = true; rollState.speedParameter = "RollSpeed";
                rollState.iKOnFeet = false; rollState.motion = rollClip;
                if (loco != null)
                {
                    rollState.transitions = new AnimatorStateTransition[0];
                    var ec = rollState.AddTransition(loco); ec.hasExitTime = false; ec.duration = 0.14f; ec.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsRolling");
                    var et = rollState.AddTransition(loco); et.hasExitTime = true; et.exitTime = 0.96f; et.duration = 0.12f;
                }
            }
            if (jStart != null) ConfigureJumpStates(sm, loco, jStart, jLoop ?? jStart, jLand ?? jStart);
            if (blockClip != null && upperSm != null)
            {
                AnimatorState stance = FindState(upperSm, "CombatStance");
                AnimatorState bState = existingBlock ?? upperSm.AddState("Block", new Vector3(300f, 160f, 0f));
                bState.speed = 1f; bState.iKOnFeet = false; bState.motion = blockClip;
                bState.transitions = new AnimatorStateTransition[0];
                if (stance != null)
                {
                    var bOut = bState.AddTransition(stance); bOut.hasExitTime = false; bOut.duration = 0.12f; bOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");
                }
            }
            EditorUtility.SetDirty(ctrl);
            return true;
        }

        private static void ConfigureJumpStates(AnimatorStateMachine sm, AnimatorState loco, AnimationClip jStart, AnimationClip jLoop, AnimationClip jLand)
        {
            var sStart = FindState(sm, "Jump_Start") ?? sm.AddState("Jump_Start", new Vector3(480f, 220f, 0f));
            var sLoop = FindState(sm, "Jump_Loop") ?? sm.AddState("Jump_Loop", new Vector3(690f, 220f, 0f));
            var sLand = FindState(sm, "Jump_Land") ?? sm.AddState("Jump_Land", new Vector3(480f, 300f, 0f));
            sStart.speed = 2.1f; sStart.iKOnFeet = false; sStart.motion = jStart;
            sLoop.speed = 1.15f; sLoop.iKOnFeet = false; sLoop.motion = jLoop;
            sLand.speed = 2.0f; sLand.iKOnFeet = false; sLand.motion = jLand;
            sStart.transitions = sLoop.transitions = sLand.transitions = new AnimatorStateTransition[0];

            var tLoop = sStart.AddTransition(sLoop); tLoop.hasExitTime = true; tLoop.exitTime = 0.72f; tLoop.duration = 0.12f;
            var tLand = sLoop.AddTransition(sLand); tLand.hasExitTime = false; tLand.duration = 0.10f; tLand.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            if (loco != null)
            {
                var tExitStart = sStart.AddTransition(loco); tExitStart.hasExitTime = false; tExitStart.duration = 0.12f; tExitStart.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
                var tExitLand = sLand.AddTransition(loco); tExitLand.hasExitTime = true; tExitLand.exitTime = 0.55f; tExitLand.duration = 0.14f;
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var cs in sm.states) if (cs.state.name == name) return cs.state;
            return null;
        }

        private static void EnsureParam(AnimatorController c, string name, AnimatorControllerParameterType type, float defVal)
        {
            foreach (var p in c.parameters) if (p.name == name) return;
            c.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = defVal, defaultBool = defVal > 0.5f });
        }
    }
}
