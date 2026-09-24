using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public enum EdgeMode
    {
        LeadVelocity,
        Fixed,
        FaceForward
    }

    public enum AttackPhase
    {
        Anticipation,
        Strike,
        HitWindow,
        FollowThrough,
        Recovery
    }

    /// <summary>
    /// Keyframe que define el objetivo de la mano y la orientación del arma desde el hombro
    /// en coordenadas esféricas normalizadas (R3: siempre alcanzable).
    /// </summary>
    [Serializable]
    public struct AttackKeyframe
    {
        [Range(0f, 1f), Tooltip("Tiempo normalizado dentro del golpe (0..1)")]
        public float time;

        [Tooltip("Azimut en grados desde el hombro en BodySpace (0 = adelante, + = derecha, - = izquierda)")]
        public float azimuth;

        [Tooltip("Elevación en grados desde el hombro en BodySpace (0 = horizontal, + = arriba, - = abajo)")]
        public float elevation;

        [Range(0f, 1f), Tooltip("Alcance normalizado respecto al largo del brazo (0..1, máx seguro 0.92)")]
        public float reach;

        [Tooltip("Inclinación vertical de la punta del arma (+ = arriba, - = abajo) en BodySpace")]
        public float tipPitch;

        [Tooltip("Giro horizontal de la punta del arma (+ = derecha, - = izquierda) en BodySpace")]
        public float tipYaw;

        [Tooltip("Modo de orientación del filo del arma")]
        public EdgeMode edgeMode;

        [Tooltip("Ángulo de roll fijo en grados si edgeMode == Fixed")]
        public float fixedRoll;

        public AttackKeyframe(
            float t,
            float az,
            float el,
            float r,
            float pitch,
            float yaw,
            EdgeMode mode = EdgeMode.LeadVelocity,
            float roll = 0f)
        {
            time = Mathf.Clamp01(t);
            azimuth = az;
            elevation = el;
            reach = Mathf.Clamp01(r);
            tipPitch = pitch;
            tipYaw = yaw;
            edgeMode = mode;
            fixedRoll = roll;
        }
    }
}
