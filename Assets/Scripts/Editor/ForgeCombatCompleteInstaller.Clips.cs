#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class ForgeCombatCompleteInstaller : EditorWindow
{
    private static AnimationClip CreateStanceClip(string archetype)
    {
        AnimationClip clip = new AnimationClip { frameRate = 60f };
        float dur = 1.6f;

        if (archetype == "Heavy2H" || archetype == "Polearm")
        {
            // Postura de guardia a 2 manos relajada y natural al costado derecho
            bool isPolearm = archetype == "Polearm";
            SetBreathingCurve(clip, "Spine Front-Back", -0.08f, -0.04f, dur);
            SetBreathingCurve(clip, "Chest Twist Left-Right", -0.18f, -0.12f, dur);

            SetBreathingCurve(clip, "Right Arm Down-Up", isPolearm ? -0.22f : -0.15f, isPolearm ? -0.16f : -0.10f, dur);
            SetBreathingCurve(clip, "Right Arm Front-Back", 0.22f, 0.28f, dur);
            SetBreathingCurve(clip, "Right Forearm Stretch", -0.28f, -0.22f, dur);
            SetBreathingCurve(clip, "Right Forearm Twist In-Out", -0.10f, -0.10f, dur);

            SetBreathingCurve(clip, "Left Shoulder Front-Back", 0.22f, 0.25f, dur);
            SetBreathingCurve(clip, "Left Arm Down-Up", -0.15f, -0.10f, dur);
            SetBreathingCurve(clip, "Left Arm Front-Back", 0.48f, 0.54f, dur);
            SetBreathingCurve(clip, "Left Arm Twist In-Out", 0.25f, 0.25f, dur);
            SetBreathingCurve(clip, "Left Forearm Stretch", -0.32f, -0.26f, dur);
        }
        else
        {
            // Postura a 1 mano natural (arma llevada con peso realista sin rigidez)
            float armHeight = (archetype == "Hammer1H" || archetype == "Axe1H") ? -0.08f : -0.22f;
            SetBreathingCurve(clip, "Spine Front-Back", -0.06f, -0.02f, dur);
            SetBreathingCurve(clip, "Chest Twist Left-Right", -0.12f, -0.08f, dur);

            SetBreathingCurve(clip, "Right Arm Down-Up", armHeight, armHeight + 0.05f, dur);
            SetBreathingCurve(clip, "Right Arm Front-Back", 0.25f, 0.32f, dur);
            SetBreathingCurve(clip, "Right Arm Twist In-Out", -0.10f, -0.10f, dur);
            SetBreathingCurve(clip, "Right Forearm Stretch", -0.32f, -0.26f, dur);
            SetBreathingCurve(clip, "Right Forearm Twist In-Out", 0.05f, 0.05f, dur);

            SetBreathingCurve(clip, "Left Arm Down-Up", -0.32f, -0.26f, dur);
            SetBreathingCurve(clip, "Left Arm Front-Back", 0.18f, 0.24f, dur);
            SetBreathingCurve(clip, "Left Forearm Stretch", -0.35f, -0.30f, dur);
        }
        return clip;
    }

    private static AnimationClip CreateAttackClip(string archetype, int step)
    {
        AnimationClip clip = new AnimationClip { frameRate = 60f };
        float dur = archetype switch
        {
            "Dagger"   => 0.52f,
            "Sword1H"  => 0.72f,
            "Axe1H"    => 0.84f,
            "Hammer1H" => 0.95f,
            "Polearm"  => 0.88f,
            "Heavy2H"  => 1.08f,
            _          => 0.78f
        };
        if (step == 5) dur *= 1.22f;
        float tWind = dur * 0.28f;
        float tHit  = dur * 0.52f;
        float tHold = dur * 0.70f;
        switch (archetype)
        {
            case "Dagger":   BuildDaggerCombo(clip, step, tWind, tHit, tHold, dur); break;
            case "Sword1H":  BuildSword1HCombo(clip, step, tWind, tHit, tHold, dur); break;
            case "Axe1H":    BuildAxe1HCombo(clip, step, tWind, tHit, tHold, dur); break;
            case "Hammer1H": BuildHammer1HCombo(clip, step, tWind, tHit, tHold, dur); break;
            case "Heavy2H":  BuildHeavy2HCombo(clip, step, tWind, tHit, tHold, dur); break;
            case "Polearm":  BuildPolearmCombo(clip, step, tWind, tHit, tHold, dur); break;
        }
        return clip;
    }

    // Añade movimiento de piernas, flexión de rodillas y rotación de cadera (Full Body Footwork)
    private static void AddLowerBodyFootwork(AnimationClip c, int step, float intensity, float w, float h, float hold, float d)
    {
        bool rightLead = (step % 2 == 1);
        float leadSign = rightLead ? 1f : -1f;

        // Torsión e inclinación de cadera/columna baja
        AddStrikeCurve(c, "Spine Twist Left-Right", -0.1f * leadSign, 0.28f * leadSign * intensity, -0.35f * leadSign * intensity, -0.30f * leadSign * intensity, -0.1f * leadSign, w, h, hold, d);
        AddStrikeCurve(c, "Spine Left-Right", 0f, -0.12f * leadSign * intensity, 0.15f * leadSign * intensity, 0.12f * leadSign * intensity, 0f, w, h, hold, d);

        if (rightLead)
        {
            // Paso ofensivo con pierna derecha al frente y empuje con pierna izquierda atrás
            AddStrikeCurve(c, "Right Upper Leg Front-Back", 0.1f, -0.15f * intensity, 0.52f * intensity, 0.45f * intensity, 0.1f, w, h, hold, d);
            AddStrikeCurve(c, "Right Lower Leg Stretch", -0.2f, -0.45f * intensity, -0.55f * intensity, -0.48f * intensity, -0.2f, w, h, hold, d);
            AddStrikeCurve(c, "Left Upper Leg Front-Back", 0.05f, 0.25f * intensity, -0.38f * intensity, -0.32f * intensity, 0.05f, w, h, hold, d);
            AddStrikeCurve(c, "Left Lower Leg Stretch", -0.2f, -0.35f * intensity, -0.15f, -0.18f, -0.2f, w, h, hold, d);
        }
        else
        {
            // Paso cruzado con pierna izquierda al frente y empuje con pierna derecha
            AddStrikeCurve(c, "Left Upper Leg Front-Back", 0.1f, -0.15f * intensity, 0.50f * intensity, 0.42f * intensity, 0.1f, w, h, hold, d);
            AddStrikeCurve(c, "Left Lower Leg Stretch", -0.2f, -0.45f * intensity, -0.52f * intensity, -0.45f * intensity, -0.2f, w, h, hold, d);
            AddStrikeCurve(c, "Right Upper Leg Front-Back", 0.05f, 0.25f * intensity, -0.35f * intensity, -0.30f * intensity, 0.05f, w, h, hold, d);
            AddStrikeCurve(c, "Right Lower Leg Stretch", -0.2f, -0.35f * intensity, -0.15f, -0.18f, -0.2f, w, h, hold, d);
        }

        // Separación firme de piernas para dar base al golpe
        AddStrikeCurve(c, "Right Upper Leg In-Out", -0.1f, -0.15f, -0.28f * intensity, -0.25f * intensity, -0.1f, w, h, hold, d);
        AddStrikeCurve(c, "Left Upper Leg In-Out", -0.1f, -0.15f, -0.28f * intensity, -0.25f * intensity, -0.1f, w, h, hold, d);
    }

    private static void AddStrikeCurve(AnimationClip clip, string muscle, float start, float windup, float hit, float hold, float end, float tW, float tH, float tHold, float dur)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, start),
            new Keyframe(tW, windup),
            new Keyframe(tH, hit),
            new Keyframe(tHold, hold),
            new Keyframe(dur, end)
        );
        for (int i = 0; i < curve.keys.Length; i++)
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);

        clip.SetCurve("", typeof(Animator), muscle, curve);
    }

    private static void SetBreathingCurve(AnimationClip clip, string muscle, float v1, float v2, float dur)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, v1),
            new Keyframe(dur * 0.5f, v2),
            new Keyframe(dur, v1)
        );
        clip.SetCurve("", typeof(Animator), muscle, curve);
    }

    private static void SaveClip(AnimationClip clip, string fileName, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string fullPath = $"{OUTPUT_PATH}/{fileName}";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
        if (existing != null)
            EditorUtility.CopySerialized(clip, existing);
        else
            AssetDatabase.CreateAsset(clip, fullPath);
    }
}
#endif
