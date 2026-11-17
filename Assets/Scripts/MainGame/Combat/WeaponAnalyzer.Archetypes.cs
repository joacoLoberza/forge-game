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

            if (headType == HeadType.Point || type == WeaponType.Sword)
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
            if (weapon == null) return WeaponArchetype.SwordMed;
            if (weapon.weaponType == WeaponType.Pickaxe) return WeaponArchetype.Pickaxe;
            bool heavyLarge = weapon.ingotSize == ItemSize.Large && weapon.handleLength != HandleLength.Short;
            return weapon.weaponType switch
            {
                WeaponType.Sword => weapon.handleLength switch
                {
                    HandleLength.Short => WeaponArchetype.SwordShort,
                    HandleLength.Long => WeaponArchetype.SwordLong,
                    _ => heavyLarge ? WeaponArchetype.SwordLong : WeaponArchetype.SwordMed
                },
                WeaponType.Axe => weapon.handleLength switch
                {
                    HandleLength.Short => WeaponArchetype.AxeShort,
                    HandleLength.Long => WeaponArchetype.AxeLong,
                    _ => heavyLarge ? WeaponArchetype.AxeLong : WeaponArchetype.AxeMed
                },
                WeaponType.Hammer => weapon.handleLength switch
                {
                    HandleLength.Short => WeaponArchetype.HammerShort,
                    HandleLength.Long => WeaponArchetype.HammerLong,
                    _ => heavyLarge ? WeaponArchetype.HammerLong : WeaponArchetype.HammerMed
                },
                _ => WeaponArchetype.SwordMed
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
