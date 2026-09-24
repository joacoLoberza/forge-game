using UnityEngine;

namespace ForjaGame.Combat
{
    public static partial class ArmIK
    {
        /// <summary>
        /// Aplica contención anatómica al alcance del brazo:
        /// Limita el objetivo de la mano al alcance físico máximo seguro (96%) sin deformar huesos.
        /// </summary>
        public static void ApplyReachAssistance(
            Transform clavicle,
            Transform spine,
            Transform hips,
            ref Vector3 targetHandPos,
            Vector3 shoulderPos,
            float armLength,
            Transform weaponHolder)
        {
            float maxReach = armLength * MaxReachCap;
            Vector3 toTarget = targetHandPos - shoulderPos;
            float dist = toTarget.magnitude;

            if (dist > maxReach)
            {
                targetHandPos = shoulderPos + toTarget.normalized * maxReach;
            }
        }

        /// <summary>
        /// Calcula la posición del Pole Target del codo con protección de cápsula de torso para evitar penetración.
        /// </summary>
        public static Vector3 CalculateElbowPoleWithTorsoCapsule(
            Transform chestTransform,
            Vector3 shoulderPos,
            Vector3 targetHandPos,
            bool isRightArm,
            float capsuleRadius = 0.22f)
        {
            if (chestTransform == null) return shoulderPos + Vector3.right * (isRightArm ? 0.4f : -0.4f);

            Vector3 chestCenter = chestTransform.position;
            Vector3 chestRight = chestTransform.right;
            Vector3 chestDown = -chestTransform.up;
            Vector3 chestFwd = chestTransform.forward;

            Vector3 toHand = (targetHandPos - shoulderPos).normalized;
            float sideSign = isRightArm ? 1f : -1f;

            // Si el brazo cruza el cuerpo (cross-body), desviar el codo hacia abajo y adelante
            float crossBody = Mathf.Clamp01(-Vector3.Dot(toHand, chestRight * sideSign));
            Vector3 poleDir = Vector3.Lerp(
                chestRight * (0.40f * sideSign) + chestDown * 0.25f - chestFwd * 0.10f,
                chestDown * 0.45f + chestFwd * 0.18f + chestRight * (0.12f * sideSign),
                crossBody);

            Vector3 polePos = shoulderPos + poleDir;

            // Verificación contra la cápsula del torso: si el pole penetra, empujarlo hacia afuera
            Vector3 fromTorso = polePos - chestCenter;
            fromTorso.y = 0f; // Plano horizontal
            if (fromTorso.magnitude < capsuleRadius)
            {
                Vector3 pushDir = fromTorso.sqrMagnitude > 0.001f ? fromTorso.normalized : (chestRight * sideSign);
                polePos = chestCenter + pushDir * (capsuleRadius + 0.05f) + Vector3.up * (polePos.y - chestCenter.y);
            }

            return polePos;
        }

        /// <summary>
        /// Verifica en armas a dos manos (2H) que el agarre secundario esté al alcance del brazo izquierdo.
        /// Si está demasiado lejos, relaja el peso del IK del brazo izquierdo en lugar de tirar del arma o quebrar articulaciones.
        /// </summary>
        public static void EnsureBimanualReachable(
            Vector3 rShoulder, Vector3 lShoulder,
            ref Vector3 gripRear, ref Vector3 gripFront,
            float rArmLen, float lArmLen,
            ref float leftArmWeight)
        {
            float maxL = lArmLen * MaxReachCap;
            float distL = Vector3.Distance(lShoulder, gripFront);

            if (distL > maxL)
            {
                float excess = distL - maxL;
                leftArmWeight = Mathf.Clamp01(1f - (excess / 0.15f));
            }
        }
    }
}
