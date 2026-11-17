#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Configura animaciones externas reales (UAL2 + Kevin Iglesias + KayKit Rig_Medium_CombatMelee CC0)
    /// por Tipo y Longitud de arma, sin movimientos absurdos (cero lanzamientos/puños) y con máscara
    /// de tren superior limpia para ataques en movimiento sin deslizamiento de piernas.
    /// </summary>
    [InitializeOnLoad]
    public static partial class CombatMeleeSetupTool
    {
        private const string KayKitMeleeFbx = "Assets/Resources/Animations/Combat/Rig_Medium_CombatMelee.fbx";
        private const string Ual2Fbx = "Assets/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx";
        private static readonly string[] BaseCtrlPaths = { "Assets/Animations/Character/PlayerAnimator.controller", "Assets/Resources/PlayerAnimator.controller" };
        private static readonly string[] OutFolders = { "Assets/Resources/Animations", "Assets/Animations/Character" };
        private static readonly string[] Ual2CombatKeys = { "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "Sword_Dash", "Sword_Heavy_Combo", "TreeChopping_Loop" };

        static CombatMeleeSetupTool() => EditorApplication.delayCall += EnsureExternalCombatAnimationsInstalled;

        [MenuItem("Tools/Forja/Instalar Animaciones Externas de Combate (UAL2 + KI + KayKit)")]
        public static void EnsureExternalCombatAnimationsInstalled()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Ual2Fbx)) return;

            var ualImp = AssetImporter.GetAtPath(Ual2Fbx) as ModelImporter;
            if (ualImp != null && ConfigureExternalHumanoidImporter(ualImp, Ual2CombatKeys, isKayKit: false))
                ualImp.SaveAndReimport();

            if (File.Exists(KayKitMeleeFbx))
            {
                var kkImp = AssetImporter.GetAtPath(KayKitMeleeFbx) as ModelImporter;
                if (kkImp != null && ConfigureExternalHumanoidImporter(kkImp, null, isKayKit: true))
                    kkImp.SaveAndReimport();
            }

            AvatarMask upperMask = EnsureCleanUpperBodyMask();
            RuntimeAnimatorController primaryBaseCtrl = null;
            foreach (string cPath in BaseCtrlPaths)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(cPath);
                if (ctrl != null)
                {
                    ConfigureFullBodyCombatController(ctrl, upperMask);
                    if (cPath == BaseCtrlPaths[0]) primaryBaseCtrl = ctrl;
                }
            }
            if (primaryBaseCtrl == null) return;

            var cMap = LoadExternalClips();
            if (!cMap.ContainsKey("UalSwC") && !cMap.ContainsKey("KK1HChop")) return;

            string chop1H = cMap.ContainsKey("KK1HChop") ? "KK1HChop" : "UalChop";
            string jumpChop1H = cMap.ContainsKey("KK1HJumpChop") ? "KK1HJumpChop" : chop1H;
            string sliceDiag = cMap.ContainsKey("KK1HSliceDiag") ? "KK1HSliceDiag" : (cMap.ContainsKey("KI1H_R") ? "KI1H_R" : "UalSwC");
            string sliceHoriz = cMap.ContainsKey("KK1HSliceHoriz") ? "KK1HSliceHoriz" : (cMap.ContainsKey("KI1H_R") ? "KI1H_R" : "UalSwDash");
            string stab1H = cMap.ContainsKey("KK1HStab") ? "KK1HStab" : "UalSwDash";
            string right1H = cMap.ContainsKey("KI1H_R") ? "KI1H_R" : chop1H;
            string chop2H = cMap.ContainsKey("KK2HChop") ? "KK2HChop" : "KI2H";
            string slice2H = cMap.ContainsKey("KK2HSlice") ? "KK2HSlice" : "NoIKGS";
            string spin2H = cMap.ContainsKey("KK2HSpin") ? "KK2HSpin" : "NoIKGS";
            string stab2H = cMap.ContainsKey("KK2HStab") ? "KK2HStab" : "KIPole";

            // Todos los combos usan mano derecha en 1H con swings completos (sin clips *_Rec) y AMBAS MANOS en 2H
            var defs = new (string name, string stance, string[] a)[]
            {
                // Espadas (Corto/Mediano/Largo): tajos amplios y estocadas reales para 1H; 2H estricto en Mandoble
                ("SwordShort_Override", "Idle1H",  new[] { sliceDiag,  sliceHoriz, chop1H,     stab1H,      "UalSwC" }),
                ("SwordMed_Override",   "Idle1H",  new[] { sliceHoriz, sliceDiag,  right1H,    "UalSwDash", "UalSwC" }),
                ("Sword1H_Override",    "Idle1H",  new[] { sliceDiag,  sliceHoriz, chop1H,     "UalSwDash", "UalSwC" }),
                ("SwordLong_Override",  "Idle2H",  new[] { "NoIKGS",   "KI2H",     slice2H,    spin2H,      chop2H }),
                // Hachas: cortes y tajos descendentes reales (2H en larga)
                ("AxeShort_Override",   "Idle1H",  new[] { chop1H,     sliceDiag,  "UalChop",  sliceHoriz,  jumpChop1H }),
                ("AxeMed_Override",     "Idle1H",  new[] { chop1H,     "UalChop",  sliceDiag,  sliceHoriz,  chop2H }),
                ("AxeLong_Override",    "Idle2H",  new[] { chop2H,     slice2H,    "KI2H",     "NoIKGS",    spin2H }),
                // Martillos / Mazas: golpes contundentes verticales y barridos pesados
                ("HammerShort_Override","Idle1H",  new[] { chop1H,     "UalChop",  sliceDiag,  jumpChop1H,  chop2H }),
                ("HammerMed_Override",  "Idle1H",  new[] { chop1H,     jumpChop1H, "UalChop",  sliceHoriz,  "KI2H" }),
                ("HammerLong_Override", "Idle2H",  new[] { "KI2H",     chop2H,     "NoIKGS",   slice2H,     stab2H }),
                // Pico: ÚNICO tipo de golpe de minería en los 5 slots
                ("Pickaxe_Override",    "Idle2H",  new[] { "UalChop",  "UalChop",  "UalChop",  "UalChop",   "UalChop" }),
                // Aliases legados consistentes
                ("Axe1H_Override",      "Idle1H",  new[] { chop1H,     "UalChop",  sliceDiag,  sliceHoriz,  chop2H }),
                ("Hammer1H_Override",   "Idle1H",  new[] { chop1H,     jumpChop1H, "UalChop",  sliceHoriz,  "KI2H" }),
                ("Hammer2H_Override",   "Idle2H",  new[] { "KI2H",     chop2H,     "NoIKGS",   slice2H,     stab2H }),
                ("Heavy2H_Override",    "Idle2H",  new[] { "NoIKGS",   "KI2H",     slice2H,    chop2H,      spin2H }),
                ("Polearm_Override",    "IdlePole",new[] { stab2H,     "NoIKSpear","KIPole",   slice2H,     "KI2H" }),
                ("Dagger_Override",     "Idle1H",  new[] { stab1H,     sliceHoriz, sliceDiag,  "UalSwDash", chop1H })
            };

            foreach (string folder in OutFolders)
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                foreach (var d in defs) InstallOverride(folder, d.name, primaryBaseCtrl, cMap, d.stance, d.a);
            }
            AssetDatabase.SaveAssets();
        }

        private static bool ConfigureExternalHumanoidImporter(ModelImporter imp, string[] forceRootLockTakes, bool isKayKit)
        {
            bool dirty = false;
            if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; dirty = true; }
            if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
            if (isKayKit && EnsureKayKitHumanMapping(imp)) dirty = true;

            var clips = imp.clipAnimations != null && imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    if (forceRootLockTakes != null)
                    {
                        bool match = false;
                        foreach (string k in forceRootLockTakes)
                            if (c.name == k || c.takeName == k || c.takeName.EndsWith("|" + k) || c.name.EndsWith("|" + k)) { match = true; break; }
                        if (!match) continue;
                    }
                    bool shouldLoop = isKayKit && (c.name.EndsWith("Idle") || c.name.EndsWith("Blocking"));
                    if (!c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ || !c.keepOriginalPositionY || !c.heightFromFeet || c.loopTime != shouldLoop)
                    {
                        c.lockRootRotation = true; c.keepOriginalOrientation = true;
                        c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = true;
                        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true; c.loopTime = shouldLoop;
                        clips[i] = c; dirty = true;
                    }
                }
                if (dirty) imp.clipAnimations = clips;
            }
            return dirty;
        }

        private static void ConfigureFullBodyCombatController(AnimatorController ctrl, AvatarMask upperMask)
        {
            if (ctrl == null || ctrl.layers == null || ctrl.layers.Length == 0) return;
            var layers = ctrl.layers;
            if (!layers[0].iKPass) layers[0].iKPass = true;
            if (layers.Length > 1)
            {
                layers[1].iKPass = true;
                if (upperMask != null) layers[1].avatarMask = upperMask;
                ConfigureLayerStates(layers[1].stateMachine, "CombatStance");
            }
            if (layers.Length > 2 && layers[2].iKPass) layers[2].iKPass = false;
            ConfigureLayerStates(layers[0].stateMachine, "Locomotion");
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
        }

        private static Dictionary<string, AnimationClip> LoadExternalClips()
        {
            var m = new Dictionary<string, AnimationClip>();
            AddSub(m, Ual2Fbx, "UalSwC",    "Sword_Regular_C");
            AddSub(m, Ual2Fbx, "UalSwDash", "Sword_Dash");
            AddSub(m, Ual2Fbx, "UalChop",   "TreeChopping_Loop");
            AddFbx(m, "KI1H_R",    "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@Attack1H01_R.fbx");
            if (File.Exists(KayKitMeleeFbx))
            {
                AddSub(m, KayKitMeleeFbx, "KK1HChop",       "Melee_1H_Attack_Chop");
                AddSub(m, KayKitMeleeFbx, "KK1HJumpChop",   "Melee_1H_Attack_Jump_Chop");
                AddSub(m, KayKitMeleeFbx, "KK1HSliceDiag",  "Melee_1H_Attack_Slice_Diagonal");
                AddSub(m, KayKitMeleeFbx, "KK1HSliceHoriz", "Melee_1H_Attack_Slice_Horizontal");
                AddSub(m, KayKitMeleeFbx, "KK1HStab",       "Melee_1H_Attack_Stab");
                AddSub(m, KayKitMeleeFbx, "KK2HChop",       "Melee_2H_Attack_Chop");
                AddSub(m, KayKitMeleeFbx, "KK2HSlice",      "Melee_2H_Attack_Slice");
                AddSub(m, KayKitMeleeFbx, "KK2HSpin",       "Melee_2H_Attack_Spin");
                AddSub(m, KayKitMeleeFbx, "KK2HStab",       "Melee_2H_Attack_Stab");
            }
            AddFbx(m, "KI2H",      "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/2H/HumanM@Attack2H01.fbx");
            AddFbx(m, "KIPole",    "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Polearm/HumanM@AttackPolearm01.fbx");
            AddFbx(m, "NoIKGS",    "Assets/Kevin Iglesias/IKHelperTool/Animations/NoIK@GreatswordAttack.fbx");
            AddFbx(m, "NoIKSpear", "Assets/Kevin Iglesias/IKHelperTool/Animations/NoIK@SpearAttack.fbx");
            AddFbx(m, "Idle1H",    "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@CombatIdle1H01.fbx");
            AddFbx(m, "Idle2H",    "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/2H/HumanM@CombatIdle2H01.fbx");
            AddFbx(m, "IdlePole",  "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Polearm/HumanM@CombatIdlePolearm01.fbx");
            return m;
        }

        private static void AddSub(Dictionary<string, AnimationClip> m, string fbx, string key, string clipName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__") && (c.name == clipName || c.name.EndsWith("|" + clipName)))
                { m[key] = c; return; }
        }

        private static void AddFbx(Dictionary<string, AnimationClip> m, string key, string fbx)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (obj is AnimationClip c && !c.name.StartsWith("__preview__")) { m[key] = c; return; }
        }

        private static void InstallOverride(string folder, string name, RuntimeAnimatorController baseCtrl, Dictionary<string, AnimationClip> cMap, string stanceKey, string[] atkKeys)
        {
            string path = $"{folder}/{name}.overrideController";
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController(baseCtrl) { name = name };
                AssetDatabase.CreateAsset(aoc, path);
            }
            else if (aoc.runtimeAnimatorController != baseCtrl) aoc.runtimeAnimatorController = baseCtrl;

            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(list);
            bool changed = false;
            for (int i = 0; i < list.Count; i++)
            {
                var orig = list[i].Key; if (orig == null) continue;
                string k = orig.name, pick = null;
                if (k.Contains("CombatIdle1H01")) pick = stanceKey;
                else if (k.Contains("Attack1H01_R")) pick = atkKeys[0];
                else if (k.Contains("Attack1H01_L")) pick = atkKeys[1];
                else if (k.Contains("AttackShield01")) pick = atkKeys[2];
                else if (k.Contains("AttackPolearm01")) pick = atkKeys[3];
                else if (k.Contains("Attack2H01")) pick = atkKeys[4];

                if (pick != null && cMap.TryGetValue(pick, out var dst) && dst != null && list[i].Value != dst)
                { list[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, dst); changed = true; }
            }
            if (changed) { aoc.ApplyOverrides(list); EditorUtility.SetDirty(aoc); }
        }
    }
}
#endif
