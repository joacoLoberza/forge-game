using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Fase individual de un ataque procedural (Guard, Windup, Strike, Recovery).
    /// Las poses se definen en espacio local del Pecho (Chest), permitiendo que cualquier arma
    /// escale su arco automáticamente sin importar su longitud.
    /// </summary>
    [Serializable]
    public struct AttackPhase
    {
        [Tooltip("Pose de la empuñadura relativa al Chest (posición y orientación local)")]
        public Pose handPose;

        [Range(-35f, 35f)]
        [Tooltip("Giro de cadera en grados (rotación de la pelvis en el plano horizontal)")]
        public float hipYaw;

        [Tooltip("Giro de pecho en grados (Yaw, Pitch, Roll)")]
        public float chestYaw;
        public float chestPitch;
        public float chestRoll;

        [Tooltip("Desplazamiento del centro de masa / peso corporal (adelante/atrás en Z, lados en X)")]
        public Vector3 weightShift;

        [Tooltip("Paso o impulso hacia adelante en metros")]
        public float lunge;

        [Tooltip("Duración base de esta fase en segundos (se escala luego por la inercia)")]
        public float duration;

        [Tooltip("Curva de interpolación y aceleración del movimiento")]
        public AnimationCurve ease;

        [Tooltip("Indica si esta fase posee una pose intermedia opcional para arquear la trayectoria")]
        public bool hasIntermediatePose;
        public Pose intermediateHandPose;

        public static AttackPhase CreateDefault(Pose pose, float duration, AnimationCurve easeCurve = null)
        {
            return new AttackPhase
            {
                handPose = pose,
                hipYaw = 0f,
                chestYaw = 0f,
                chestPitch = 0f,
                chestRoll = 0f,
                weightShift = Vector3.zero,
                lunge = 0f,
                duration = duration,
                ease = easeCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                hasIntermediatePose = false,
                intermediateHandPose = Pose.identity
            };
        }
    }

    /// <summary>
    /// Definición ScriptableObject para un ataque procedural de combate con 4 fases:
    /// Guard, Windup (anticipación), Strike (impacto) y Recovery (recuperación/inercia).
    /// </summary>
    [CreateAssetMenu(fileName = "NewAttackDefinition", menuName = "Forja/Combat/Attack Definition")]
    public class AttackDefinitionSO : ScriptableObject
    {
        [Header("Identificación")]
        public string attackName = "Ataque Base";
        public GripMode gripMode = GripMode.OneHand;
        public WeaponFamily preferredFamily = WeaponFamily.Blade;

        [Header("Fases del Ataque")]
        public AttackPhase guardPhase;
        public AttackPhase windupPhase;
        public AttackPhase strikePhase;
        public AttackPhase recoveryPhase;

        [Header("Propiedades Cinéticas")]
        [Tooltip("Multiplicador del arco del barrido según la inercia")]
        public float arcMultiplier = 1.0f;
        [Tooltip("Ángulo estimado de apertura del golpe en grados")]
        public float swingArcAngle = 75f;
    }
}
