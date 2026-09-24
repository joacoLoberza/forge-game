using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Solucionador de Cinemática Inversa (IK) Analítico de Dos Huesos (Two-Bone IK) para Brazos.
    /// Garantiza límite estricto de alcance al 96%, límites de muñeca y reparto de pronación.
    /// </summary>
    public static partial class ArmIK
    {
        public const float MaxReachCap = 0.96f;
        public const float ReachAssistThreshold = 0.92f;

        public const float MaxWristFlexion = 60f;
        public const float MaxWristRadial = 20f;
        public const float MaxWristUlnar = 30f;
        public const float ForearmPronationShare = 0.65f; // 65% en antebrazo, 35% en muñeca

        /// <summary>
        /// Resuelve Two-Bone IK analítico preservando rigurosamente las longitudes óseas originales.
        /// </summary>
        public static void SolveTwoBone(
            Transform root, Transform mid, Transform end,
            Vector3 targetPos, Vector3 polePos, float weight = 1.0f)
        {
            if (weight <= 0.001f || root == null || mid == null || end == null) return;

            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = end.position;

            float lenAB = Vector3.Distance(a, b);
            float lenBC = Vector3.Distance(b, c);
            if (lenAB < 0.01f || lenBC < 0.01f) return;

            float maxDist = (lenAB + lenBC) * MaxReachCap;
            float minDist = Mathf.Abs(lenAB - lenBC) * 1.001f;

            Vector3 toTarget = targetPos - a;
            float dist = toTarget.magnitude;
            if (dist < 0.001f) return;

            float clampedDist = Mathf.Clamp(dist, minDist, maxDist);
            Vector3 dir = toTarget / dist;

            // Ley del Coseno para ángulo alfa en la raíz (A)
            float cosAlpha = Mathf.Clamp(
                (lenAB * lenAB + clampedDist * clampedDist - lenBC * lenBC) / (2f * lenAB * clampedDist),
                -1f, 1f);
            float sinAlpha = Mathf.Sqrt(Mathf.Max(0f, 1f - cosAlpha * cosAlpha));

            Vector3 poleDir = (polePos - a).normalized;
            Vector3 planeNormal = Vector3.Cross(dir, poleDir);
            if (planeNormal.sqrMagnitude < 0.0001f) planeNormal = Vector3.up;
            planeNormal.Normalize();

            Vector3 tangent = Vector3.Cross(planeNormal, dir).normalized;
            Vector3 bDesired = a + dir * (lenAB * cosAlpha) + tangent * (lenAB * sinAlpha);

            Quaternion origRootRot = root.rotation;
            Quaternion origMidRot = mid.rotation;

            // Rotar hombro para ubicar codo en bDesired
            Vector3 currentAB = b - a;
            Vector3 desiredAB = bDesired - a;
            if (currentAB.sqrMagnitude > 0.0001f && desiredAB.sqrMagnitude > 0.0001f)
            {
                Quaternion rotRoot = Quaternion.FromToRotation(currentAB, desiredAB);
                root.rotation = rotRoot * root.rotation;
            }

            // Rotar codo para ubicar muñeca en targetPos
            b = mid.position;
            c = end.position;
            Vector3 currentBC = c - b;
            Vector3 desiredBC = (a + dir * clampedDist) - b;
            if (currentBC.sqrMagnitude > 0.0001f && desiredBC.sqrMagnitude > 0.0001f)
            {
                Quaternion rotMid = Quaternion.FromToRotation(currentBC, desiredBC);
                mid.rotation = rotMid * mid.rotation;
            }

            if (weight < 0.999f)
            {
                root.rotation = Quaternion.Slerp(origRootRot, root.rotation, weight);
                mid.rotation = Quaternion.Slerp(origMidRot, mid.rotation, weight);
            }
        }

        /// <summary>
        /// Aplica límites articulares anatómicos a la muñeca y reparte la pronación axial con el antebrazo.
        /// </summary>
        public static void ApplyWristAndPronationLimits(
            Transform forearm, Transform hand, Quaternion targetHandRot, float weight = 1.0f)
        {
            if (forearm == null || hand == null || weight <= 0.001f) return;

            Quaternion relRot = Quaternion.Inverse(forearm.rotation) * targetHandRot;
            Vector3 euler = relRot.eulerAngles;

            float pitch = NormalizeAngle(euler.x); // Flexión / Extensión (<= 60°)
            float yaw = NormalizeAngle(euler.y);   // Pronación / Supinación axial
            float roll = NormalizeAngle(euler.z);  // Desviación Radial (20°) / Cubital (30°)

            pitch = Mathf.Clamp(pitch, -MaxWristFlexion, MaxWristFlexion);
            roll = Mathf.Clamp(roll, -MaxWristUlnar, MaxWristRadial);

            // Reparto de pronación: 60-70% en el antebrazo, el resto en la muñeca
            float forearmYaw = yaw * ForearmPronationShare;
            float wristYaw = yaw * (1f - ForearmPronationShare);

            forearm.rotation *= Quaternion.AngleAxis(forearmYaw * weight, forearm.forward);

            Quaternion clampedRel = Quaternion.Euler(pitch, wristYaw, roll);
            Quaternion finalHandRot = forearm.rotation * clampedRel;

            if (Quaternion.Dot(hand.rotation, finalHandRot) < 0f)
            {
                finalHandRot = new Quaternion(-finalHandRot.x, -finalHandRot.y, -finalHandRot.z, -finalHandRot.w);
            }
            hand.rotation = Quaternion.Slerp(hand.rotation, finalHandRot, weight);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
