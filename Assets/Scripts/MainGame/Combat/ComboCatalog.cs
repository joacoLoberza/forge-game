using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Catálogo y fábrica centralizada de combos de 5 golpes para los 6 arquetipos de armas.
    /// Define los waypoints en espacio del pecho, orientaciones, fases marciales y finishers.
    /// </summary>
    public static partial class ComboCatalog
    {
        private static readonly Dictionary<WeaponArchetype, AttackMove[]> _cache = new();

        public static AttackMove[] GetCombo(WeaponArchetype archetype)
        {
            if (_cache.TryGetValue(archetype, out var moves) && moves != null && moves.Length == 5)
            {
                return moves;
            }

            AttackMove[] generated = archetype switch
            {
                WeaponArchetype.Dagger => BuildDaggerCombo(),
                WeaponArchetype.SwordShort => BuildScaled(BuildSwordCombo(), archetype, 0.78f, 0.90f, 0.76f, "Short Sword"),
                WeaponArchetype.Sword1H or WeaponArchetype.SwordMed => BuildScaled(BuildSwordCombo(), archetype, 1.00f, 1.00f, 1.00f, "Medium Sword"),
                WeaponArchetype.Heavy2H or WeaponArchetype.SwordLong => BuildScaled(BuildHeavy2HCombo(), archetype, 1.18f, 1.14f, 1.18f, "Long Greatsword"),
                WeaponArchetype.Polearm => BuildPolearmCombo(),
                WeaponArchetype.AxeShort => BuildScaled(BuildAxeCombo(), archetype, 0.80f, 0.90f, 0.78f, "Short Axe"),
                WeaponArchetype.Axe1H or WeaponArchetype.AxeMed => BuildScaled(BuildAxeCombo(), archetype, 1.00f, 1.02f, 1.00f, "Medium Axe"),
                WeaponArchetype.AxeLong => BuildScaled(BuildAxeCombo(), archetype, 1.20f, 1.18f, 1.16f, "Long Greataxe"),
                WeaponArchetype.HammerShort => BuildScaled(BuildHammerCombo(), archetype, 0.82f, 0.92f, 0.78f, "Short Hammer"),
                WeaponArchetype.Hammer1H or WeaponArchetype.HammerMed => BuildScaled(BuildHammerCombo(), archetype, 1.00f, 1.04f, 1.00f, "Medium Hammer"),
                WeaponArchetype.HammerLong => BuildScaled(BuildHammerCombo(), archetype, 1.22f, 1.20f, 1.15f, "Long Maul"),
                WeaponArchetype.Pickaxe => BuildPickaxeCombo(),
                _ => BuildSwordCombo()
            };

            _cache[archetype] = generated;
            return generated;
        }

        private static AttackMove[] BuildScaled(AttackMove[] src, WeaponArchetype arch, float durScale, float dmgScale, float lungeScale, string label)
        {
            var res = new AttackMove[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                AttackMove m = Object.Instantiate(src[i]);
                m.name = $"{label} {i + 1}";
                m.moveName = $"{label}: {src[i].moveName}";
                m.archetype = arch;
                m.baseDuration *= durScale;
                m.damageMultiplier *= dmgScale;
                m.lungeDistance *= lungeScale;
                if (arch.ToString().StartsWith("Axe") || arch.ToString().StartsWith("Hammer"))
                    if (m.motionType == AttackMotionType.Thrust) m.motionType = AttackMotionType.OverheadSlam;
                res[i] = m;
            }
            return res;
        }

        private static AttackMove CreateMove(
            string name, WeaponArchetype arch, AttackMotionType motion,
            WeaponCapabilities reqCap, float baseDur, float dmgMult, float lunge,
            AttackWaypoint[] wps, AttackMove fallback = null)
        {
            var move = ScriptableObject.CreateInstance<AttackMove>();
            move.name = name;
            move.moveName = name;
            move.archetype = arch;
            move.motionType = motion;
            move.requiredCapability = reqCap;
            move.baseDuration = baseDur;
            move.damageMultiplier = dmgMult;
            move.lungeDistance = lunge;
            move.waypoints = wps;
            move.fallbackMove = fallback;

            // Ventana amplia alineada con el barrido real del arma (desde el lanzamiento hasta la salida)
            move.anticipationEnd = 0.16f;
            move.hitApex = 0.44f;
            move.hitWindowStart = 0.18f;
            move.hitWindowEnd = 0.72f;
            move.recoveryStart = 0.78f;
            move.hitStopDuration = 0.055f;

            return move;
        }
    }
}
