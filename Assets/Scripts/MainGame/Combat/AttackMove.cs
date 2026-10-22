using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public enum AttackMotionType
    {
        HorizontalSlash,
        Thrust,
        OverheadSlam
    }

    [Serializable]
    public struct TorsoAngles
    {
        public Vector3 hips;
        public Vector3 spine;
        public Vector3 chest;
        public float lean;

        public TorsoAngles(Vector3 h, Vector3 s, Vector3 c, float l = 0f)
        {
            hips = h;
            spine = s;
            chest = c;
            lean = l;
        }

        public static TorsoAngles Identity => new TorsoAngles(Vector3.zero, Vector3.zero, Vector3.zero, 0f);
    }

    [Serializable]
    public struct AttackWaypoint
    {
        [Tooltip("Posición del agarre en espacio del pecho, normalizada por el largo del brazo (0..1)")]
        public Vector3 gripPosChestNorm;

        [Tooltip("Vector unitario de orientación de la punta del arma en espacio del pecho")]
        public Vector3 tipDirChest;

        [Tooltip("Ángulos articulares del torso en este waypoint")]
        public TorsoAngles torso;

        [Range(0f, 1f)]
        public float time;

        public AttackWaypoint(Vector3 gripNorm, Vector3 tipDir, TorsoAngles torsoPose, float t)
        {
            gripPosChestNorm = gripNorm;
            tipDirChest = tipDir.sqrMagnitude > 0.001f ? tipDir.normalized : Vector3.forward;
            torso = torsoPose;
            time = Mathf.Clamp01(t);
        }
    }

    /// <summary>
    /// ScriptableObject que define un movimiento marcial o golpe de combo.
    /// Define la trayectoria y orientación del arma en ESPACIO DEL PECHO, normalizada por el largo del brazo.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAttackMove", menuName = "ForjaGame/Combat/Attack Move")]
    public class AttackMove : ScriptableObject
    {
        [Header("Identificación y Capacidades")]
        public string moveName = "Tajo Básico";
        public WeaponArchetype archetype = WeaponArchetype.Sword1H;
        public AttackMotionType motionType = AttackMotionType.HorizontalSlash;
        public WeaponCapabilities requiredCapability = WeaponCapabilities.CanSlash;
        public AttackMove fallbackMove;

        [Header("Fases Marciales (Tiempos Normalizados 0..1)")]
        [Range(0f, 1f)] public float anticipationEnd = 0.22f;
        [Range(0f, 1f)] public float hitApex = 0.46f;
        [Range(0f, 1f)] public float hitWindowStart = 0.40f;
        [Range(0f, 1f)] public float hitWindowEnd = 0.54f;
        [Range(0f, 1f)] public float recoveryStart = 0.60f;

        [Header("Dinámica y Tactilidad")]
        [Tooltip("Duración base del golpe en segundos")]
        public float baseDuration = 0.65f;
        [Tooltip("Pausa de fotogramas al conectar (Hit-Stop en segundos: 40-70ms)")]
        [Range(0.02f, 0.10f)] public float hitStopDuration = 0.055f;
        [Tooltip("Multiplicador de daño del golpe (1.65x en Finisher)")]
        public float damageMultiplier = 1.0f;
        [Tooltip("Avance o zancada hacia adelante durante el golpe")]
        public float lungeDistance = 0.35f;

        [Header("Waypoints en Espacio del Pecho")]
        public AttackWaypoint[] waypoints = new AttackWaypoint[0];

        public bool IsFinisher => damageMultiplier >= 1.5f;

        public AttackWaypoint GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Length == 0)
                return new AttackWaypoint(new Vector3(0.3f, -0.2f, 0.4f), Vector3.forward, TorsoAngles.Identity, 0f);
            return waypoints[Mathf.Clamp(index, 0, waypoints.Length - 1)];
        }
    }
}
