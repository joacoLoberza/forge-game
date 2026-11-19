using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Forge;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Ejecuta las mecánicas secundarias y efectos visuales 3D de cada aleación en combate
    /// (sin inflar daño ni velocidad base de los minerales):
    /// 1) Ferric Bronze: sin desgaste contra piedra/armadura + ondas de choque de bastión al golpear/bloquear/empujar.
    /// 2) Rose Gold: +12 Estamina por golpe + Tajo Creciente Carmesí y Sangrado prolongado en combos/espalda.
    /// 3) Blue Gold: Relámpago Cobalto en remates/golpes pesados que interrumpe ataques y salta en cadena.
    /// 4) Orichalcum: Nova Áurea en remates y parries que cura (+8 HP, +15 Estamina) y ralentiza 40% en área.
    /// </summary>
    public static class AlloyPassiveCombatResolver
    {
        public static bool OnWeaponHit(
            AttackController atk, WeaponStats weapon, IDamageable target, Vector3 hitPos,
            int dealtDamage, int comboIdx, bool isFinisher, bool isBackCrit, AttackMotionType motion)
        {
            var info = AlloyPassiveResolver.Resolve(weapon);
            if (info.Kind == AlloyPassiveKind.None || atk == null) return false;

            bool isLiving = target is CaveCreatureBase;
            bool isStoneOrNode = target is GolemEnemyController or TitanBossController or ResourceNodeController or IDestructibleTunnelWall;
            Vector3 playerPos = atk.transform.position;

            switch (info.Kind)
            {
                case AlloyPassiveKind.FerricBronze:
                    if (isStoneOrNode) AlloyPassiveVfx.SpawnSparkBurst(hitPos, info.AccentColor, new Color(1f, 0.85f, 0.55f), 14, 0.2f);
                    if (isLiving && (isFinisher || comboIdx == 2 || isBackCrit))
                        TriggerBronzeShockwave(atk.transform, hitPos, 2.5f, 0);
                    return isStoneOrNode; // Cero desgaste contra roca, menas o golems

                case AlloyPassiveKind.RoseGold:
                    if (isLiving)
                    {
                        atk.GetComponent<PlayerStaminaController>()?.RestoreStamina(12f);
                        AlloyPassiveVfx.SpawnSparkBurst(hitPos, info.AccentColor, Color.white, 16, 0.22f);
                        AlloyPassiveVfx.SpawnPlayerAuraPillar(atk.transform, info.AccentColor, 0.62f);
                        if (comboIdx == 2 || isFinisher || isBackCrit)
                        {
                            AlloyPassiveVfx.SpawnCrescentSlash(hitPos, atk.transform.forward, info.AccentColor, 1.75f);
                            AttachStatus(target as CaveCreatureBase)?.ApplyCrimsonBleed(3, 7, 0.75f);
                        }
                    }
                    return false;

                case AlloyPassiveKind.BlueGold:
                    if (target is IDestructibleTunnelWall or ResourceNodeController)
                    {
                        target.TakeDamage(Mathf.Max(3, Mathf.RoundToInt(dealtDamage * 0.45f)));
                        AlloyPassiveVfx.SpawnShockwaveRing(hitPos, 1.85f, info.AccentColor, 0.26f, 0.16f);
                    }
                    else if (isLiving && (isFinisher || isBackCrit || motion == AttackMotionType.OverheadSlam))
                    {
                        TriggerCobaltTremor(atk.transform, target as CaveCreatureBase, hitPos, info.AccentColor);
                    }
                    return false;

                case AlloyPassiveKind.Orichalcum:
                    if (isLiving)
                    {
                        AlloyPassiveVfx.SpawnSparkBurst(hitPos, info.AccentColor, new Color(1f, 0.96f, 0.72f), 18, 0.26f);
                        if (isFinisher || isBackCrit)
                            TriggerOrichalcumNova(atk.gameObject, playerPos, hitPos);
                    }
                    return false;
            }
            return false;
        }

        public static void OnWeaponBash(AttackController atk, WeaponStats weapon, Vector3 center)
        {
            var info = AlloyPassiveResolver.Resolve(weapon);
            if (info.Kind == AlloyPassiveKind.FerricBronze && atk != null)
                TriggerBronzeShockwave(atk.transform, center, 3.2f, 6);
        }

        public static void OnDefenseTriggered(GameObject defender, WeaponStats weapon, CaveCreatureBase attacker, bool timedParry)
        {
            if (defender == null) return;
            var info = AlloyPassiveResolver.Resolve(weapon);
            if (info.Kind == AlloyPassiveKind.FerricBronze)
                TriggerBronzeShockwave(defender.transform, defender.transform.position, 3.3f, timedParry ? 10 : 7);
            else if (info.Kind == AlloyPassiveKind.Orichalcum && timedParry)
                TriggerOrichalcumNova(defender, defender.transform.position, attacker != null ? attacker.transform.position : defender.transform.position);
        }

        private static void TriggerBronzeShockwave(Transform owner, Vector3 origin, float radius, int recoilDmg)
        {
            Color col = AlloyPassiveResolver.FerricBronzeInfo.AccentColor;
            AlloyPassiveVfx.SpawnShockwaveRing(origin, radius, col, 0.34f, 0.26f);
            AlloyPassiveVfx.SpawnSparkBurst(origin + Vector3.up * 0.6f, col, Color.white, 26, 0.45f);
            var creatures = CaveCreatureBase.ActiveCreatures;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var c = creatures[i];
                if (c == null || !c.IsAlive) continue;
                Vector3 diff = c.transform.position - origin; diff.y = 0f;
                if (diff.sqrMagnitude <= radius * radius)
                {
                    Vector3 push = (c.transform.position - owner.position); push.y = 0f;
                    c.ApplyKnockback(push.sqrMagnitude > 0.01f ? push : owner.forward, 8.6f, 0.22f);
                    if (recoilDmg > 0) c.TakeDamage(recoilDmg);
                }
            }
        }

        private static void TriggerCobaltTremor(Transform attacker, CaveCreatureBase primary, Vector3 hitPos, Color col)
        {
            AlloyPassiveVfx.SpawnShockwaveRing(hitPos, 3.6f, col, 0.32f, 0.26f);
            AlloyPassiveVfx.SpawnSparkBurst(hitPos + Vector3.up * 0.6f, col, Color.white, 28, 0.35f);
            ForgeAudioSystem.PlayChiselCut(hitPos);
            if (primary != null && primary.IsAlive)
            {
                AttachStatus(primary)?.ApplyBriefInterruptStagger(0.75f);
                primary.ApplyKnockback(primary.transform.position - attacker.position, 10.5f, 0.22f);
            }
            int chained = 0;
            var creatures = CaveCreatureBase.ActiveCreatures;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var c = creatures[i];
                if (c == null || c == primary || !c.IsAlive) continue;
                if ((c.transform.position - hitPos).sqrMagnitude <= 3.8f * 3.8f)
                {
                    AlloyPassiveVfx.SpawnLightningArc(hitPos + Vector3.up * 0.7f, c.transform.position + Vector3.up * 0.9f, col);
                    c.TakeDamage(10);
                    AttachStatus(c)?.ApplyBriefInterruptStagger(0.55f);
                    c.ApplyKnockback(c.transform.position - hitPos, 7.5f, 0.18f);
                    if (++chained >= 3) break;
                }
            }
        }

        private static void TriggerOrichalcumNova(GameObject player, Vector3 playerPos, Vector3 center)
        {
            Color col = AlloyPassiveResolver.OrichalcumInfo.AccentColor;
            AlloyPassiveVfx.SpawnShockwaveRing(center, 4.4f, col, 0.42f, 0.30f);
            AlloyPassiveVfx.SpawnPlayerAuraPillar(player.transform, col, 0.85f);
            AlloyPassiveVfx.SpawnSparkBurst(playerPos + Vector3.up * 1.0f, col, Color.white, 30, 0.45f);
            player.GetComponent<PlayerHealthController>()?.Heal(8);
            player.GetComponent<PlayerStaminaController>()?.RestoreStamina(15f);

            var creatures = CaveCreatureBase.ActiveCreatures;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var c = creatures[i];
                if (c == null || !c.IsAlive) continue;
                if ((c.transform.position - center).sqrMagnitude <= 4.6f * 4.6f)
                    AttachStatus(c)?.ApplyAstralSlow(0.60f, 2.8f);
            }
        }

        private static AlloyEnemyStatusRunner AttachStatus(CaveCreatureBase creature)
        {
            if (creature == null || !creature.IsAlive) return null;
            return creature.GetComponent<AlloyEnemyStatusRunner>() ?? creature.gameObject.AddComponent<AlloyEnemyStatusRunner>();
        }
    }

    public sealed class AlloyEnemyStatusRunner : MonoBehaviour
    {
        private CaveCreatureBase _creature;
        private int _bleedTicks;
        private int _bleedDmg;
        private float _bleedInterval, _nextBleedAt, _slowUntil, _staggerUntil, _nextAuraTick;

        private void Awake() => _creature = GetComponent<CaveCreatureBase>();

        public void ApplyCrimsonBleed(int ticks, int dmgPerTick, float interval)
        {
            _bleedTicks = Mathf.Max(_bleedTicks, ticks);
            _bleedDmg = dmgPerTick; _bleedInterval = interval;
            _nextBleedAt = Time.time + 0.35f;
        }

        public void ApplyAstralSlow(float mult, float duration)
        {
            if (_creature == null) return;
            _creature.PassiveSpeedMultiplier = Mathf.Min(_creature.PassiveSpeedMultiplier, mult);
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
        }

        public void ApplyBriefInterruptStagger(float duration)
        {
            if (_creature == null || _creature is TitanBossController) return;
            _creature.InterruptAttackWindupBrief(duration);
            _staggerUntil = Mathf.Max(_staggerUntil, Time.time + duration);
        }

        private void Update()
        {
            if (_creature == null || !_creature.IsAlive) { if (_creature != null) _creature.PassiveSpeedMultiplier = 1f; Destroy(this); return; }

            if (_bleedTicks > 0 && Time.time >= _nextBleedAt)
            {
                _bleedTicks--; _nextBleedAt = Time.time + _bleedInterval;
                _creature.TakeDamage(_bleedDmg);
                AlloyPassiveVfx.SpawnSparkBurst(transform.position + Vector3.up * 0.9f, new Color(0.95f, 0.08f, 0.12f), new Color(0.55f, 0.02f, 0.04f), 14, 0.25f);
            }
            if (Time.time < _slowUntil && Time.time >= _nextAuraTick)
            {
                _nextAuraTick = Time.time + 0.35f;
                AlloyPassiveVfx.SpawnSparkBurst(transform.position + Vector3.up * 0.7f, new Color(1f, 0.84f, 0.24f), Color.white, 8, 0.45f);
            }
            else if (Time.time >= _slowUntil && _creature.PassiveSpeedMultiplier < 0.99f)
                _creature.PassiveSpeedMultiplier = 1f;

            if (_bleedTicks <= 0 && Time.time >= _slowUntil && Time.time >= _staggerUntil)
            {
                _creature.PassiveSpeedMultiplier = 1f;
                Destroy(this);
            }
        }
    }
}
