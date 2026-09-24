using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Utilidades matemáticas para coordenadas esféricas normalizadas desde el hombro.
    /// Convención del sistema:
    /// Azimut: 0 = adelante (+Z), + = derecha (+X), - = izquierda (-X).
    /// Elevación: 0 = plano horizontal, + = arriba (+Y), - = abajo (-Y).
    /// Alcance: factor 0..1 del largo del brazo.
    /// </summary>
    public static class SphericalCoords
    {
        public const float SafeReachLimit = 0.92f;
        public const float HardReachCap = 0.96f;

        /// <summary>
        /// Convierte azimut y elevación en grados a un vector unitario de dirección en BodySpace.
        /// </summary>
        public static Vector3 SphericalToVector(float azimuthDeg, float elevationDeg)
        {
            float azRad = azimuthDeg * Mathf.Deg2Rad;
            float elRad = elevationDeg * Mathf.Deg2Rad;
            float cosEl = Mathf.Cos(elRad);
            float sinEl = Mathf.Sin(elRad);
            float sinAz = Mathf.Sin(azRad);
            float cosAz = Mathf.Cos(azRad);

            return new Vector3(cosEl * sinAz, sinEl, cosEl * cosAz).normalized;
        }

        /// <summary>
        /// Convierte un vector de dirección a azimut y elevación en grados.
        /// </summary>
        public static void VectorToSpherical(Vector3 dir, out float azimuthDeg, out float elevationDeg)
        {
            if (dir.sqrMagnitude < 1e-5f)
            {
                azimuthDeg = 0f;
                elevationDeg = 0f;
                return;
            }

            Vector3 norm = dir.normalized;
            elevationDeg = Mathf.Asin(Mathf.Clamp(norm.y, -1f, 1f)) * Mathf.Rad2Deg;
            azimuthDeg = Mathf.Atan2(norm.x, norm.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Calcula la posición mundial de un objetivo de mano en coordenadas esféricas relativas al hombro.
        /// </summary>
        public static Vector3 CalculateHandTargetWorld(
            Vector3 shoulderWorldPos,
            Quaternion bodySpaceRotation,
            float azimuthDeg,
            float elevationDeg,
            float reachNorm,
            float armLength)
        {
            float clampedReach = Mathf.Clamp(reachNorm, 0f, HardReachCap);
            Vector3 localDir = SphericalToVector(azimuthDeg, elevationDeg);
            Vector3 worldOffset = bodySpaceRotation * (localDir * (clampedReach * armLength));
            return shoulderWorldPos + worldOffset;
        }

        /// <summary>
        /// Calcula el vector de dirección de la punta (TipDir) en BodySpace a partir de pitch y yaw.
        /// </summary>
        public static Vector3 CalculateTipDirection(float pitchDeg, float yawDeg)
        {
            return SphericalToVector(yawDeg, pitchDeg);
        }
    }
}
