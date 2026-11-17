using UnityEngine;

namespace ForjaGame.Combat
{
    public static partial class ComboCatalog
    {
        private static AttackMove[] BuildAxeCombo()
        {
            return new AttackMove[]
            {
                CreateMove("Axe: Diagonal Chop", WeaponArchetype.Axe1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanChop, 1.05f, 1.05f, 0.44f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.42f, 0.18f, 0.20f), new Vector3(0.6f, 0.6f, 0.3f), new TorsoAngles(new Vector3(0, 16f, 0), new Vector3(0, 22f, 0), new Vector3(0, 28f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.28f, -0.12f, 0.48f), new Vector3(-0.7f, 0.1f, 0.5f), new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -30f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.24f, -0.16f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Axe: Return Cut", WeaponArchetype.Axe1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanHook | WeaponCapabilities.CanChop, 1.00f, 1.15f, 0.42f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.35f, -0.05f, 0.28f), new Vector3(-0.7f, 0.2f, 0.5f), new TorsoAngles(new Vector3(0, -14f, 0), new Vector3(0, -18f, 0), new Vector3(0, -22f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.32f, -0.02f, 0.48f), new Vector3(0.8f, -0.2f, 0.4f), new TorsoAngles(new Vector3(0, 15f, 0), new Vector3(0, 20f, 0), new Vector3(0, 26f, 0)), 0.45f),
                        new AttackWaypoint(new Vector3(0.24f, -0.16f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Axe: Side Cleave", WeaponArchetype.Axe1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanChop, 1.02f, 1.2f, 0.44f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.38f, -0.25f, 0.22f), new Vector3(0.4f, -0.4f, 0.6f), new TorsoAngles(new Vector3(0, 14f, 0), new Vector3(0, 18f, 0), new Vector3(0, 24f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.25f, -0.05f, 0.52f), new Vector3(-0.6f, 0.3f, 0.6f), new TorsoAngles(new Vector3(0, -14f, 0), new Vector3(0, -20f, 0), new Vector3(0, -26f, 0)), 0.48f),
                        new AttackWaypoint(new Vector3(0.24f, -0.16f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Axe: Vertical Splitter", WeaponArchetype.Axe1H, AttackMotionType.OverheadSlam,
                    WeaponCapabilities.CanChop, 1.15f, 1.45f, 0.48f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.20f, 0.45f, 0.12f), new Vector3(0.1f, 0.95f, -0.1f), new TorsoAngles(Vector3.zero, new Vector3(-12f, 0, 0), new Vector3(-20f, 0, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.05f, -0.35f, 0.55f), new Vector3(0.1f, -0.6f, 0.8f), new TorsoAngles(new Vector3(6f, 0, 0), new Vector3(16f, 0, 0), new Vector3(26f, 0, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(0.24f, -0.16f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Axe: Beheading Finisher", WeaponArchetype.Axe1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanChop, 1.25f, 1.65f, 0.52f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.42f, 0.20f, 0.18f), new Vector3(0.7f, 0.5f, 0.2f), new TorsoAngles(new Vector3(0, 20f, 0), new Vector3(0, 28f, 0), new Vector3(0, 36f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.38f, -0.12f, 0.55f), new Vector3(-0.8f, 0.0f, 0.5f), new TorsoAngles(new Vector3(0, -20f, 0), new Vector3(0, -30f, 0), new Vector3(0, -40f, 0)), 0.52f),
                        new AttackWaypoint(new Vector3(0.24f, -0.16f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    })
            };
        }

        private static AttackMove[] BuildHammerCombo()
        {
            return new AttackMove[]
            {
                CreateMove("Hammer: Descending Slam", WeaponArchetype.Hammer1H, AttackMotionType.OverheadSlam,
                    WeaponCapabilities.CanSmash, 1.10f, 1.1f, 0.46f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.24f, 0.38f, 0.15f), new Vector3(0.1f, 0.9f, 0.0f), new TorsoAngles(Vector3.zero, new Vector3(-10f, 0, 0), new Vector3(-16f, 0, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.06f, -0.28f, 0.50f), new Vector3(0.1f, -0.4f, 0.8f), new TorsoAngles(new Vector3(5f, 0, 0), new Vector3(14f, 0, 0), new Vector3(22f, 0, 0)), 0.48f),
                        new AttackWaypoint(new Vector3(0.25f, -0.18f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Hammer: Heavy Side Strike", WeaponArchetype.Hammer1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSmash, 1.05f, 1.15f, 0.44f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.40f, 0.05f, 0.20f), new Vector3(0.7f, 0.2f, 0.5f), new TorsoAngles(new Vector3(0, 15f, 0), new Vector3(0, 22f, 0), new Vector3(0, 28f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.30f, -0.08f, 0.48f), new Vector3(-0.8f, 0.0f, 0.5f), new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -30f, 0)), 0.48f),
                        new AttackWaypoint(new Vector3(0.25f, -0.18f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Hammer: Front Crush", WeaponArchetype.Hammer1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSmash, 1.05f, 1.20f, 0.42f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.32f, -0.05f, 0.25f), new Vector3(-0.6f, 0.2f, 0.5f), new TorsoAngles(new Vector3(0, -14f, 0), new Vector3(0, -18f, 0), new Vector3(0, -22f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.28f, -0.02f, 0.50f), new Vector3(0.7f, -0.1f, 0.5f), new TorsoAngles(new Vector3(0, 14f, 0), new Vector3(0, 20f, 0), new Vector3(0, 26f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.25f, -0.18f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Hammer: Telluric Slam", WeaponArchetype.Hammer1H, AttackMotionType.OverheadSlam,
                    WeaponCapabilities.CanSmash, 1.20f, 1.35f, 0.48f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.22f, 0.42f, 0.12f), new Vector3(0.1f, 0.95f, -0.1f), new TorsoAngles(Vector3.zero, new Vector3(-12f, 0, 0), new Vector3(-20f, 0, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.05f, -0.32f, 0.52f), new Vector3(0.1f, -0.5f, 0.8f), new TorsoAngles(new Vector3(6f, 0, 0), new Vector3(16f, 0, 0), new Vector3(26f, 0, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(0.25f, -0.18f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Hammer: Crushing Finisher", WeaponArchetype.Hammer1H, AttackMotionType.OverheadSlam,
                    WeaponCapabilities.CanSmash, 1.35f, 1.65f, 0.52f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.25f, 0.48f, 0.10f), new Vector3(0.1f, 0.98f, -0.1f), new TorsoAngles(Vector3.zero, new Vector3(-14f, 0, 0), new Vector3(-24f, 0, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.04f, -0.38f, 0.58f), new Vector3(0.1f, -0.7f, 0.7f), new TorsoAngles(new Vector3(8f, 0, 0), new Vector3(20f, 0, 0), new Vector3(30f, 0, 0)), 0.52f),
                        new AttackWaypoint(new Vector3(0.25f, -0.18f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    })
            };
        }

        private static AttackMove[] BuildPickaxeCombo()
        {
            AttackWaypoint[] overhead =
            {
                new AttackWaypoint(new Vector3(0.18f, 0.44f, 0.10f), new Vector3(0.1f, 0.96f, -0.1f), new TorsoAngles(Vector3.zero, new Vector3(-12f, 0, 0), new Vector3(-20f, 0, 0)), 0.0f),
                new AttackWaypoint(new Vector3(0.04f, -0.30f, 0.52f), new Vector3(0.05f, -0.6f, 0.8f), new TorsoAngles(new Vector3(6f, 0, 0), new Vector3(14f, 0, 0), new Vector3(24f, 0, 0)), 0.46f),
                new AttackWaypoint(new Vector3(0.22f, -0.15f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
            };
            AttackWaypoint[] angled =
            {
                new AttackWaypoint(new Vector3(0.38f, 0.30f, 0.14f), new Vector3(0.45f, 0.8f, 0.1f), new TorsoAngles(new Vector3(0, 14f, 0), new Vector3(0, 18f, 0), new Vector3(0, 22f, 0)), 0.0f),
                new AttackWaypoint(new Vector3(-0.18f, -0.18f, 0.48f), new Vector3(-0.4f, -0.35f, 0.8f), new TorsoAngles(new Vector3(0, -12f, 0), new Vector3(0, -18f, 0), new Vector3(0, -24f, 0)), 0.46f),
                new AttackWaypoint(new Vector3(0.22f, -0.15f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
            };
            return new[]
            {
                CreateMove("Pickaxe: Overhead Strike", WeaponArchetype.Pickaxe, AttackMotionType.OverheadSlam, WeaponCapabilities.CanSmash, 0.78f, 1.00f, 0.14f, overhead),
                CreateMove("Pickaxe: Quarry Chop", WeaponArchetype.Pickaxe, AttackMotionType.HorizontalSlash, WeaponCapabilities.CanChop, 0.76f, 1.05f, 0.14f, angled),
                CreateMove("Pickaxe: Deep Vein Strike", WeaponArchetype.Pickaxe, AttackMotionType.OverheadSlam, WeaponCapabilities.CanSmash, 0.78f, 1.08f, 0.14f, overhead),
                CreateMove("Pickaxe: Fracture Cleave", WeaponArchetype.Pickaxe, AttackMotionType.HorizontalSlash, WeaponCapabilities.CanChop, 0.76f, 1.12f, 0.14f, angled),
                CreateMove("Pickaxe: Shatter Strike", WeaponArchetype.Pickaxe, AttackMotionType.OverheadSlam, WeaponCapabilities.CanSmash, 0.90f, 1.35f, 0.18f, overhead)
            };
        }
    }
}
