using UnityEngine;

namespace ForjaGame.Combat
{
    public static partial class ComboCatalog
    {
        private static AttackMove[] BuildHeavy2HCombo()
        {
            return new AttackMove[]
            {
                CreateMove("Mandoble: Barrido Amplio", WeaponArchetype.Heavy2H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 1.30f, 1.15f, 0.52f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.45f, 0.12f, 0.22f), new Vector3(0.6f, 0.5f, 0.5f), new TorsoAngles(new Vector3(0, 18f, 0), new Vector3(0, 26f, 0), new Vector3(0, 35f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.35f, -0.10f, 0.52f), new Vector3(-0.7f, 0.1f, 0.5f), new TorsoAngles(new Vector3(0, -18f, 0), new Vector3(0, -28f, 0), new Vector3(0, -36f, 0)), 0.52f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Mandoble: Tajo de Inercia", WeaponArchetype.Heavy2H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 1.25f, 1.20f, 0.50f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.40f, -0.05f, 0.28f), new Vector3(-0.7f, 0.2f, 0.5f), new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -32f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.40f, 0.02f, 0.50f), new Vector3(0.8f, -0.1f, 0.4f), new TorsoAngles(new Vector3(0, 18f, 0), new Vector3(0, 26f, 0), new Vector3(0, 34f, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Mandoble: Estocada a Dos Manos", WeaponArchetype.Heavy2H, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust | WeaponCapabilities.CanSlash, 1.15f, 1.30f, 0.48f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.20f, -0.12f, 0.22f), Vector3.forward, new TorsoAngles(new Vector3(0, 12f, 0), new Vector3(0, 16f, 0), new Vector3(0, 20f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.04f, -0.05f, 0.70f), Vector3.forward, new TorsoAngles(new Vector3(4f, -12f, 0), new Vector3(8f, -16f, 0), new Vector3(12f, -22f, 0)), 0.48f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Mandoble: Tajo Cleave Diagonal", WeaponArchetype.Heavy2H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 1.25f, 1.35f, 0.50f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.35f, 0.25f, 0.20f), new Vector3(0.5f, 0.7f, 0.2f), new TorsoAngles(new Vector3(0, 16f, 0), new Vector3(0, 22f, 0), new Vector3(0, 28f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.25f, -0.15f, 0.55f), new Vector3(-0.6f, -0.2f, 0.6f), new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -30f, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Mandoble: Impacto Demoledor Finisher", WeaponArchetype.Heavy2H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 1.45f, 1.65f, 0.58f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.48f, 0.15f, 0.15f), new Vector3(0.8f, 0.3f, -0.3f), new TorsoAngles(new Vector3(0, 25f, 0), new Vector3(0, 35f, 0), new Vector3(0, 48f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.45f, -0.15f, 0.60f), new Vector3(-0.8f, 0.1f, 0.4f), new TorsoAngles(new Vector3(0, -25f, 0), new Vector3(0, -35f, 0), new Vector3(0, -48f, 0)), 0.58f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    })
            };
        }

        private static AttackMove[] BuildPolearmCombo()
        {
            return new AttackMove[]
            {
                CreateMove("Asta: Estocada Recta", WeaponArchetype.Polearm, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 1.05f, 1.05f, 0.44f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.25f, -0.08f, 0.20f), Vector3.forward, new TorsoAngles(new Vector3(0, 10f, 0), new Vector3(0, 14f, 0), new Vector3(0, 18f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.04f, -0.02f, 0.62f), Vector3.forward, new TorsoAngles(new Vector3(0, -10f, 0), new Vector3(0, -14f, 0), new Vector3(0, -20f, 0)), 0.44f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Asta: Barrido Deflector de Astil", WeaponArchetype.Polearm, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSmash | WeaponCapabilities.CanSlash, 1.10f, 1.15f, 0.46f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.42f, 0.05f, 0.25f), new Vector3(0.7f, 0.2f, 0.5f), new TorsoAngles(new Vector3(0, 16f, 0), new Vector3(0, 22f, 0), new Vector3(0, 28f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.35f, -0.05f, 0.55f), new Vector3(-0.8f, 0.0f, 0.4f), new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -30f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Asta: Estocada Deslizante Baja", WeaponArchetype.Polearm, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 1.05f, 1.25f, 0.44f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.28f, -0.06f, 0.15f), Vector3.forward, new TorsoAngles(new Vector3(0, 12f, 0), new Vector3(0, 16f, 0), new Vector3(0, 22f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.02f, 0.02f, 0.72f), Vector3.forward, new TorsoAngles(new Vector3(0, -12f, 0), new Vector3(0, -18f, 0), new Vector3(0, -25f, 0)), 0.44f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Asta: Barrido Bajo y Culatazo", WeaponArchetype.Polearm, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSmash, 1.10f, 1.10f, 0.46f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.32f, -0.12f, 0.25f), new Vector3(-0.6f, 0.3f, 0.5f), new TorsoAngles(new Vector3(0, -14f, 0), new Vector3(0, -18f, 0), new Vector3(0, -24f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.32f, 0.02f, 0.48f), new Vector3(0.7f, 0.1f, 0.5f), new TorsoAngles(new Vector3(0, 14f, 0), new Vector3(0, 20f, 0), new Vector3(0, 26f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Asta: Estocada Imperial Finisher", WeaponArchetype.Polearm, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 1.25f, 1.65f, 0.50f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.30f, 0.05f, 0.10f), Vector3.forward, new TorsoAngles(new Vector3(0, 16f, 0), new Vector3(0, 24f, 0), new Vector3(0, 32f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.01f, 0.04f, 0.82f), Vector3.forward, new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -32f, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    })
            };
        }
    }
}
