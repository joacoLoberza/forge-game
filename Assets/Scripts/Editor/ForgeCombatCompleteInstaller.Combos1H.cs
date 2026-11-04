#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class ForgeCombatCompleteInstaller : EditorWindow
{
    // --- 1. DAGA ---
    private static void BuildDaggerCombo(AnimationClip c, int step, float w, float h, float hold, float d)
    {
        AddLowerBodyFootwork(c, step, 0.75f, w, h, hold, d);
        if (step == 1 || step == 3)
        {
            AddStrikeCurve(c, "Spine Front-Back", -0.06f, 0.05f, -0.18f, -0.15f, -0.06f, w, h, hold, d);
            AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.25f, -0.35f, -0.30f, -0.15f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Down-Up", -0.1f, 0.2f, -0.1f, -0.15f, -0.1f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, 0.05f, 0.65f, 0.6f, 0.30f, w, h, hold, d);
            AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.55f, 0.1f, 0.0f, -0.35f, w, h, hold, d);
            AddStrikeCurve(c, "Right Forearm Twist In-Out", 0.05f, 0.2f, -0.25f, -0.2f, 0.05f, w, h, hold, d);
        }
        else if (step == 2)
        {
            AddStrikeCurve(c, "Chest Twist Left-Right", -0.25f, -0.42f, 0.35f, 0.3f, -0.15f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Front-Back", 0.35f, 0.68f, -0.1f, -0.05f, 0.30f, w, h, hold, d);
            AddStrikeCurve(c, "Right Forearm Stretch", -0.3f, -0.65f, 0.2f, 0.1f, -0.35f, w, h, hold, d);
            AddStrikeCurve(c, "Right Forearm Twist In-Out", -0.2f, 0.3f, 0.25f, 0.2f, 0.05f, w, h, hold, d);
        }
        else
        {
            float power = (step == 5) ? 1.25f : 1.0f;
            AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.12f, -0.32f * power, -0.28f, -0.08f, w, h, hold, d);
            AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.35f, -0.45f, -0.4f, -0.15f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, -0.30f, 0.05f, 0.0f, -0.15f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, -0.25f, 0.75f, 0.7f, 0.30f, w, h, hold, d);
            AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.75f, 0.35f, 0.25f, -0.35f, w, h, hold, d);
            AddStrikeCurve(c, "Right Hand Down-Up", 0f, 0.15f, -0.55f, -0.45f, 0f, w, h, hold, d);
        }
    }

    // --- 2. ESPADA 1H ---
    private static void BuildSword1HCombo(AnimationClip c, int step, float w, float h, float hold, float d)
    {
        AddLowerBodyFootwork(c, step, 0.9f, w, h, hold, d);
        switch (step)
        {
            case 1:
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.12f, -0.25f, -0.2f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.35f, -0.4f, -0.35f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, 0.42f, -0.32f, -0.28f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, -0.05f, 0.65f, 0.6f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.55f, 0.15f, 0.1f, -0.35f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Twist In-Out", 0.05f, 0.18f, -0.22f, -0.18f, 0.05f, w, h, hold, d);
                break;
            case 2:
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.30f, -0.45f, 0.4f, 0.35f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.25f, 0.05f, 0.1f, 0.05f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.55f, 0.68f, -0.15f, -0.1f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", 0.05f, -0.55f, 0.25f, 0.15f, -0.35f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Twist In-Out", -0.2f, 0.28f, 0.25f, 0.2f, 0.05f, w, h, hold, d);
                break;
            case 3:
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, -0.25f, 0.08f, 0.04f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", 0.25f, 0.4f, -0.35f, -0.3f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.1f, -0.45f, 0.38f, 0.32f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.1f, -0.15f, 0.65f, 0.55f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Twist In-Out", 0.2f, -0.25f, -0.22f, -0.18f, 0.05f, w, h, hold, d);
                break;
            case 4:
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.15f, -0.35f, -0.3f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.35f, -0.48f, -0.42f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, -0.25f, 0.1f, 0.05f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, -0.25f, 0.75f, 0.7f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.65f, 0.35f, 0.25f, -0.35f, w, h, hold, d);
                AddStrikeCurve(c, "Right Hand Down-Up", 0f, 0.1f, -0.5f, -0.4f, 0f, w, h, hold, d);
                break;
            case 5:
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.3f, -0.55f, -0.5f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, 0.6f, -0.5f, -0.45f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, 0.2f, 0.55f, 0.5f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.6f, 0.25f, 0.2f, -0.35f, w, h, hold, d);
                break;
        }
    }

    // --- 3. HACHA 1H ---
    private static void BuildAxe1HCombo(AnimationClip c, int step, float w, float h, float hold, float d)
    {
        AddLowerBodyFootwork(c, step, 1.0f, w, h, hold, d);
        switch (step)
        {
            case 1: // Hendidura diagonal pesada (Arriba a Derecha -> Abajo a Izquierda)
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.18f, -0.38f, -0.32f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.42f, -0.42f, -0.36f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.05f, 0.48f, -0.38f, -0.32f, -0.05f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, -0.12f, 0.66f, 0.58f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.62f, 0.22f, 0.16f, -0.35f, w, h, hold, d);
                break;
            case 2: // Corte ascendente desgarrador desde abajo (Izquierda a Derecha)
                AddStrikeCurve(c, "Spine Front-Back", -0.15f, -0.32f, 0.12f, 0.08f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.35f, -0.48f, 0.42f, 0.36f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.30f, -0.45f, 0.35f, 0.28f, -0.05f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.55f, 0.65f, -0.10f, -0.05f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Twist In-Out", -0.15f, 0.25f, 0.22f, 0.15f, 0.05f, w, h, hold, d);
                break;
            case 3: // Hacha vertical directa al cráneo/escudo
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.28f, -0.48f, -0.42f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.05f, 0.58f, -0.48f, -0.42f, -0.05f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, 0.18f, 0.55f, 0.50f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.68f, 0.28f, 0.22f, -0.35f, w, h, hold, d);
                break;
            case 4: // Tajo horizontal de leñador con giro de cadera
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, -0.12f, -0.25f, -0.20f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.48f, -0.48f, -0.42f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.05f, 0.08f, 0.02f, 0.0f, -0.05f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, -0.20f, 0.70f, 0.62f, 0.30f, w, h, hold, d);
                break;
            case 5: // Finisher: Guillotina pesada con caída de torso
                AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.35f, -0.58f, -0.52f, -0.08f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.05f, 0.65f, -0.58f, -0.52f, -0.05f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, 0.25f, 0.58f, 0.52f, 0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.72f, 0.32f, 0.26f, -0.35f, w, h, hold, d);
                break;
        }
    }

    // --- 4. MARTILLO 1H ---
    private static void BuildHammer1HCombo(AnimationClip c, int step, float w, float h, float hold, float d)
    {
        AddLowerBodyFootwork(c, step, 1.1f, w, h, hold, d);
        if (step == 1 || step == 4)
        {
            AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.25f, -0.45f, -0.42f, -0.08f, w, h, hold, d);
            AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.42f, -0.42f, -0.38f, -0.15f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Down-Up", -0.05f, 0.55f, -0.45f, -0.42f, -0.05f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, 0.05f, 0.60f, 0.58f, 0.30f, w, h, hold, d);
            AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.68f, 0.25f, 0.20f, -0.35f, w, h, hold, d);
        }
        else if (step == 2 || step == 3)
        {
            float dir = (step == 2) ? -1f : 1f;
            AddStrikeCurve(c, "Spine Front-Back", -0.08f, -0.18f, -0.28f, -0.24f, -0.08f, w, h, hold, d);
            AddStrikeCurve(c, "Chest Twist Left-Right", -0.15f, 0.48f * dir, -0.48f * dir, -0.44f * dir, -0.15f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Down-Up", -0.05f, -0.15f, 0.18f, 0.12f, -0.05f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, (dir > 0) ? -0.18f : 0.68f, (dir > 0) ? 0.68f : -0.18f, 0.30f, 0.30f, w, h, hold, d);
        }
        else
        {
            AddStrikeCurve(c, "Spine Front-Back", -0.08f, 0.38f, -0.65f, -0.60f, -0.08f, w, h, hold, d);
            AddStrikeCurve(c, "Chest Front-Back", 0f, 0.25f, -0.45f, -0.40f, 0f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Down-Up", -0.05f, 0.65f, -0.60f, -0.55f, -0.05f, w, h, hold, d);
            AddStrikeCurve(c, "Right Arm Front-Back", 0.30f, 0.30f, 0.60f, 0.55f, 0.30f, w, h, hold, d);
            AddStrikeCurve(c, "Right Forearm Stretch", -0.35f, -0.75f, 0.35f, 0.30f, -0.35f, w, h, hold, d);
        }
    }
}
#endif
