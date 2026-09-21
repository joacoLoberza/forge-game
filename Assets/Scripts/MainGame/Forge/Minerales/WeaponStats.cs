using System;
using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.Forge
{
    public enum WeaponCombatStance
    {
        OneHanded,
        TwoHanded,
        Polearm
    }

    public enum WeaponAnimationStyle
    {
        Unarmed,
        Sword1H,
        Axe1H,
        Hammer1H,
        Heavy2H,
        Polearm
    }

    [Serializable]
    public class WeaponStats : IForgeItem
    {
        public string id = Guid.NewGuid().ToString();
        public string weaponName;
        public WeaponType weaponType;
        public ItemSize ingotSize;
        public HandleLength handleLength;
        public AlloyComposition composition = new();
        [NonSerialized] public Mesh customMesh;
        [NonSerialized] public Mesh customHandleMesh;
        [NonSerialized] public Material customBladeMaterial;
        [NonSerialized] public Material customHandleMaterial;
        public Vector3 bladeMountPosition;
        public Quaternion bladeMountRotation = Quaternion.identity;
        public Vector3 gripCenterOffset;
        public bool isFrontFaceDominant = true;
        public Vector3[] vertexPositions;

        public float baseDamage;
        public float attackSpeed;
        public float attackRange;
        public float maxSharpness;
        public float currentSharpness;
        public float quality;
        public string qualityPrefix = "Standard";

        [SerializeField] private WeaponProfile _profile;
        public WeaponProfile Profile => _profile.mass > 0.01f ? _profile : CalculateProfile();

        public WeaponProfile CalculateProfile(ProceduralCombatConfigSO config = null)
        {
            // 1. Longitud del mango en metros
            float hLen = 0.50f;
            if (customHandleMesh != null && customHandleMesh.bounds.size.x > 0.05f)
            {
                hLen = customHandleMesh.bounds.size.x;
            }
            else
            {
                hLen = handleLength switch
                {
                    HandleLength.Short => 0.35f,
                    HandleLength.Medium => 0.70f,
                    HandleLength.Long => 1.45f,
                    _ => 0.55f
                };
            }

            // 2. Dimensiones volumétricas de la cabeza metálica
            float headL = 0.80f, headW = 0.06f, headT = 0.015f, latExt = 0.02f;
            if (customMesh != null && customMesh.bounds.size.sqrMagnitude > 0.001f)
            {
                var b = customMesh.bounds;
                headL = Mathf.Max(0.10f, b.size.x);
                headW = Mathf.Max(0.02f, b.size.z);
                headT = Mathf.Max(0.008f, b.size.y);
                latExt = Mathf.Abs(b.center.z) / Mathf.Max(0.01f, b.size.z);
            }
            else
            {
                switch (weaponType)
                {
                    case WeaponType.Axe:
                        headL = 0.32f; headW = 0.22f; headT = 0.04f; latExt = 0.35f;
                        break;
                    case WeaponType.Hammer:
                        headL = 0.26f; headW = 0.18f; headT = 0.16f; latExt = 0.05f;
                        break;
                    default:
                        headL = 0.85f; headW = 0.06f; headT = 0.015f; latExt = 0.02f;
                        break;
                }
            }

            // 3. Filo (0..1)
            float sh = maxSharpness > 0.01f ? Mathf.Clamp01(currentSharpness / maxSharpness) : 0.65f;
            if (weaponType == WeaponType.Hammer) sh = 0.10f;
            else if (sh < 0.2f && quality >= 0.5f) sh = 0.75f;

            // 4. Masa total en kg
            float headMass = ingotSize switch
            {
                ItemSize.Small => 1.2f,
                ItemSize.Medium => 2.8f,
                ItemSize.Large => 5.8f,
                _ => 2.5f
            };
            float handleMass = handleLength switch
            {
                HandleLength.Short => 0.25f,
                HandleLength.Medium => 0.65f,
                HandleLength.Long => 1.40f,
                _ => 0.55f
            };
            float totalMass = headMass + handleMass;

            _profile = WeaponProfile.Calculate(hLen, headL, headW, headT, sh, latExt, totalMass, config);
            return _profile;
        }

        public WeaponAnimationStyle GetAnimationStyle()
        {
            // Lingote Grande (9 minerales): Masa pesada que siempre requiere dos manos
            if (ingotSize == ItemSize.Large)
                return WeaponAnimationStyle.Heavy2H;

            // Mango Largo: Hachas y martillos largos usan mandoble (Heavy2H); espadas largas usan Polearm (naginata)
            if (handleLength == HandleLength.Long)
            {
                if (weaponType == WeaponType.Sword) return WeaponAnimationStyle.Polearm;
                return WeaponAnimationStyle.Heavy2H;
            }

            // Armas estándar de una mano (Short y Medium handle con lingote Small o Medium):
            return weaponType switch
            {
                WeaponType.Axe => WeaponAnimationStyle.Axe1H,
                WeaponType.Hammer => WeaponAnimationStyle.Hammer1H,
                _ => WeaponAnimationStyle.Sword1H
            };
        }

        public WeaponCombatStance GetCombatStance()
        {
            var style = GetAnimationStyle();
            return style switch
            {
                WeaponAnimationStyle.Polearm => WeaponCombatStance.Polearm,
                WeaponAnimationStyle.Heavy2H => WeaponCombatStance.TwoHanded,
                _ => WeaponCombatStance.OneHanded
            };
        }

        public float GetNormalizedWeight()
        {
            float headVal = ingotSize switch
            {
                ItemSize.Small => 0.20f,
                ItemSize.Medium => 0.55f,
                ItemSize.Large => 0.95f,
                _ => 0.50f
            };

            float handleVal = handleLength switch
            {
                HandleLength.Short => 0.25f,
                HandleLength.Medium => 0.50f,
                HandleLength.Long => 0.90f,
                _ => 0.50f
            };

            float typeBonus = weaponType switch
            {
                WeaponType.Hammer => 0.15f,
                WeaponType.Axe => 0.08f,
                _ => 0.0f
            };

            return Mathf.Clamp01(headVal * 0.60f + handleVal * 0.40f + typeBonus);
        }

        public float GetSpeedMultiplier()
        {
            float weight = GetNormalizedWeight();
            return Mathf.Clamp(1.35f - (weight * 0.60f), 0.70f, 1.30f);
        }

        public int MaxComboCount => 3;

        public float GetBaseAttackDuration() => GetAnimationStyle() switch
        {
            WeaponAnimationStyle.Sword1H => 0.70f,
            WeaponAnimationStyle.Axe1H => 0.80f,
            WeaponAnimationStyle.Hammer1H => 0.90f,
            WeaponAnimationStyle.Heavy2H => 1.15f,
            WeaponAnimationStyle.Polearm => 0.85f,
            _ => 0.80f
        };

        public float GetComboLinkRatio() => GetAnimationStyle() switch
        {
            WeaponAnimationStyle.Sword1H => 0.58f,
            WeaponAnimationStyle.Axe1H => 0.62f,
            WeaponAnimationStyle.Hammer1H => 0.65f,
            WeaponAnimationStyle.Heavy2H => 0.68f,
            WeaponAnimationStyle.Polearm => 0.60f,
            _ => 0.62f
        };

        public float damage { get => baseDamage; set => baseDamage = value; }
        public float durability { get => maxSharpness; set => maxSharpness = value; }
        public float currentDurability { get => currentSharpness; set => currentSharpness = value; }

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(qualityPrefix) ? weaponName : $"{weaponName} ({qualityPrefix})";
        public ForgeItemCategory Category => ForgeItemCategory.Weapon;
        public int PurityPercentage => Mathf.RoundToInt(quality * 100f);

        public float SharpnessRatio => maxSharpness > 0f ? Mathf.Clamp01(currentSharpness / maxSharpness) : 0f;
        public float EffectiveDamage => baseDamage * Mathf.Lerp(0.60f, 1.00f, SharpnessRatio);
        public bool IsDull => currentSharpness <= 0f;

        public void ConsumeSharpness(float amount = 1f) => currentSharpness = Mathf.Max(0f, currentSharpness - amount);
        public void RestoreSharpness(float amount) => currentSharpness = Mathf.Min(maxSharpness, currentSharpness + amount);

        public string Description => $"Damage: {EffectiveDamage:F1} (Base: {baseDamage:F1}) | Speed: {attackSpeed:F2} | Range: {attackRange:F2}m | Sharpness: {currentSharpness:F0}/{maxSharpness:F0} ({SharpnessRatio * 100:F0}%) | Purity: {PurityPercentage}%";
        public override string ToString() => $"{DisplayName} [{ingotSize}|{handleLength}] -> Dmg: {EffectiveDamage:F1}, Spd: {attackSpeed:F2}, Rng: {attackRange:F2}m, Shp: {currentSharpness:F0}/{maxSharpness:F0}";
    }
}
