using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Configuración central de umbrales y constantes para el sistema de combate procedural.
    /// Expone todos los parámetros editables en tiempo de ejecución para balance y calibración.
    /// </summary>
    [CreateAssetMenu(fileName = "ProceduralCombatConfig", menuName = "Forja/Combat/Procedural Combat Config")]
    public class ProceduralCombatConfigSO : ScriptableObject
    {
        [Header("Clasificación de Agarre (GripMode)")]
        [Tooltip("Longitud mínima normalizada del mango en metros")]
        public float minHandleLength = 0.15f;
        [Tooltip("Longitud máxima normalizada del mango en metros")]
        public float maxHandleLength = 1.85f;
        [Tooltip("Umbral h < 0.25: agarre de 1 mano corta")]
        public float gripOneShortThreshold = 0.25f;
        [Tooltip("Umbral h < 0.55: agarre de 1 mano / mano y media")]
        public float gripOneHandThreshold = 0.55f;
        [Tooltip("Umbral h < 0.85: agarre a dos manos")]
        public float gripTwoHandThreshold = 0.85f;
        [Tooltip("Masa (kg) a partir de la cual se fuerza agarre a dos manos independientemente del mango")]
        public float forceTwoHandMassThreshold = 3.2f;

        [Header("Clasificación de Familias")]
        [Tooltip("Ratio largo/ancho de cabeza mínimo para clasificar como Blade")]
        public float bladeLengthToWidthRatio = 2.8f;
        [Tooltip("Filo mínimo (0..1) para clasificar como Blade")]
        public float bladeSharpnessThreshold = 0.5f;
        [Tooltip("Extensión lateral mínima respecto al eje del mango para clasificar como Axe")]
        public float axeLateralExtentThreshold = 0.25f;
        [Tooltip("Filo mínimo para clasificar como Axe")]
        public float axeSharpnessThreshold = 0.45f;
        [Tooltip("Ratio handleLength/headLength (> 1.45) para designar variante de asta")]
        public float polearmRatioThreshold = 1.45f;

        [Header("Límites Físicos Razonables de Forja")]
        public float maxReasonableLength = 2.4f;
        public float maxReasonableMass = 12.0f;

        [Header("Poses de Guardia Procedurales por Agarre (Espacio Chest)")]
        [Tooltip("Posición y rotación de guardia para armas de una mano corta (Daga)")]
        public Vector3 guardPosOneShort = new Vector3(0.22f, -0.05f, 0.48f);
        public Vector3 guardRotOneShort = new Vector3(-12f, 20f, -10f);

        [Tooltip("Posición y rotación de guardia para armas de una mano (Espada, Hacha, Maza)")]
        public Vector3 guardPosOneHand = new Vector3(0.22f, -0.04f, 0.52f);
        public Vector3 guardRotOneHand = new Vector3(-20f, 20f, -12f);

        [Tooltip("Posición y rotación de guardia para armas de dos manos (Mandoble, Gran Mazo)")]
        public Vector3 guardPosTwoHand = new Vector3(0.14f, -0.10f, 0.48f);
        public Vector3 guardRotTwoHand = new Vector3(-24f, 15f, -8f);

        [Tooltip("Posición y rotación de guardia para armas de asta (Guja, Alabarda)")]
        public Vector3 guardPosPole = new Vector3(0.18f, -0.14f, 0.46f);
        public Vector3 guardRotPole = new Vector3(-32f, 18f, 12f);

        [Header("Calibración Anatómica de Manos para IK (Palma a Hueso Hand)")]
        [Tooltip("Offset posición de hueso RightHand al centro del puño cerrado")]
        public Vector3 rightHandOffsetPos = new Vector3(0.035f, 0.075f, -0.015f);
        [Tooltip("Offset rotación de hueso RightHand al eje longitudinal del arma")]
        public Vector3 rightHandOffsetRot = new Vector3(-90f, 0f, 90f);

        [Tooltip("Offset posición de hueso LeftHand al centro del puño cerrado (simetría)")]
        public Vector3 leftHandOffsetPos = new Vector3(-0.035f, 0.075f, -0.015f);
        [Tooltip("Offset rotación de hueso LeftHand al eje longitudinal del arma (simetría)")]
        public Vector3 leftHandOffsetRot = new Vector3(-90f, 0f, -90f);

        [Header("Offsets de Codos (IK Hints)")]
        public Vector3 rightElbowHintOffset = new Vector3(0.22f, -0.26f, -0.10f);
        public Vector3 leftElbowHintOffset = new Vector3(-0.22f, -0.26f, -0.10f);

        [Header("Límites Anatómicos")]
        [Tooltip("Fracción mínima de la longitud del brazo (distancia hombro->mano)")]
        public float armReachMinRatio = 0.25f;
        [Tooltip("Fracción máxima de la longitud del brazo para prevenir hiperextensión o pop")]
        public float armReachMaxRatio = 0.95f;
        [Tooltip("Límite de ángulo de elevación de hombro en espacio de pecho")]
        public float shoulderElevationMaxAngle = 85f;
        [Tooltip("Límite máximo de torsión de muñeca respecto al antebrazo")]
        public float wristTwistMaxAngle = 80f;
        [Tooltip("Límite de giro de cadera antes de reorientar el cuerpo completo")]
        public float hipYawMaxAngle = 35f;

        [Header("Volúmenes de Exclusión Corporal")]
        public float torsoRadius = 0.24f;
        public float torsoHeight = 0.65f;
        public float headRadius = 0.16f;
        public float legsRadius = 0.22f;
        public float legsHeight = 0.85f;
        [Tooltip("Margen de seguridad adicional al repeler el arma")]
        public float exclusionMargin = 0.04f;

        [Header("Cadena Cinética")]
        [Tooltip("Retraso secuencial entre eslabones (40-80 ms)")]
        public float kineticChainLagSeconds = 0.055f;
        public float hipContributionRatio = 0.25f;
        public float spineContributionRatio = 0.35f;
        public float chestContributionRatio = 0.40f;

        [Header("Física de Inercia y Daño")]
        [Tooltip("Inercia de referencia para normalizar factores k = sqrt(I / Iref)")]
        public float referenceInertia = 0.85f;
        public float windupBaseFactor = 0.25f;
        public float windupClampMin = 0.12f;
        public float windupClampMax = 0.90f;
        public float strikeBaseFactor = 0.18f;
        public float strikeClampMin = 0.08f;
        public float strikeClampMax = 0.60f;
        public float recoveryBaseFactor = 0.30f;
        public float recoveryClampMin = 0.15f;
        public float recoveryClampMax = 1.00f;

        [Header("Game Feel")]
        public float hitStopBaseDuration = 0.065f;
        public float hitStopMaxDuration = 0.12f;
        public float screenShakeBaseIntensity = 0.20f;
    }
}
