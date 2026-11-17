#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    public static partial class CombatMeleeSetupTool
    {
        private static readonly (string human, string bone)[] KayKitBoneMap =
        {
            ("Hips", "hips"), ("Spine", "spine"), ("Chest", "chest"), ("Head", "head"),
            ("LeftUpperArm", "upperarm.l"), ("LeftLowerArm", "lowerarm.l"), ("LeftHand", "hand.l"),
            ("RightUpperArm", "upperarm.r"), ("RightLowerArm", "lowerarm.r"), ("RightHand", "hand.r"),
            ("LeftUpperLeg", "upperleg.l"), ("LeftLowerLeg", "lowerleg.l"), ("LeftFoot", "foot.l"), ("LeftToes", "toes.l"),
            ("RightUpperLeg", "upperleg.r"), ("RightLowerLeg", "lowerleg.r"), ("RightFoot", "foot.r"), ("RightToes", "toes.r")
        };

        private static bool EnsureKayKitHumanMapping(ModelImporter imp)
        {
            if (imp == null) return false;
            var hd = imp.humanDescription;
            bool hasAll = hd.human != null && hd.human.Length >= KayKitBoneMap.Length;
            if (hasAll) return false;

            var list = new HumanBone[KayKitBoneMap.Length];
            for (int i = 0; i < KayKitBoneMap.Length; i++)
            {
                list[i] = new HumanBone
                {
                    humanName = KayKitBoneMap[i].human,
                    boneName = KayKitBoneMap[i].bone,
                    limit = new HumanLimit { useDefaultValues = true }
                };
            }
            hd.human = list;
            hd.upperArmTwist = 0.5f; hd.lowerArmTwist = 0.5f;
            hd.upperLegTwist = 0.5f; hd.lowerLegTwist = 0.5f;
            hd.armStretch = 0.05f; hd.legStretch = 0.05f; hd.feetSpacing = 0f;
            imp.humanDescription = hd;
            return true;
        }

        private static AvatarMask EnsureCleanUpperBodyMask()
        {
            const string maskPath = "Assets/Resources/Animations/Combat/UpperBodyCombatMask.mask";
            string dir = Path.GetDirectoryName(maskPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            bool created = mask == null;
            if (created) mask = new AvatarMask();

            bool dirty = created;
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var p = (AvatarMaskBodyPart)i;
                bool active = p == AvatarMaskBodyPart.Body || p == AvatarMaskBodyPart.Head ||
                              p == AvatarMaskBodyPart.LeftArm || p == AvatarMaskBodyPart.RightArm ||
                              p == AvatarMaskBodyPart.LeftFingers || p == AvatarMaskBodyPart.RightFingers ||
                              p == AvatarMaskBodyPart.LeftHandIK || p == AvatarMaskBodyPart.RightHandIK;
                if (mask.GetHumanoidBodyPartActive(p) != active) { mask.SetHumanoidBodyPartActive(p, active); dirty = true; }
            }
            if (mask.transformCount > 0) { mask.transformCount = 0; dirty = true; }

            if (created) AssetDatabase.CreateAsset(mask, maskPath);
            else if (dirty) EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void ConfigureLayerStates(AnimatorStateMachine sm, string idleStateName)
        {
            if (sm == null) return;
            AnimatorState idleState = null;
            var atkStates = new List<AnimatorState>();
            foreach (var cs in sm.states)
            {
                if (cs.state == null) continue;
                if (cs.state.name == idleStateName) idleState = cs.state;
                else if (cs.state.name.StartsWith("Attack")) atkStates.Add(cs.state);
            }
            foreach (var st in atkStates)
            {
                st.speedParameterActive = true; st.speedParameter = "AttackSpeed";
                st.mirrorParameterActive = false; st.mirror = false;
                if (idleState != null && st.transitions.Length == 0)
                {
                    var tr = st.AddTransition(idleState);
                    tr.hasExitTime = true; tr.exitTime = 0.90f; tr.duration = 0.12f; tr.hasFixedDuration = true;
                }
                else
                {
                    foreach (var tr in st.transitions)
                    {
                        if (tr.destinationState == idleState)
                        {
                            tr.exitTime = 0.90f;
                            tr.duration = 0.12f;
                        }
                    }
                }
            }
        }
    }
}
#endif

