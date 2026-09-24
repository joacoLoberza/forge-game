using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public static partial class WeaponAnalyzer
    {
        private static WeaponCapabilities DetermineCapabilities(
            WeaponType type, HeadType headType, float mass, float handleLen, float headLen, float latExt)
        {
            WeaponCapabilities caps = WeaponCapabilities.None;

            if (headType == HeadType.Edge)
            {
                caps |= WeaponCapabilities.CanSlash;
                if (type == WeaponType.Axe || mass >= 2.6f) caps |= WeaponCapabilities.CanChop;
            }

            if (headType == HeadType.Point || (type == WeaponType.Sword && headLen >= 0.35f))
            {
                caps |= WeaponCapabilities.CanThrust;
            }

            if (headType == HeadType.Blunt || type == WeaponType.Hammer || mass >= 3.2f)
            {
                caps |= WeaponCapabilities.CanSmash;
            }

            if (type == WeaponType.Axe && latExt >= 0.12f)
            {
                caps |= WeaponCapabilities.CanHook;
            }

            if (handleLen >= 0.85f || mass >= 4.0f)
            {
                caps |= WeaponCapabilities.IsTwoHanded;
            }

            return caps;
        }

        private static WeaponArchetype DetermineArchetype(
            WeaponStats weapon, float totalMass, float handleLen, float totalLen, WeaponCapabilities caps)
        {
            if (handleLen >= 1.25f && totalLen >= 1.65f)
            {
                return WeaponArchetype.Polearm;
            }

            if ((caps & WeaponCapabilities.IsTwoHanded) != 0 || weapon.ingotSize == ItemSize.Large || totalMass >= 4.0f)
            {
                return WeaponArchetype.Heavy2H;
            }

            if (weapon.weaponType == WeaponType.Axe) return WeaponArchetype.Axe1H;
            if (weapon.weaponType == WeaponType.Hammer) return WeaponArchetype.Hammer1H;

            if (weapon.weaponType == WeaponType.Sword && (handleLen <= 0.40f && totalLen <= 0.65f))
            {
                return WeaponArchetype.Dagger;
            }

            return WeaponArchetype.Sword1H;
        }

        /// <summary>
        /// Sustituye un movimiento si el arma no cuenta con la capacidad requerida (ej. estocada con martillo -> golpe con culata).
        /// </summary>
        public static AttackMove ResolveMove(AttackMove desiredMove, WeaponAnalysis analysis)
        {
            if (desiredMove == null) return null;

            if (desiredMove.requiredCapability != WeaponCapabilities.None &&
                (analysis.capabilities & desiredMove.requiredCapability) == 0)
            {
                if (desiredMove.fallbackMove != null)
                {
                    return desiredMove.fallbackMove;
                }
            }

            return desiredMove;
        }
    }
}
