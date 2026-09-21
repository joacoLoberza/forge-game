using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public enum WeaponFamily
    {
        Blade,
        Axe,
        Blunt
    }

    public enum GripMode
    {
        OneShort,
        OneHand,
        TwoHand,
        Pole
    }

    /// <summary>
    /// Perfil físico del arma forjada.
    /// Contiene las propiedades cinemáticas, inerciales y de forma deducidas directamente
    /// a partir de las mallas y dimensiones del lingote y mango.
    /// </summary>
    [Serializable]
    public struct WeaponProfile
    {
        public WeaponFamily family;    // Blade, Axe, Blunt
        public GripMode grip;          // OneShort, OneHand, TwoHand, Pole
        public float mass;             // kg totales
        public float length;           // Longitud total desde el pomo a la punta (m)
        public float handleLength;     // Longitud del mango (m)
        public float headLength;       // Largo de la cabeza metálica (m)
        public float headWidth;        // Ancho de la cabeza metálica (m)
        public float headThickness;    // Espesor de la cabeza metálica (m)
        public float headSharpness;    // 0..1 (filo vs romo)
        public float lateralExtent;    // Cuánto sobresale la cabeza hacia un lado respecto al eje del mango
        public float gripToCom;        // Distancia mano trasera -> centro de masa (m)
        public float inertia;          // I ≈ Σ m_i · d_i² respecto a la mano trasera (kg·m²)
        public float headRadius;       // Radio volumétrico para barrido de impacto (m)
        public string generatedName;   // Nombre deducido para UI (Daga, Mandoble, Alabarda, etc.)

        /// <summary>
        /// Deduce y calcula un WeaponProfile completo a partir de parámetros geométricos y masa.
        /// </summary>
        public static WeaponProfile Calculate(
            float handleLen,
            float hLength,
            float hWidth,
            float hThickness,
            float sharpness,
            float latExtent,
            float totalMass,
            ProceduralCombatConfigSO config)
        {
            if (config == null) config = ScriptableObject.CreateInstance<ProceduralCombatConfigSO>();

            float safeHandleLen = Mathf.Clamp(handleLen, config.minHandleLength, config.maxReasonableLength);
            float safeHeadLen = Mathf.Max(0.08f, hLength);
            float safeHeadWidth = Mathf.Max(0.02f, hWidth);
            float safeHeadThickness = Mathf.Max(0.008f, hThickness);
            float safeSharpness = Mathf.Clamp01(sharpness);
            float safeMass = Mathf.Clamp(totalMass, 0.35f, config.maxReasonableMass);
            float totalLen = Mathf.Clamp(safeHandleLen + safeHeadLen, 0.20f, config.maxReasonableLength);

            // 1. Distribución aproximada de masa: 20-30% mango, 70-80% cabeza metálica
            float handleMassFraction = Mathf.Clamp(safeHandleLen / totalLen * 0.40f, 0.15f, 0.45f);
            float handleMass = safeMass * handleMassFraction;
            float headMass = safeMass - handleMass;

            // 2. Centros de masa e inercia respecto al pomo (mano trasera):
            // Mango: varilla cilíndrica desde x=0 hasta x=safeHandleLen
            float comHandle = safeHandleLen * 0.5f;
            float inertiaHandle = (1f / 3f) * handleMass * (safeHandleLen * safeHandleLen);

            // Cabeza: centrada a distancia comHead
            float comHead = safeHandleLen + (safeHeadLen * 0.45f);
            float inertiaHead = headMass * (comHead * comHead) + (1f / 12f) * headMass * (safeHeadLen * safeHeadLen);

            float totalInertia = Mathf.Max(0.01f, inertiaHandle + inertiaHead);
            float comTotal = (handleMass * comHandle + headMass * comHead) / safeMass;

            // 3. Radio volumétrico de la cabeza para barrido
            float radiusHead = Mathf.Max(safeHeadWidth * 0.5f, safeHeadThickness * 0.5f);
            radiusHead = Mathf.Clamp(radiusHead, 0.04f, 0.45f);

            // 4. Clasificación de Agarre (GripMode)
            float hNorm = Mathf.InverseLerp(config.minHandleLength, config.maxHandleLength, safeHandleLen);
            GripMode gripMode;
            if (hNorm < config.gripOneShortThreshold) gripMode = GripMode.OneShort;
            else if (hNorm < config.gripOneHandThreshold) gripMode = GripMode.OneHand;
            else if (hNorm < config.gripTwoHandThreshold) gripMode = GripMode.TwoHand;
            else gripMode = GripMode.Pole;

            // Si la masa excede el umbral, forzar mínimo TwoHand por plausibilidad física
            if (safeMass > config.forceTwoHandMassThreshold && gripMode < GripMode.TwoHand)
            {
                gripMode = GripMode.TwoHand;
            }

            // 5. Clasificación de Familia (WeaponFamily)
            float lenToWidthRatio = safeHeadLen / safeHeadWidth;
            WeaponFamily weaponFamily;

            if (lenToWidthRatio >= config.bladeLengthToWidthRatio && safeSharpness >= config.bladeSharpnessThreshold)
            {
                weaponFamily = WeaponFamily.Blade;
            }
            else if (latExtent >= config.axeLateralExtentThreshold && safeSharpness >= config.axeSharpnessThreshold)
            {
                weaponFamily = WeaponFamily.Axe;
            }
            else
            {
                weaponFamily = WeaponFamily.Blunt;
            }

            // 6. Generación de Nombre Procedural (Sección 4 C)
            float shaftRatio = safeHandleLen / safeHeadLen;
            bool isShaftVariant = shaftRatio >= config.polearmRatioThreshold;
            string name = GenerateWeaponName(weaponFamily, gripMode, isShaftVariant, safeMass);

            return new WeaponProfile
            {
                family = weaponFamily,
                grip = gripMode,
                mass = safeMass,
                length = totalLen,
                handleLength = safeHandleLen,
                headLength = safeHeadLen,
                headWidth = safeHeadWidth,
                headThickness = safeHeadThickness,
                headSharpness = safeSharpness,
                lateralExtent = latExtent,
                gripToCom = comTotal,
                inertia = totalInertia,
                headRadius = radiusHead,
                generatedName = name
            };
        }

        private static string GenerateWeaponName(WeaponFamily family, GripMode grip, bool isShaftVariant, float mass)
        {
            return family switch
            {
                WeaponFamily.Blade => grip switch
                {
                    GripMode.OneShort => "Daga",
                    GripMode.OneHand => isShaftVariant ? "Sable de Asta" : "Espada",
                    GripMode.TwoHand => "Mandoble",
                    GripMode.Pole => "Guja",
                    _ => "Espada"
                },
                WeaponFamily.Axe => grip switch
                {
                    GripMode.OneShort => "Hachuela",
                    GripMode.OneHand => "Hacha de Mano",
                    GripMode.TwoHand => isShaftVariant ? "Alabarda" : "Hacha de Guerra",
                    GripMode.Pole => "Alabarda",
                    _ => "Hacha de Guerra"
                },
                WeaponFamily.Blunt => grip switch
                {
                    GripMode.OneShort => "Porra",
                    GripMode.OneHand => "Maza",
                    GripMode.TwoHand => mass > 5.5f ? "Gran Mazo" : "Martillo de Guerra",
                    GripMode.Pole => "Martillo de Asta",
                    _ => "Martillo de Guerra"
                },
                _ => "Arma"
            };
        }

        public override string ToString()
        {
            return $"{generatedName} [{family}, {grip}] | Masa: {mass:F1}kg, Long: {length:F2}m (Mango: {handleLength:F2}m), Inercia: {inertia:F2} kg·m², CoM: {gripToCom:F2}m";
        }
    }
}
