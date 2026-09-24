using UnityEngine;

namespace ForjaGame.Combat
{
    public static partial class ArmIK
    {
        /// <summary>
        /// Aplica asistencia anatómica escalonada cuando el objetivo supera el 92% del alcance del brazo:
        /// 1. Clavícula / Hombro -> 2. Torso -> 3. Caderas -> 4. Solo al final acerca el arma.
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
            float assistThreshold = armLength * ReachAssistThreshold;

            Vector3 toTarget = targetHandPos - shoulderPos;
            float dist = toTarget.magnitude;

            if (dist <= assistThreshold) return;

            Vector3 dir = toTarget / dist;
            float excess = dist - assistThreshold;

            // 1. Asistencia de Clavícula / Hombro (hasta 0.05m de extensión)
            if (clavicle != null)
            {
                float clavicleShift = Mathf.Min(excess * 0.40f, 0.05f);
                clavicle.position += dir * clavicleShift;
                excess -= clavicleShift;
            }

            // 2. Asistencia de Torso (inclinación frontal sutil hacia el objetivo)
            if (excess > 0.001f && spine != null)
            {
                float spineAssist = Mathf.Min(excess * 0.35f, 0.04f);
                spine.position += dir * spineAssist;
                excess -= spineAssist;
            }

            // 3. Asistencia de Caderas
            if (excess > 0.001f && hips != null)
            {
                float hipsAssist = Mathf.Min(excess * 0.25f, 0.03f);
                hips.position += dir * hipsAssist;
                excess -= hipsAssist;
            }

            // 4. Solo al final, si aún supera el tope absoluto (96%), acercar el arma
            float finalDist = Vector3.Distance(shoulderPos, targetHandPos);
            if (finalDist > maxReach && weaponHolder != null)
            {
                Vector3 pullback = dir * (finalDist - maxReach);
                weaponHolder.position -= pullback;
                targetHandPos -= pullback;
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
        /// Verifica en armas a dos manos (2H) que ambos agarres sean alcanzables; si no, desplaza el arma hacia el pecho.
        /// </summary>
        public static void EnsureBimanualReachable(
            Vector3 rShoulder, Vector3 lShoulder,
            ref Vector3 gripRear, ref Vector3 gripFront,
            float rArmLen, float lArmLen,
            Transform weaponHolder)
        {
            float maxR = rArmLen * MaxReachCap;
            float maxL = lArmLen * MaxReachCap;

            float distR = Vector3.Distance(rShoulder, gripRear);
            float distL = Vector3.Distance(lShoulder, gripFront);

            float excessR = Mathf.Max(0f, distR - maxR);
            float excessL = Mathf.Max(0f, distL - maxL);

            if ((excessR > 0.001f || excessL > 0.001f) && weaponHolder != null)
            {
                Vector3 toShoulderMid = (((rShoulder + lShoulder) * 0.5f) - ((gripRear + gripFront) * 0.5f)).normalized;
                float maxExcess = Mathf.Max(excessR, excessL);
                Vector3 shift = toShoulderMid * maxExcess;

                weaponHolder.position += shift;
                gripRear += shift;
                gripFront += shift;
            }
        }
    }
}
