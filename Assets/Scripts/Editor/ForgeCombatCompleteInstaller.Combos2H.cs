#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public partial class ForgeCombatCompleteInstaller : EditorWindow
{
    // --- 5. HEAVY 2H (Hachas de Mango Largo, Grandes Hachas y Mazas Pesadas: CERO ESTOCADAS, 5 GOLPES DE PESO DISTINTOS) ---
    private static void BuildHeavy2HCombo(AnimationClip c, int step, float w, float h, float hold, float d)
    {
        AddLowerBodyFootwork(c, step, 1.25f, w, h, hold, d);
        switch (step)
        {
            case 1: // Golpe 1: Gran Hendidura Diagonal Descendente (Aprovecha el peso de la cabeza desde arriba a la derecha)
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, 0.22f, -0.42f, -0.38f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.18f, 0.48f, -0.48f, -0.42f, -0.18f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, 0.52f, -0.42f, -0.36f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, -0.05f, 0.65f, 0.58f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.15f, 0.45f, -0.38f, -0.32f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.68f, 0.25f, 0.30f, 0.50f, w, h, hold, d);
                break;

            case 2: // Golpe 2: Corte Ascendente Brutal (Arrastra la cabeza pesada desde abajo a la izquierda hacia arriba)
                AddStrikeCurve(c, "Spine Front-Back", -0.25f, -0.38f, 0.15f, 0.10f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.42f, -0.52f, 0.45f, 0.38f, -0.18f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.35f, -0.50f, 0.42f, 0.35f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.60f, 0.68f, -0.05f, 0.0f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.32f, -0.45f, 0.38f, 0.30f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.25f, 0.15f, 0.68f, 0.60f, 0.50f, w, h, hold, d);
                break;

            case 3: // Golpe 3: Aplastamiento Vertical Directo Sobre la Cabeza (Overhead Crush)
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, 0.32f, -0.55f, -0.50f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.18f, 0.0f, 0.0f, 0.0f, -0.18f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, 0.62f, -0.52f, -0.46f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, 0.42f, 0.58f, 0.52f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.15f, 0.58f, -0.48f, -0.42f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.55f, 0.62f, 0.56f, 0.50f, w, h, hold, d);
                break;

            case 4: // Golpe 4: Barrido Pendular Horizontal Amplio (Inercia centrífuga de la cabeza pesada)
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, -0.15f, -0.28f, -0.22f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.18f, 0.55f, -0.55f, -0.48f, -0.18f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, 0.05f, 0.0f, -0.05f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, -0.15f, 0.70f, 0.62f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.15f, 0.05f, 0.0f, -0.05f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.75f, 0.15f, 0.22f, 0.50f, w, h, hold, d);
                break;

            case 5: // Golpe 5: Ejecución del Verdugo (Gran carga tras el hombro y caída devastadora contra el piso)
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, 0.40f, -0.68f, -0.62f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Front-Back", 0f, 0.25f, -0.42f, -0.38f, 0f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.18f, 0.35f, -0.25f, -0.20f, -0.18f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.15f, 0.68f, -0.60f, -0.55f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, 0.35f, 0.62f, 0.56f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.15f, 0.62f, -0.55f, -0.50f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.55f, 0.65f, 0.58f, 0.50f, w, h, hold, d);
                break;
        }
    }

    // --- 6. POLEARM / MANDOBLE LARGO (Espadas largas / Mandobles / Lanzas: Combina Tajos, Cortes Ascendentes y Estocadas) ---
    private static void BuildPolearmCombo(AnimationClip c, int step, float w, float h, float hold, float d)
    {
        AddLowerBodyFootwork(c, step, 1.15f, w, h, hold, d);
        switch (step)
        {
            case 1: // Golpe 1: Tajo Diagonal Descendente de Mandoble/Alabarda
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, 0.18f, -0.35f, -0.30f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.25f, 0.45f, -0.45f, -0.38f, -0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.20f, 0.48f, -0.35f, -0.30f, -0.20f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, 0.0f, 0.65f, 0.58f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.15f, 0.42f, -0.32f, -0.28f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.68f, 0.25f, 0.30f, 0.50f, w, h, hold, d);
                break;

            case 2: // Golpe 2: Estocada Profunda a 2 Manos con Zancada (Thrust)
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, 0.15f, -0.42f, -0.36f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.25f, -0.48f, 0.32f, 0.26f, -0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.20f, -0.35f, 0.05f, 0.0f, -0.20f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, -0.35f, 0.78f, 0.70f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.30f, -0.70f, 0.35f, 0.25f, -0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.25f, 0.75f, 0.68f, 0.50f, w, h, hold, d);
                AddStrikeCurve(c, "Right Hand Down-Up", 0f, 0.15f, -0.58f, -0.48f, 0f, w, h, hold, d);
                break;

            case 3: // Golpe 3: Corte Diagonal Ascendente (Uppercut Slash de Izquierda a Derecha)
                AddStrikeCurve(c, "Spine Front-Back", -0.20f, -0.32f, 0.12f, 0.08f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.35f, -0.48f, 0.45f, 0.38f, -0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.30f, -0.45f, 0.38f, 0.32f, -0.20f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.55f, 0.65f, -0.05f, 0.0f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.25f, -0.40f, 0.35f, 0.28f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.25f, 0.15f, 0.68f, 0.60f, 0.50f, w, h, hold, d);
                break;

            case 4: // Golpe 4: Estocada Alta al Pecho/Cabeza (High Thrust)
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, 0.12f, -0.38f, -0.32f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Chest Twist Left-Right", -0.25f, 0.35f, -0.48f, -0.42f, -0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.20f, -0.15f, 0.22f, 0.18f, -0.20f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, -0.30f, 0.78f, 0.70f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Right Forearm Stretch", -0.30f, -0.68f, 0.35f, 0.25f, -0.30f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.30f, 0.75f, 0.68f, 0.50f, w, h, hold, d);
                AddStrikeCurve(c, "Right Hand Down-Up", 0f, 0.15f, -0.58f, -0.48f, 0f, w, h, hold, d);
                break;

            case 5: // Golpe 5: Tajo Vertical Descendente Final a 2 Manos
                AddStrikeCurve(c, "Spine Front-Back", -0.10f, 0.35f, -0.58f, -0.52f, -0.10f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Down-Up", -0.20f, 0.62f, -0.52f, -0.46f, -0.20f, w, h, hold, d);
                AddStrikeCurve(c, "Right Arm Front-Back", 0.25f, 0.35f, 0.60f, 0.54f, 0.25f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Down-Up", -0.15f, 0.58f, -0.48f, -0.42f, -0.15f, w, h, hold, d);
                AddStrikeCurve(c, "Left Arm Front-Back", 0.50f, 0.55f, 0.62f, 0.56f, 0.50f, w, h, hold, d);
                break;
        }
    }
}
#endif
