using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// PlayerCombatIKDriver - Submódulo de Cadena Cinética y Torsión Secuencial.
    /// Ejecuta la propagación de fuerza a través de 4 fases marciales:
    /// Caderas -> Columna/Torso -> Hombros/Pecho -> Brazos -> Arma.
    /// </summary>
    public partial class PlayerCombatIKDriver
    {
        private void CalculateTorsoKinematics(int comboIndex, float t,
            out float hipsYaw, out float hipsPitch, out float hipsRoll,
            out float spineYaw, out float spinePitch, out float spineRoll,
            out float chestYaw, out float chestPitch, out float chestRoll)
        {
            WeaponAnimationStyle style = _currentWeapon != null ? _currentWeapon.GetAnimationStyle() : WeaponAnimationStyle.Sword1H;
            AttackMotionType motionType = WeaponComboAnimator.GetMotionType(style, comboIndex);

            float weightNorm = _currentWeapon != null ? _currentWeapon.GetNormalizedWeight() : 0.5f;

            // Duraciones y retraso de desfase articular según inercia del arma
            float windupEnd = Mathf.Lerp(0.16f, 0.28f, weightNorm);
            float hitApex = Mathf.Lerp(0.40f, 0.52f, weightNorm);

            if (motionType == AttackMotionType.OverheadSlam)
            {
                windupEnd += 0.04f;
                hitApex += 0.03f;
            }
            else if (motionType == AttackMotionType.Thrust)
            {
                windupEnd -= 0.02f;
                hitApex -= 0.02f;
            }

            float boneLag = Mathf.Lerp(0.025f, 0.055f, weightNorm);
            float overshootRatio = Mathf.Lerp(0.06f, 0.18f, weightNorm);
            float recoveryStart = hitApex + Mathf.Lerp(0.06f, 0.12f, weightNorm);

            GetTorsoMotionTargets(motionType, comboIndex,
                out float wHY, out float wHP, out float wHR,
                out float wSY, out float wSP, out float wSR,
                out float wCY, out float wCP, out float wCR,
                out float sHY, out float sHP, out float sHR,
                out float sSY, out float sSP, out float sSR,
                out float sCY, out float sCP, out float sCR);

            if (t <= windupEnd)
            {
                // FASE 1: ANTICIPACIÓN (WIND-UP)
                float tau = Mathf.Clamp01(t / windupEnd);
                float twoLag = Mathf.Min(0.5f, 2f * boneLag);

                float uH = EaseInOutSine(Mathf.Clamp01(tau / (1f - twoLag)));
                float uS = EaseInOutSine(Mathf.Clamp01((tau - boneLag) / (1f - twoLag)));
                float uC = EaseInOutSine(Mathf.Clamp01((tau - twoLag) / (1f - twoLag)));

                hipsYaw = Mathf.Lerp(_strikeStartHipsYaw, wHY, uH);
                hipsPitch = Mathf.Lerp(_strikeStartHipsPitch, wHP, uH);
                hipsRoll = Mathf.Lerp(_strikeStartHipsRoll, wHR, uH);

                spineYaw = Mathf.Lerp(_strikeStartSpineYaw, wSY, uS);
                spinePitch = Mathf.Lerp(_strikeStartSpinePitch, wSP, uS);
                spineRoll = Mathf.Lerp(_strikeStartSpineRoll, wSR, uS);

                chestYaw = Mathf.Lerp(_strikeStartChestYaw, wCY, uC);
                chestPitch = Mathf.Lerp(_strikeStartChestPitch, wCP, uC);
                chestRoll = Mathf.Lerp(_strikeStartChestRoll, wCR, uC);
            }
            else if (t <= hitApex)
            {
                // FASE 2: ACELERACIÓN (DRIVE) -> FASE 3: IMPACTO (APEX)
                float tau = Mathf.Clamp01((t - windupEnd) / (hitApex - windupEnd));
                float twoLag = Mathf.Min(0.5f, 2f * boneLag);

                float uH = EaseInQuad(Mathf.Clamp01(tau / (1f - twoLag)));
                float uS = EaseInOutQuad(Mathf.Clamp01((tau - boneLag) / (1f - twoLag)));
                float uC = EaseOutQuad(Mathf.Clamp01((tau - twoLag) / (1f - twoLag)));

                hipsYaw = Mathf.Lerp(wHY, sHY, uH);
                hipsPitch = Mathf.Lerp(wHP, sHP, uH);
                hipsRoll = Mathf.Lerp(wHR, sHR, uH);

                spineYaw = Mathf.Lerp(wSY, sSY, uS);
                spinePitch = Mathf.Lerp(wSP, sSP, uS);
                spineRoll = Mathf.Lerp(wSR, sSR, uS);

                chestYaw = Mathf.Lerp(wCY, sCY, uC);
                chestPitch = Mathf.Lerp(wCP, sCP, uC);
                chestRoll = Mathf.Lerp(wCR, sCR, uC);
            }
            else if (t <= recoveryStart)
            {
                // FASE 4A: FOLLOW-THROUGH (OVERSHOOT POR INERCIA)
                float tau = Mathf.Clamp01((t - hitApex) / (recoveryStart - hitApex));
                float u = EaseOutQuad(tau);

                float oHY = sHY * (1f + overshootRatio);
                float oHP = sHP * (1f + overshootRatio * 0.5f);
                float oHR = sHR * (1f + overshootRatio * 0.5f);

                float oSY = sSY * (1f + overshootRatio);
                float oSP = sSP * (1f + overshootRatio * 0.5f);
                float oSR = sSR * (1f + overshootRatio * 0.5f);

                float oCY = sCY * (1f + overshootRatio);
                float oCP = sCP * (1f + overshootRatio * 0.5f);
                float oCR = sCR * (1f + overshootRatio * 0.5f);

                hipsYaw = Mathf.Lerp(sHY, oHY, u);
                hipsPitch = Mathf.Lerp(sHP, oHP, u);
                hipsRoll = Mathf.Lerp(sHR, oHR, u);

                spineYaw = Mathf.Lerp(sSY, oSY, u);
                spinePitch = Mathf.Lerp(sSP, oSP, u);
                spineRoll = Mathf.Lerp(sSR, oSR, u);

                chestYaw = Mathf.Lerp(sCY, oCY, u);
                chestPitch = Mathf.Lerp(sCP, oCP, u);
                chestRoll = Mathf.Lerp(sCR, oCR, u);
            }
            else
            {
                // FASE 4B: RECUPERACIÓN SUAVE HACIA COMBAT IDLE
                float tau = Mathf.Clamp01((t - recoveryStart) / (1f - recoveryStart));
                float u = EaseOutCubic(tau);

                float oHY = sHY * (1f + overshootRatio);
                float oHP = sHP * (1f + overshootRatio * 0.5f);
                float oHR = sHR * (1f + overshootRatio * 0.5f);

                float oSY = sSY * (1f + overshootRatio);
                float oSP = sSP * (1f + overshootRatio * 0.5f);
                float oSR = sSR * (1f + overshootRatio * 0.5f);

                float oCY = sCY * (1f + overshootRatio);
                float oCP = sCP * (1f + overshootRatio * 0.5f);
                float oCR = sCR * (1f + overshootRatio * 0.5f);

                hipsYaw = Mathf.Lerp(oHY, 0f, u);
                hipsPitch = Mathf.Lerp(oHP, 0f, u);
                hipsRoll = Mathf.Lerp(oHR, 0f, u);

                spineYaw = Mathf.Lerp(oSY, 0f, u);
                spinePitch = Mathf.Lerp(oSP, 0f, u);
                spineRoll = Mathf.Lerp(oSR, 0f, u);

                chestYaw = Mathf.Lerp(oCY, 0f, u);
                chestPitch = Mathf.Lerp(oCP, 0f, u);
                chestRoll = Mathf.Lerp(oCR, 0f, u);
            }
        }
    }
}
