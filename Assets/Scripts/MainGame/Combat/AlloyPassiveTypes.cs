using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public enum AlloyPassiveKind
    {
        None = 0,
        FerricBronze = 1,
        RoseGold = 2,
        BlueGold = 3,
        Orichalcum = 4
    }

    public readonly struct AlloyPassiveInfo
    {
        public readonly AlloyPassiveKind Kind;
        public readonly string PassiveName;
        public readonly string Summary;
        public readonly Color AccentColor;
        public readonly string ColorHex;

        public AlloyPassiveInfo(AlloyPassiveKind kind, string name, string summary, Color col, string hex)
        {
            Kind = kind; PassiveName = name; Summary = summary; AccentColor = col; ColorHex = hex;
        }
    }

    /// <summary>
    /// Resuelve qué pasiva táctica secundaria corresponde a la aleación forjada del arma
    /// sin inflar su daño o velocidad base (que provienen estrictamente de los minerales y su calidad).
    /// </summary>
    public static class AlloyPassiveResolver
    {
        public static readonly AlloyPassiveInfo NoneInfo = new(
            AlloyPassiveKind.None, "Pure Temper", "Pure metal: relies strictly on mineral craft & proportions.", new Color(0.85f, 0.85f, 0.88f), "#5C4632");

        public static readonly AlloyPassiveInfo FerricBronzeInfo = new(
            AlloyPassiveKind.FerricBronze,
            "Bastion Resonance",
            "Zero edge wear vs stone/armor; resists guard-break & unleashes a Bronze Shockwave when blocking or bashing.",
            new Color(0.92f, 0.56f, 0.24f), "#B45309");

        public static readonly AlloyPassiveInfo RoseGoldInfo = new(
            AlloyPassiveKind.RoseGold,
            "Crimson Surge",
            "Every hit restores +12 Stamina; 3rd hit, Finishers & Backstabs carve a Crimson Arc causing Bleed (21 DMG).",
            new Color(0.96f, 0.30f, 0.38f), "#BE123C");

        public static readonly AlloyPassiveInfo BlueGoldInfo = new(
            AlloyPassiveKind.BlueGold,
            "Cobalt Tremor",
            "Finishers, Slams & Backstabs detonate Cobalt Lightning that interrupts windups, arcs to nearby foes & cracks walls.",
            new Color(0.32f, 0.72f, 1.00f), "#1D4ED8");

        public static readonly AlloyPassiveInfo OrichalcumInfo = new(
            AlloyPassiveKind.Orichalcum,
            "Astral Resonance",
            "Finishers & Timed Parries unleash a Golden Nova: heals +8 HP, restores +15 Stamina & slows nearby foes 40%.",
            new Color(1.00f, 0.82f, 0.22f), "#B4860B");

        public static AlloyPassiveInfo Resolve(WeaponStats weapon)
        {
            if (weapon == null || weapon.weaponType == WeaponType.Pickaxe || weapon.composition?.ingredients == null)
                return NoneInfo;
            return ResolveByAlloyName(AlloyNameRegistry.ResolveAlloyMaterialName(weapon.composition.ingredients));
        }

        public static AlloyPassiveInfo ResolveByAlloyName(string alloyName) => alloyName switch
        {
            "Ferric Bronze" => FerricBronzeInfo,
            "Rose Gold"     => RoseGoldInfo,
            "Blue Gold"     => BlueGoldInfo,
            "Orichalcum"    => OrichalcumInfo,
            _               => NoneInfo
        };
    }
}

