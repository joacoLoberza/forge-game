#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public partial class ForgeCombatCompleteInstaller : EditorWindow
{
    private const string OUTPUT_PATH = "Assets/Animations/GeneratedCombat";
    private const string CONTROLLER_PATH = "Assets/Animations/Character/PlayerAnimator.controller";

    [MenuItem("ForgeTools/1. Instalar Sistema Completo de Animaciones y Capas (V2 Full-Body)", false, 1)]
    public static void InstallCompleteCombatSystem()
    {
        if (!Directory.Exists(OUTPUT_PATH))
            Directory.CreateDirectory(OUTPUT_PATH);

        // 1. Crear clips plantilla base
        AnimationClip baseStance = CreateStanceClip("Sword1H");
        SaveClip(baseStance, "Base_CombatStance.anim", true);
        baseStance = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OUTPUT_PATH}/Base_CombatStance.anim");

        AnimationClip[] baseAttacks = new AnimationClip[5];
        for (int s = 1; s <= 5; s++)
        {
            AnimationClip c = CreateAttackClip("Sword1H", s);
            SaveClip(c, $"Base_Attack{s}.anim", false);
            baseAttacks[s - 1] = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OUTPUT_PATH}/Base_Attack{s}.anim");
        }

        // 2. Crear AvatarMask para Upper Body
        AvatarMask upperMask = CreateOrUpdateUpperBodyMask();

        // 3. Configurar PlayerAnimator.controller (Layer 0 Cuerpo Completo + Layer 1 Tren Superior)
        ConfigurePlayerAnimatorController(upperMask, baseStance, baseAttacks);

        // 4. Generar las 36 animaciones con biomecánica de cuerpo completo y enlazar Overrides
        string[] archetypes = { "Dagger", "Sword1H", "Axe1H", "Hammer1H", "Heavy2H", "Polearm" };
        foreach (string arch in archetypes)
        {
            AnimationClip stanceClip = CreateStanceClip(arch);
            SaveClip(stanceClip, $"{arch}_CombatStance.anim", true);
            AnimationClip savedStance = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OUTPUT_PATH}/{arch}_CombatStance.anim");

            AnimationClip[] savedAttacks = new AnimationClip[5];
            for (int step = 1; step <= 5; step++)
            {
                AnimationClip attackClip = CreateAttackClip(arch, step);
                SaveClip(attackClip, $"{arch}_Attack{step}.anim", false);
                savedAttacks[step - 1] = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OUTPUT_PATH}/{arch}_Attack{step}.anim");
            }

            AssignToOverrideController(arch, baseStance, baseAttacks, savedStance, savedAttacks);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ForgeCombatInstaller V2] 36 animaciones Full-Body regeneradas con éxito.");
    }

    private static AvatarMask CreateOrUpdateUpperBodyMask()
    {
        string maskPath = $"{OUTPUT_PATH}/UpperBodyCombatMask.mask";
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, maskPath);
        }

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            AvatarMaskBodyPart part = (AvatarMaskBodyPart)i;
            bool isUpper = (part == AvatarMaskBodyPart.Body ||
                            part == AvatarMaskBodyPart.Head ||
                            part == AvatarMaskBodyPart.LeftArm ||
                            part == AvatarMaskBodyPart.RightArm ||
                            part == AvatarMaskBodyPart.LeftFingers ||
                            part == AvatarMaskBodyPart.RightFingers ||
                            part == AvatarMaskBodyPart.LeftHandIK ||
                            part == AvatarMaskBodyPart.RightHandIK);
            mask.SetHumanoidBodyPartActive(part, isUpper);
        }

        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static void ConfigurePlayerAnimatorController(AvatarMask upperMask, AnimationClip baseStance, AnimationClip[] baseAttacks)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            string[] found = AssetDatabase.FindAssets("PlayerAnimator t:AnimatorController");
            if (found.Length > 0)
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(found[0]));
        }
        if (controller == null) return;

        bool hasAttackSpeed = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "AttackSpeed") { hasAttackSpeed = true; break; }
        }
        if (!hasAttackSpeed)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "AttackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f
            });
        }

        AnimatorControllerLayer[] layers = controller.layers;
        int upperLayerIndex = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name.Contains("Upper")) { upperLayerIndex = i; break; }
        }
        if (upperLayerIndex == -1)
        {
            controller.AddLayer("Upper Body");
            layers = controller.layers;
            upperLayerIndex = layers.Length - 1;
        }

        SetupAttackStatesInStateMachine(layers[0].stateMachine, baseStance, baseAttacks, false);

        layers[upperLayerIndex].avatarMask = upperMask;
        layers[upperLayerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[upperLayerIndex].defaultWeight = 0.7f;
        controller.layers = layers;

        SetupAttackStatesInStateMachine(layers[upperLayerIndex].stateMachine, baseStance, baseAttacks, true);
        EditorUtility.SetDirty(controller);
    }

    private static void SetupAttackStatesInStateMachine(AnimatorStateMachine sm, AnimationClip baseStance, AnimationClip[] baseAttacks, bool isUpperLayer)
    {
        AnimatorState stanceState = FindOrCreateState(sm, "CombatStance", new Vector3(300, 50, 0));
        stanceState.motion = baseStance;
        stanceState.speedParameterActive = false;

        AnimatorState locomotionState = FindOrCreateState(sm, "Locomotion", new Vector3(250, 120, 0));

        if (isUpperLayer)
            sm.defaultState = stanceState;

        for (int i = 0; i < 5; i++)
        {
            string stateName = $"Attack{i + 1}";
            AnimatorState atkState = FindOrCreateState(sm, stateName, new Vector3(550, i * 70, 0));
            atkState.motion = baseAttacks[i];
            atkState.speedParameterActive = true;
            atkState.speedParameter = "AttackSpeed";

            atkState.transitions = new AnimatorStateTransition[0];
            AnimatorState targetReturn = isUpperLayer ? stanceState : locomotionState;
            if (targetReturn != null)
            {
                AnimatorStateTransition exitTrans = atkState.AddTransition(targetReturn);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime = 0.92f;
                exitTrans.duration = 0.18f;
            }
        }
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name, Vector3 pos)
    {
        foreach (var child in sm.states)
        {
            if (child.state.name == name) return child.state;
        }
        return sm.AddState(name, pos);
    }

    private static void AssignToOverrideController(string archetype, AnimationClip baseStance, AnimationClip[] baseAttacks, AnimationClip archStance, AnimationClip[] archAttacks)
    {
        string[] guids = AssetDatabase.FindAssets($"{archetype}_Override t:AnimatorOverrideController");
        if (guids.Length == 0) return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        AnimatorOverrideController aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (aoc == null) return;

        aoc[baseStance] = archStance;
        for (int i = 0; i < 5; i++)
            aoc[baseAttacks[i]] = archAttacks[i];

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        aoc.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            string kName = overrides[i].Key.name;
            if (kName.Contains("CombatIdle") || kName.Contains("Stance"))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, archStance);
            else if (kName.Contains("Attack1H01_R") || kName == "Base_Attack1")
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, archAttacks[0]);
            else if (kName.Contains("Attack1H01_L") || kName == "Base_Attack2")
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, archAttacks[1]);
            else if (kName.Contains("AttackShield01") || kName == "Base_Attack3")
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, archAttacks[2]);
            else if (kName.Contains("AttackPolearm01") || kName == "Base_Attack4")
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, archAttacks[3]);
            else if (kName.Contains("Attack2H01") || kName == "Base_Attack5")
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, archAttacks[4]);
        }
        aoc.ApplyOverrides(overrides);
        EditorUtility.SetDirty(aoc);
    }
}
#endif

