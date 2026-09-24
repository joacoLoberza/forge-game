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
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 0.75f, 1.15f, 0.30f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.45f, 0.12f, 0.22f), new Vector3(0.6f, 0.5f, 0.5f), new TorsoAngles(new Vector3(0, 18f, 0), new Vector3(0, 26f, 0), new Vector3(0, 35f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.35f, -0.10f, 0.52f), new Vector3(-0.7f, 0.1f, 0.5f), new TorsoAngles(new Vector3(0, -18f, 0), new Vector3(0, -28f, 0), new Vector3(0, -36f, 0)), 0.48f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Mandoble: Tajo de Inercia", WeaponArchetype.Heavy2H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 0.72f, 1.20f, 0.32f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.40f, -0.05f, 0.28f), new Vector3(-0.7f, 0.2f, 0.5f), new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -32f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.40f, 0.02f, 0.50f), new Vector3(0.8f, -0.1f, 0.4f), new TorsoAngles(new Vector3(0, 18f, 0), new Vector3(0, 26f, 0), new Vector3(0, 34f, 0)), 0.48f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Mandoble: Corte Descendente", WeaponArchetype.Heavy2H, AttackMotionType.OverheadSlam,
                    WeaponCapabilities.CanChop | WeaponCapabilities.CanSmash, 0.78f, 1.30f, 0.35f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.20f, 0.42f, 0.15f), new Vector3(0.1f, 0.95f, -0.1f), new TorsoAngles(Vector3.zero, new Vector3(-12f, 0, 0), new Vector3(-20f, 0, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.05f, -0.32f, 0.58f), new Vector3(0.1f, -0.6f, 0.8f), new TorsoAngles(new Vector3(6f, 0, 0), new Vector3(16f, 0, 0), new Vector3(25f, 0, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Mandoble: Elevación Pesada", WeaponArchetype.Heavy2H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 0.74f, 1.25f, 0.30f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.32f, -0.35f, 0.22f), new Vector3(0.3f, -0.7f, 0.5f), new TorsoAngles(new Vector3(0, 14f, 0), new Vector3(0, 20f, 0), new Vector3(0, 25f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.15f, 0.25f, 0.55f), new Vector3(-0.4f, 0.8f, 0.4f), new TorsoAngles(new Vector3(0, -14f, 0), new Vector3(0, -20f, 0), new Vector3(0, -26f, 0)), 0.48f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                // Finisher giro 360 de inercia o slam masivo
                CreateMove("Mandoble: Giro Demoledor Finisher", WeaponArchetype.Heavy2H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash | WeaponCapabilities.CanSmash, 0.95f, 1.65f, 0.60f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.48f, 0.15f, 0.15f), new Vector3(0.8f, 0.3f, -0.3f), new TorsoAngles(new Vector3(0, 25f, 0), new Vector3(0, 35f, 0), new Vector3(0, 48f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.45f, -0.15f, 0.60f), new Vector3(-0.8f, 0.1f, 0.4f), new TorsoAngles(new Vector3(0, -25f, 0), new Vector3(0, -35f, 0), new Vector3(0, -48f, 0)), 0.55f),
                        new AttackWaypoint(new Vector3(0.20f, -0.20f, 0.35f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    })
            };
        }

        private static AttackMove[] BuildPolearmCombo()
        {
            return new AttackMove[]
            {
                CreateMove("Asta: Estocada Recta", WeaponArchetype.Polearm, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 0.62f, 1.05f, 0.35f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.25f, -0.08f, 0.20f), Vector3.forward, new TorsoAngles(new Vector3(0, 10f, 0), new Vector3(0, 14f, 0), new Vector3(0, 18f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.04f, -0.02f, 0.62f), Vector3.forward, new TorsoAngles(new Vector3(0, -10f, 0), new Vector3(0, -14f, 0), new Vector3(0, -20f, 0)), 0.44f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Asta: Barrido de Astil", WeaponArchetype.Polearm, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSmash | WeaponCapabilities.CanSlash, 0.65f, 1.15f, 0.30f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.42f, 0.05f, 0.25f), new Vector3(0.7f, 0.2f, 0.5f), new TorsoAngles(new Vector3(0, 16f, 0), new Vector3(0, 22f, 0), new Vector3(0, 28f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(-0.35f, -0.05f, 0.55f), new Vector3(-0.8f, 0.0f, 0.4f), new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -30f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Asta: Estocada Deslizante Larga", WeaponArchetype.Polearm, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 0.70f, 1.25f, 0.42f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.28f, -0.06f, 0.15f), Vector3.forward, new TorsoAngles(new Vector3(0, 12f, 0), new Vector3(0, 16f, 0), new Vector3(0, 22f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.02f, 0.02f, 0.72f), Vector3.forward, new TorsoAngles(new Vector3(0, -12f, 0), new Vector3(0, -18f, 0), new Vector3(0, -25f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Asta: Golpe Reverso con Culata", WeaponArchetype.Polearm, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSmash, 0.60f, 1.10f, 0.28f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.32f, -0.12f, 0.25f), new Vector3(-0.6f, 0.3f, 0.5f), new TorsoAngles(new Vector3(0, -14f, 0), new Vector3(0, -18f, 0), new Vector3(0, -24f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.32f, 0.02f, 0.48f), new Vector3(0.7f, 0.1f, 0.5f), new TorsoAngles(new Vector3(0, 14f, 0), new Vector3(0, 20f, 0), new Vector3(0, 26f, 0)), 0.44f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                // Finisher estocada imperial a máxima extensión
                CreateMove("Asta: Estocada Imperial Finisher", WeaponArchetype.Polearm, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 0.88f, 1.65f, 0.65f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.30f, 0.05f, 0.10f), Vector3.forward, new TorsoAngles(new Vector3(0, 16f, 0), new Vector3(0, 24f, 0), new Vector3(0, 32f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.01f, 0.04f, 0.82f), Vector3.forward, new TorsoAngles(new Vector3(0, -16f, 0), new Vector3(0, -24f, 0), new Vector3(0, -32f, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(0.22f, -0.18f, 0.32f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    })
            };
        }
    }
}
