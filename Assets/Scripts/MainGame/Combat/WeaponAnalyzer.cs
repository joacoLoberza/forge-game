using System;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public enum HeadType
    {
        Edge,
        Blunt,
        Point
    }

    [Flags]
    public enum WeaponCapabilities
    {
        None = 0,
        CanSlash = 1 << 0,
        CanThrust = 1 << 1,
        CanChop = 1 << 2,
        CanSmash = 1 << 3,
        CanHook = 1 << 4,
        IsTwoHanded = 1 << 5
    }

    public enum WeaponArchetype
    {
        Dagger,
        Sword1H,
        Axe1H,
        Hammer1H,
        Heavy2H,
        Polearm
    }

    [Serializable]
    public struct WeaponAnalysis
    {
        public float totalLength;
        public float handleLength;
        public float headLength;
        public float headWidth;
        public float headThickness;
        public float headMass;
        public float handleMass;
        public float totalMass;
        public float centerOfMass;
        public float inertia;
        public HeadType headType;
        public WeaponCapabilities capabilities;
        public WeaponArchetype archetype;

        public bool CanSlash => (capabilities & WeaponCapabilities.CanSlash) != 0;
        public bool CanThrust => (capabilities & WeaponCapabilities.CanThrust) != 0;
        public bool CanChop => (capabilities & WeaponCapabilities.CanChop) != 0;
        public bool CanSmash => (capabilities & WeaponCapabilities.CanSmash) != 0;
        public bool CanHook => (capabilities & WeaponCapabilities.CanHook) != 0;
        public bool IsTwoHanded => (capabilities & WeaponCapabilities.IsTwoHanded) != 0;
    }

    /// <summary>
    /// Analizador físico y biomecánico del arma forjada.
    /// Mide masa, inercia, dimensiones y deduce las capacidades marciales y arquetipo.
    /// </summary>
    public static partial class WeaponAnalyzer
    {
        public static WeaponAnalysis Analyze(WeaponStats weapon, float rigArmLength = 0.65f)
        {
            if (weapon == null)
            {
                return GetDefaultAnalysis();
            }

            // 1. Longitud del mango
            float hLen = weapon.handleLength switch
            {
                HandleLength.Short => 0.35f,
                HandleLength.Medium => 0.65f,
                HandleLength.Long => 1.45f,
                _ => 0.55f
            };
            if (weapon.customHandleMesh != null && weapon.customHandleMesh.bounds.size.x > 0.05f)
                hLen = weapon.customHandleMesh.bounds.size.x;

            // 2. Dimensiones de la cabeza
            float headL = 0.80f, headW = 0.06f, headT = 0.015f, latExt = 0.02f;
            if (weapon.customMesh != null && weapon.customMesh.bounds.size.sqrMagnitude > 0.001f)
            {
                var b = weapon.customMesh.bounds;
                headL = Mathf.Max(0.10f, b.size.x);
                headW = Mathf.Max(0.02f, b.size.z);
                headT = Mathf.Max(0.008f, b.size.y);
                latExt = Mathf.Abs(b.center.z) / Mathf.Max(0.01f, b.size.z);
            }
            else
            {
                switch (weapon.weaponType)
                {
                    case WeaponType.Axe:
                        headL = 0.34f; headW = 0.22f; headT = 0.04f; latExt = 0.35f;
                        break;
                    case WeaponType.Hammer:
                        headL = 0.26f; headW = 0.18f; headT = 0.16f; latExt = 0.05f;
                        break;
                    default:
                        headL = 0.85f; headW = 0.06f; headT = 0.015f; latExt = 0.02f;
                        break;
                }
            }

            // 3. Masas
            float headM = weapon.ingotSize switch
            {
                ItemSize.Small => 1.2f,
                ItemSize.Medium => 2.8f,
                ItemSize.Large => 5.8f,
                _ => 2.5f
            };
            float handleM = weapon.handleLength switch
            {
                HandleLength.Short => 0.25f,
                HandleLength.Medium => 0.65f,
                HandleLength.Long => 1.40f,
                _ => 0.55f
            };
            float totMass = headM + handleM;
            float totLen = hLen + headL;

            // 4. Centro de masa e inercia respecto al agarre trasero (I = m * d_cm^2)
            float comHandle = hLen * 0.5f;
            float comHead = hLen + (headL * 0.45f);
            float dCM = (handleM * comHandle + headM * comHead) / totMass;
            float inertia = totMass * (dCM * dCM) + (1f / 12f) * headM * (headL * headL);

            // 5. Tipo de cabeza y capacidades
            HeadType hType = DetermineHeadType(weapon.weaponType, headL, headW, weapon.currentSharpness, weapon.maxSharpness);
            WeaponCapabilities caps = DetermineCapabilities(weapon.weaponType, hType, totMass, hLen, headL, latExt);
            WeaponArchetype arch = DetermineArchetype(weapon, totMass, hLen, totLen, caps);

            return new WeaponAnalysis
            {
                totalLength = totLen,
                handleLength = hLen,
                headLength = headL,
                headWidth = headW,
                headThickness = headT,
                headMass = headM,
                handleMass = handleM,
                totalMass = totMass,
                centerOfMass = dCM,
                inertia = Mathf.Max(0.01f, inertia),
                headType = hType,
                capabilities = caps,
                archetype = arch
            };
        }

        private static HeadType DetermineHeadType(WeaponType type, float headL, float headW, float sharpness, float maxSharpness)
        {
            float sh = maxSharpness > 0.01f ? (sharpness / maxSharpness) : 0.6f;
            if (type == WeaponType.Hammer || sh < 0.20f) return HeadType.Blunt;
            if (type == WeaponType.Sword && (headL / Mathf.Max(0.01f, headW)) > 6.0f) return HeadType.Point;
            return HeadType.Edge;
        }

        private static WeaponAnalysis GetDefaultAnalysis()
        {
            return new WeaponAnalysis
            {
                totalLength = 1.0f,
                handleLength = 0.55f,
                headLength = 0.45f,
                headWidth = 0.05f,
                headThickness = 0.02f,
                headMass = 2.0f,
                handleMass = 0.5f,
                totalMass = 2.5f,
                centerOfMass = 0.65f,
                inertia = 1.05f,
                headType = HeadType.Edge,
                capabilities = WeaponCapabilities.CanSlash | WeaponCapabilities.CanThrust,
                archetype = WeaponArchetype.Sword1H
            };
        }
    }
}
