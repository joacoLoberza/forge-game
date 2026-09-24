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
                WeaponArchetype.Sword1H => BuildSwordCombo(),
                WeaponArchetype.Axe1H => BuildAxeCombo(),
                WeaponArchetype.Hammer1H => BuildHammerCombo(),
                WeaponArchetype.Heavy2H => BuildHeavy2HCombo(),
                WeaponArchetype.Polearm => BuildPolearmCombo(),
                _ => BuildSwordCombo()
            };

            _cache[archetype] = generated;
            return generated;
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

            // Fases estándar
            move.anticipationEnd = 0.22f;
            move.hitApex = 0.46f;
            move.hitWindowStart = 0.40f;
            move.hitWindowEnd = 0.54f;
            move.recoveryStart = 0.60f;
            move.hitStopDuration = 0.055f;

            return move;
        }
    }
}
