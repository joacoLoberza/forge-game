using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Evaluador cinemático del movimiento marcial en ESPACIO DEL PECHO.
    /// Resuelve splines de waypoints, orientación con roll guiado por la velocidad y retorno a guardia por resorte.
    /// </summary>
    public static class MoveSolver
    {
        public static void EvaluateMove(
            AttackMove move,
            float normTime,
            float armLength,
            HeadType headType,
            out Vector3 gripPosChest,
            out Quaternion weaponRotChest,
            out TorsoAngles torsoPose,
            out Vector3 velocityChest)
        {
            if (move == null || move.waypoints == null || move.waypoints.Length == 0)
            {
                gripPosChest = new Vector3(0.25f, -0.15f, 0.40f) * (armLength / 0.65f);
                weaponRotChest = Quaternion.identity;
                torsoPose = TorsoAngles.Identity;
                velocityChest = Vector3.zero;
                return;
            }

            var wps = move.waypoints;
            int n = wps.Length;

            // 1. Localizar tramo del waypoint correspondiente a normTime
            int idx = 0;
            while (idx < n - 1 && wps[idx + 1].time <= normTime) idx++;

            int i0 = Mathf.Max(0, idx - 1);
            int i1 = idx;
            int i2 = Mathf.Min(n - 1, idx + 1);
            int i3 = Mathf.Min(n - 1, idx + 2);

            float t0 = wps[i1].time;
            float t1 = wps[i2].time;
            float segU = (t1 > t0) ? Mathf.Clamp01((normTime - t0) / (t1 - t0)) : 0f;

            // 2. Interpolación de posición con Catmull-Rom en espacio del pecho
            Vector3 p0 = wps[i0].gripPosChestNorm * armLength;
            Vector3 p1 = wps[i1].gripPosChestNorm * armLength;
            Vector3 p2 = wps[i2].gripPosChestNorm * armLength;
            Vector3 p3 = wps[i3].gripPosChestNorm * armLength;

            gripPosChest = CatmullRom(p0, p1, p2, p3, segU);

            // Derivada primera (velocidad en espacio de pecho) para guiar el Roll
            velocityChest = CatmullRomDerivative(p0, p1, p2, p3, segU);

            // 3. Orientación: Apuntar punta según waypoint
            Vector3 tipDir = Vector3.Slerp(wps[i1].tipDirChest, wps[i2].tipDirChest, segU).normalized;
            if (tipDir.sqrMagnitude < 0.001f) tipDir = Vector3.forward;

            // 4. Cálculo del ROLL guiado por el elemento que impacta
            Vector3 rollLeadVector = CalculateRollLeadVector(tipDir, velocityChest, headType, move.motionType);
            Quaternion baseRot = Quaternion.LookRotation(tipDir, rollLeadVector);

            weaponRotChest = EnsureQuaternionHemisphere(baseRot, Quaternion.LookRotation(wps[i1].tipDirChest, Vector3.up));

            // 5. Interpolación de postura de torso
            torsoPose = InterpolateTorso(wps[i1].torso, wps[i2].torso, segU);
        }

        private static Vector3 CalculateRollLeadVector(Vector3 forwardTip, Vector3 velocity, HeadType headType, AttackMotionType motion)
        {
            Vector3 vNorm = velocity.normalized;
            Vector3 lateralVelocity = Vector3.ProjectOnPlane(vNorm, forwardTip);

            if (lateralVelocity.sqrMagnitude < 0.01f)
            {
                return Vector3.up;
            }

            lateralVelocity.Normalize();

            // Filo en tajos: el filo apunta en la dirección de corte
            // Cara plana en martillos: la cara frontal impacta
            // Punta en estocadas: alineada al avance
            if (motion == AttackMotionType.Thrust)
            {
                return Vector3.up;
            }

            if (headType == HeadType.Blunt)
            {
                return lateralVelocity;
            }

            return Vector3.Cross(forwardTip, lateralVelocity);
        }

        private static Quaternion EnsureQuaternionHemisphere(Quaternion target, Quaternion reference)
        {
            if (Quaternion.Dot(target, reference) < 0f)
            {
                return new Quaternion(-target.x, -target.y, -target.z, -target.w);
            }
            return target;
        }

        private static TorsoAngles InterpolateTorso(TorsoAngles a, TorsoAngles b, float u)
        {
            return new TorsoAngles(
                Vector3.Lerp(a.hips, b.hips, u),
                Vector3.Lerp(a.spine, b.spine, u),
                Vector3.Lerp(a.chest, b.chest, u),
                Mathf.Lerp(a.lean, b.lean, u)
            );
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static Vector3 CatmullRomDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                (2f * (2f * p0 - 5f * p1 + 4f * p2 - p3)) * t +
                (3f * (-p0 + 3f * p1 - 3f * p2 + p3)) * t2
            );
        }

        /// <summary>
        /// Resorte críticamente amortiguado (Critically Damped Spring) para retorno suave hacia la pose de guardia.
        /// </summary>
        public static Vector3 SpringTowards(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime, float deltaTime)
        {
            return Vector3.SmoothDamp(current, target, ref velocity, Mathf.Max(0.001f, smoothTime), Mathf.Infinity, deltaTime);
        }
    }
}
