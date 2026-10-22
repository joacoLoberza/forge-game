using UnityEngine;

namespace ForjaGame.Combat
{
    public static partial class ComboCatalog
    {
        private static AttackMove[] BuildSwordCombo()
        {
            return new AttackMove[]
            {
                // Golpe 1: Tajo diagonal descendente (45° Overhead Slice)
                CreateMove("Espada: Tajo Diagonal", WeaponArchetype.Sword1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash, 0.80f, 1.0f, 0.38f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.24f, -0.12f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(0.32f, 0.15f, 0.24f), new Vector3(0.3f, 0.9f, -0.1f), new TorsoAngles(new Vector3(0, 10f, 0), new Vector3(0, 14f, 0), new Vector3(0, 18f, 0)), 0.22f),
                        new AttackWaypoint(new Vector3(0.12f, -0.04f, 0.52f), new Vector3(-0.4f, -0.3f, 0.8f), new TorsoAngles(new Vector3(0, -8f, 0), new Vector3(0, -12f, 0), new Vector3(0, -15f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(-0.10f, -0.18f, 0.40f), new Vector3(-0.6f, -0.5f, 0.6f), new TorsoAngles(new Vector3(0, -10f, 0), new Vector3(0, -15f, 0), new Vector3(0, -20f, 0)), 0.65f),
                        new AttackWaypoint(new Vector3(0.24f, -0.12f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                // Golpe 2: Tajo reverso horizontal (Chest Cleave)
                CreateMove("Espada: Tajo Reverso", WeaponArchetype.Sword1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash, 0.72f, 1.1f, 0.35f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.10f, -0.05f, 0.38f), new Vector3(-0.5f, 0.5f, 0.6f), TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(-0.14f, 0.0f, 0.32f), new Vector3(-0.7f, 0.3f, 0.5f), new TorsoAngles(new Vector3(0, -10f, 0), new Vector3(0, -14f, 0), new Vector3(0, -18f, 0)), 0.20f),
                        new AttackWaypoint(new Vector3(0.15f, -0.02f, 0.48f), new Vector3(0.7f, 0.1f, 0.6f), new TorsoAngles(new Vector3(0, 8f, 0), new Vector3(0, 12f, 0), new Vector3(0, 16f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.32f, -0.08f, 0.38f), new Vector3(0.8f, -0.1f, 0.4f), new TorsoAngles(new Vector3(0, 12f, 0), new Vector3(0, 16f, 0), new Vector3(0, 20f, 0)), 0.65f),
                        new AttackWaypoint(new Vector3(0.24f, -0.12f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                // Golpe 3: Estocada penetrante recta (Thrust)
                CreateMove("Espada: Estocada Penetrante", WeaponArchetype.Sword1H, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust | WeaponCapabilities.CanSlash, 0.70f, 1.20f, 0.42f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.24f, -0.10f, 0.25f), Vector3.forward, new TorsoAngles(new Vector3(0, 8f, 0), new Vector3(0, 12f, 0), new Vector3(0, 16f, 0)), 0.0f),
                        new AttackWaypoint(new Vector3(0.06f, -0.04f, 0.62f), Vector3.forward, new TorsoAngles(new Vector3(2f, -8f, 0), new Vector3(4f, -12f, 0), new Vector3(6f, -16f, 0)), 0.42f),
                        new AttackWaypoint(new Vector3(0.24f, -0.12f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                // Golpe 4: Tajo de continuidad bajo-alto
                CreateMove("Espada: Tajo de Continuidad", WeaponArchetype.Sword1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash, 0.78f, 1.25f, 0.38f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.12f, -0.15f, 0.30f), new Vector3(-0.4f, -0.3f, 0.6f), TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(-0.18f, -0.22f, 0.24f), new Vector3(-0.6f, -0.4f, 0.5f), new TorsoAngles(new Vector3(0, -10f, 0), new Vector3(0, -14f, 0), new Vector3(0, -18f, 0)), 0.20f),
                        new AttackWaypoint(new Vector3(0.18f, 0.05f, 0.50f), new Vector3(0.5f, 0.3f, 0.7f), new TorsoAngles(new Vector3(0, 8f, 0), new Vector3(0, 12f, 0), new Vector3(0, 16f, 0)), 0.46f),
                        new AttackWaypoint(new Vector3(0.24f, -0.12f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    }),

                // Golpe 5: Finisher - Tajo devastador de potencia
                CreateMove("Espada: Tajo Potente Finisher", WeaponArchetype.Sword1H, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash, 0.95f, 1.65f, 0.45f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.28f, 0.05f, 0.28f), new Vector3(0.3f, 0.7f, 0.4f), TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(0.35f, 0.18f, 0.18f), new Vector3(0.4f, 0.8f, -0.2f), new TorsoAngles(new Vector3(0, 18f, 0), new Vector3(0, 24f, 0), new Vector3(0, 30f, 0)), 0.25f),
                        new AttackWaypoint(new Vector3(0.05f, -0.02f, 0.58f), new Vector3(-0.3f, -0.2f, 0.9f), new TorsoAngles(new Vector3(0, -14f, 0), new Vector3(0, -20f, 0), new Vector3(0, -26f, 0)), 0.50f),
                        new AttackWaypoint(new Vector3(-0.16f, -0.14f, 0.42f), new Vector3(-0.6f, -0.4f, 0.6f), new TorsoAngles(new Vector3(0, -18f, 0), new Vector3(0, -24f, 0), new Vector3(0, -30f, 0)), 0.70f),
                        new AttackWaypoint(new Vector3(0.24f, -0.12f, 0.36f), new Vector3(0.2f, 0.8f, 0.5f), TorsoAngles.Identity, 1.0f)
                    })
            };
        }

        private static AttackMove[] BuildDaggerCombo()
        {
            return new AttackMove[]
            {
                CreateMove("Daga: Tajo Rápido", WeaponArchetype.Dagger, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash, 0.38f, 0.85f, 0.18f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.22f, -0.10f, 0.28f), Vector3.forward, TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(0.28f, 0.02f, 0.22f), Vector3.forward, new TorsoAngles(new Vector3(0, 8f, 0), new Vector3(0, 10f, 0), new Vector3(0, 14f, 0)), 0.15f),
                        new AttackWaypoint(new Vector3(-0.08f, -0.05f, 0.42f), new Vector3(-0.4f, 0.2f, 0.8f), new TorsoAngles(new Vector3(0, -8f, 0), new Vector3(0, -10f, 0), new Vector3(0, -14f, 0)), 0.42f),
                        new AttackWaypoint(new Vector3(0.22f, -0.10f, 0.28f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Daga: Estocada Baja", WeaponArchetype.Dagger, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 0.36f, 0.90f, 0.20f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.20f, -0.16f, 0.22f), Vector3.forward, TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(0.08f, -0.18f, 0.48f), new Vector3(0f, -0.1f, 0.95f), new TorsoAngles(new Vector3(0, -4f, 0), new Vector3(0, -6f, 0), new Vector3(0, -8f, 0)), 0.40f),
                        new AttackWaypoint(new Vector3(0.22f, -0.10f, 0.28f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Daga: Tajo Reverso", WeaponArchetype.Dagger, AttackMotionType.HorizontalSlash,
                    WeaponCapabilities.CanSlash, 0.38f, 0.95f, 0.22f, new[]
                    {
                        new AttackWaypoint(new Vector3(-0.10f, -0.05f, 0.28f), Vector3.forward, TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(0.22f, 0.02f, 0.42f), new Vector3(0.6f, 0.1f, 0.7f), new TorsoAngles(new Vector3(0, 8f, 0), new Vector3(0, 10f, 0), new Vector3(0, 14f, 0)), 0.42f),
                        new AttackWaypoint(new Vector3(0.22f, -0.10f, 0.28f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Daga: Estocada Media", WeaponArchetype.Dagger, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 0.40f, 1.05f, 0.25f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.18f, -0.05f, 0.20f), Vector3.forward, TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(0.06f, -0.02f, 0.50f), Vector3.forward, new TorsoAngles(new Vector3(0, -4f, 0), new Vector3(0, -6f, 0), new Vector3(0, -8f, 0)), 0.42f),
                        new AttackWaypoint(new Vector3(0.22f, -0.10f, 0.28f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    }),

                CreateMove("Daga: Estocada con Lunge Finisher", WeaponArchetype.Dagger, AttackMotionType.Thrust,
                    WeaponCapabilities.CanThrust, 0.55f, 1.65f, 0.60f, new[]
                    {
                        new AttackWaypoint(new Vector3(0.22f, 0.05f, 0.15f), Vector3.forward, TorsoAngles.Identity, 0.0f),
                        new AttackWaypoint(new Vector3(0.26f, 0.08f, 0.10f), Vector3.forward, new TorsoAngles(new Vector3(0, 10f, 0), new Vector3(0, 14f, 0), new Vector3(0, 18f, 0)), 0.20f),
                        new AttackWaypoint(new Vector3(0.04f, -0.04f, 0.56f), Vector3.forward, new TorsoAngles(new Vector3(0, -10f, 0), new Vector3(0, -14f, 0), new Vector3(0, -18f, 0)), 0.45f),
                        new AttackWaypoint(new Vector3(0.22f, -0.10f, 0.28f), Vector3.forward, TorsoAngles.Identity, 1.0f)
                    })
            };
        }
    }
}
