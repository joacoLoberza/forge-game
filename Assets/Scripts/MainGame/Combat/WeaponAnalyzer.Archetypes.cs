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
                caps |= WeaponCapabilities.CanThrust;

            if (headType == HeadType.Blunt || type == WeaponType.Hammer || mass >= 3.2f)
                caps |= WeaponCapabilities.CanSmash;

            if (type == WeaponType.Axe && latExt >= 0.12f)
                caps |= WeaponCapabilities.CanHook;

            if (handleLen >= 0.78f || mass >= 3.6f)
                caps |= WeaponCapabilities.IsTwoHanded;

            return caps;
        }

        private static WeaponArchetype DetermineArchetype(
            WeaponStats weapon, float totalMass, float handleLen, float totalLen, WeaponCapabilities caps)
        {
            if (weapon == null) return WeaponArchetype.Sword1H;
            if (weapon.weaponType == WeaponType.Pickaxe) return WeaponArchetype.Hammer1H;

            // Sincronizar estrictamente el arquetipo de combo con el estilo morfológico 1H / 2H / Polearm
            WeaponAnimationStyle style = weapon.GetAnimationStyle();
            return style switch
            {
                WeaponAnimationStyle.Polearm => WeaponArchetype.Polearm,
                WeaponAnimationStyle.Heavy2H => WeaponArchetype.Heavy2H,
                WeaponAnimationStyle.Dagger1H => WeaponArchetype.Dagger,
                WeaponAnimationStyle.Axe1H => WeaponArchetype.Axe1H,
                WeaponAnimationStyle.Hammer1H => WeaponArchetype.Hammer1H,
                _ => WeaponArchetype.Sword1H
            };
        }

        public static AttackMove ResolveMove(AttackMove desiredMove, WeaponAnalysis analysis)
        {
            if (desiredMove == null) return null;

            if (desiredMove.requiredCapability != WeaponCapabilities.None &&
                (analysis.capabilities & desiredMove.requiredCapability) == 0)
            {
                if (desiredMove.fallbackMove != null)
                    return desiredMove.fallbackMove;
            }

            return desiredMove;
        }
    }
}
