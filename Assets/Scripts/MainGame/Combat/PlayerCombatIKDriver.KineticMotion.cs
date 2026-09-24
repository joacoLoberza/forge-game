using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// PlayerCombatIKDriver - Submódulo de Perfiles de Movimiento y Easing para Cadena Cinética.
    /// Define los ángulos objetivo (Yaw, Pitch, Roll) para caderas, columna y pecho según el tipo de ataque.
    /// </summary>
    public partial class PlayerCombatIKDriver
    {
        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

        /// <summary>
        /// Modula la intensidad de torsión de cadera y pecho según el tipo, peso y longitud del arma equipada.
        /// </summary>
        private float GetTorsoEffortMultiplier()
        {
            if (!_isEquipped || _currentWeapon == null) return 1.0f;

            var style = _currentWeapon.GetAnimationStyle();
            float baseMultiplier = style switch
            {
                WeaponAnimationStyle.Dagger1H => 0.42f,
                WeaponAnimationStyle.Sword1H => 0.85f,
                WeaponAnimationStyle.Axe1H => 0.95f,
                WeaponAnimationStyle.Hammer1H => 1.15f,
                WeaponAnimationStyle.Polearm => 1.15f,
                WeaponAnimationStyle.Heavy2H => 1.35f,
                _ => 1.0f
            };

            float normWeight = _currentWeapon.GetNormalizedWeight();
            float weightTweak = Mathf.Lerp(-0.08f, 0.08f, normWeight);

            float lengthRatio = Mathf.Clamp(_currentWeapon.attackRange / 2.2f, 0.85f, 1.25f);
            float lengthTweak = (lengthRatio - 1.0f) * 0.12f;

            return Mathf.Clamp(baseMultiplier + weightTweak + lengthTweak, 0.38f, 1.45f);
        }

        /// <summary>
        /// Obtiene los ángulos objetivo de windup (w) y strike (s) para cada grupo articular según el patrón marcial.
        /// </summary>
        private void GetTorsoMotionTargets(AttackMotionType motionType, int comboIndex,
            out float wHY, out float wHP, out float wHR,
            out float wSY, out float wSP, out float wSR,
            out float wCY, out float wCP, out float wCR,
            out float sHY, out float sHP, out float sHR,
            out float sSY, out float sSP, out float sSR,
            out float sCY, out float sCP, out float sCR)
        {
            int step = Mathf.Clamp(comboIndex, 0, 4);

            switch (motionType)
            {
                case AttackMotionType.Thrust:
                    // Estocada: Carga retrasada en guardia -> Gran zancada, proyección e inclinación frontal
                    wHY = 8f;   wHP = -2f; wHR = 2f;
                    wSY = 12f;  wSP = -3f; wSR = 3f;
                    wCY = 16f;  wCP = -4f; wCR = 4f;

                    sHY = -10f; sHP = 6f;  sHR = -4f;
                    sSY = -14f; sSP = 10f; sSR = -8f;
                    sCY = -18f; sCP = 15f; sCR = -12f;
                    break;

                case AttackMotionType.OverheadSlam:
                    // Aplastamiento vertical: Arqueo dorsal amplio -> Flexión explosiva descendente
                    wHY = 0f; wHP = -4f;  wHR = 0f;
                    wSY = 0f; wSP = -10f; wSR = 0f;
                    wCY = 0f; wCP = -18f; wCR = 0f;

                    sHY = 0f; sHP = 8f;   sHR = 0f;
                    sSY = 0f; sSP = 16f;  sSR = 0f;
                    sCY = 0f; sCP = 26f;  sCR = 0f;
                    break;

                default: // HorizontalSlash
                    // Tajo horizontal: Torsión en sentido contrario -> Descarga rotacional de tronco
                    bool isRightToLeft = (step % 2 == 0);
                    if (isRightToLeft)
                    {
                        wHY = 12f;  wHP = 0f;  wHR = 2f;
                        wSY = 16f;  wSP = -2f; wSR = 3f;
                        wCY = 22f;  wCP = -3f; wCR = 4f;

                        sHY = -14f; sHP = 6f;  sHR = -2f;
                        sSY = -20f; sSP = 12f; sSR = -3f;
                        sCY = -26f; sCP = 20f; sCR = -4f;
                    }
                    else
                    {
                        wHY = -12f; wHP = 0f;  wHR = -2f;
                        wSY = -16f; wSP = 0f;  wSR = -3f;
                        wCY = -20f; wCP = 0f;  wCR = -4f;

                        sHY = 14f;  sHP = 5f;  sHR = 2f;
                        sSY = 18f;  sSP = 10f; sSR = 3f;
                        sCY = 24f;  sCP = 18f; sCR = 4f;
                    }
                    break;
            }

            // Modular intensidad anatómica
            float effort = GetTorsoEffortMultiplier();
            wHY *= effort; wHP *= effort; wHR *= effort;
            wSY *= effort; wSP *= effort; wSR *= effort;
            wCY *= effort; wCP *= effort; wCR *= effort;

            sHY *= effort; sHP *= effort; sHR *= effort;
            sSY *= effort; sSP *= effort; sSR *= effort;
            sCY *= effort; sCP *= effort; sCR *= effort;

            sCY = Mathf.Clamp(sCY, -32f, 32f);
            sCP = Mathf.Clamp(sCP, -12f, 30f);
            sCR = Mathf.Clamp(sCR, -16f, 16f);
        }
    }
}
