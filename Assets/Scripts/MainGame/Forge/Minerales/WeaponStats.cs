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
        Dagger1H,
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
        public void SetProfile(WeaponProfile profile) => _profile = profile;

        public WeaponProfile CalculateProfile()
        {
            float hLen = handleLength switch
            {
                HandleLength.Short => 0.38f,
                HandleLength.Medium => 0.78f,
                HandleLength.Long => 1.38f,
                _ => 0.55f
            };
            if (customHandleMesh != null && customHandleMesh.bounds.size.x > 0.05f)
                hLen = customHandleMesh.bounds.size.x;

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
                    case WeaponType.Axe: headL = 0.32f; headW = 0.22f; headT = 0.04f; latExt = 0.35f; break;
                    case WeaponType.Hammer: headL = 0.26f; headW = 0.18f; headT = 0.16f; latExt = 0.05f; break;
                    default: headL = 0.85f; headW = 0.06f; headT = 0.015f; latExt = 0.02f; break;
                }
            }

            float sh = maxSharpness > 0.01f ? Mathf.Clamp01(currentSharpness / maxSharpness) : 0.65f;
            if (weaponType == WeaponType.Hammer) sh = 0.10f;
            else if (sh < 0.2f && quality >= 0.5f) sh = 0.75f;

            float headMass = ingotSize switch { ItemSize.Small => 1.2f, ItemSize.Medium => 2.9f, ItemSize.Large => 5.8f, _ => 2.5f };
            float handleMass = Mathf.Clamp(hLen * 0.95f, 0.22f, 1.55f);
            _profile = WeaponProfile.Calculate(hLen, headL, headW, headT, sh, latExt, headMass + handleMass);
            return _profile;
        }

        /// <summary>
        /// Determina el estilo de animación real según la morfología forjada (longitud real del mango,
        /// masa de la cabeza y tipo de arma), diferenciando hachas/martillos ligeros 1H de pesados 2H y alabardas.
        /// </summary>
        public WeaponAnimationStyle GetAnimationStyle()
        {
            if (weaponType == WeaponType.Pickaxe)
                return WeaponAnimationStyle.Hammer1H;

            WeaponProfile prof = Profile;

            // Asta / Alabarda / Lanza de hoja (mangos muy largos con cabeza ligera o mediana)
            if (prof.grip == GripMode.Pole && ingotSize != ItemSize.Large)
                return WeaponAnimationStyle.Polearm;
            if (handleLength == HandleLength.Long && weaponType == WeaponType.Sword && ingotSize != ItemSize.Large)
                return WeaponAnimationStyle.Polearm;

            // Agarre pesado a DOS MANOS: Lingote Grande, Mango Largo, perfil TwoHand,
            // o Hachas/Martillos de cabeza mediana con mango mediano/largo (Hacha de Batalla / Martillo de Guerra 2H)
            bool isHeavyHeadedAxeOrHammer = (weaponType == WeaponType.Axe || weaponType == WeaponType.Hammer)
                                            && handleLength != HandleLength.Short
                                            && (ingotSize != ItemSize.Small || prof.handleLength >= 0.78f);

            if (ingotSize == ItemSize.Large || handleLength == HandleLength.Long ||
                prof.grip == GripMode.TwoHand || prof.grip == GripMode.Pole ||
                isHeavyHeadedAxeOrHammer)
            {
                return WeaponAnimationStyle.Heavy2H;
            }

            // Armas ágiles de UNA MANO (Hachuelas cortas, Mazas compactas, Espadas 1H y Dagas)
            return weaponType switch
            {
                WeaponType.Axe => WeaponAnimationStyle.Axe1H,
                WeaponType.Hammer => WeaponAnimationStyle.Hammer1H,
                WeaponType.Sword when handleLength == HandleLength.Short || prof.grip == GripMode.OneShort => WeaponAnimationStyle.Dagger1H,
                _ => WeaponAnimationStyle.Sword1H
            };
        }

        public WeaponCombatStance GetCombatStance()
        {
            return GetAnimationStyle() switch
            {
                WeaponAnimationStyle.Polearm => WeaponCombatStance.Polearm,
                WeaponAnimationStyle.Heavy2H => WeaponCombatStance.TwoHanded,
                _ => WeaponCombatStance.OneHanded
            };
        }

        public float GetNormalizedWeight()
        {
            float headVal = ingotSize switch { ItemSize.Small => 0.20f, ItemSize.Medium => 0.55f, ItemSize.Large => 0.95f, _ => 0.50f };
            float handleVal = handleLength switch { HandleLength.Short => 0.25f, HandleLength.Medium => 0.50f, HandleLength.Long => 0.90f, _ => 0.50f };
            float typeBonus = weaponType switch { WeaponType.Hammer or WeaponType.Pickaxe => 0.18f, WeaponType.Axe => 0.10f, _ => 0.0f };

            float mineralDensityMult = 1.0f;
            if (composition != null && composition.ingredients != null && composition.ingredients.Count > 0)
            {
                float mineralSpeed = composition.CalculateWeightedProperties().baseAttackSpeed;
                mineralDensityMult = Mathf.Clamp(1.0f / Mathf.Max(0.5f, mineralSpeed), 0.75f, 1.35f);
            }
            return Mathf.Clamp01((headVal * 0.60f + handleVal * 0.40f + typeBonus) * mineralDensityMult);
        }

        public float GetSpeedMultiplier()
        {
            float weight = GetNormalizedWeight();
            float mineralSpeed = composition != null ? composition.CalculateWeightedProperties().baseAttackSpeed : 1.0f;
            float handleLengthRatio = Mathf.InverseLerp((float)HandleLength.Short, (float)HandleLength.Long, (float)handleLength);
            float handleLengthPenalty = Mathf.Lerp(1.0f, 0.78f, handleLengthRatio);
            return Mathf.Clamp((1.42f - (weight * 0.72f)) * handleLengthPenalty * mineralSpeed, 0.58f, 1.65f);
        }

        public int MaxComboCount => GetAnimationStyle() == WeaponAnimationStyle.Unarmed ? 3 : 5;

        public float GetBaseAttackDuration() => GetAnimationStyle() switch
        {
            WeaponAnimationStyle.Dagger1H => 0.65f, WeaponAnimationStyle.Sword1H => 1.05f, WeaponAnimationStyle.Axe1H => 1.05f,
            WeaponAnimationStyle.Hammer1H => 1.10f, WeaponAnimationStyle.Heavy2H => 1.45f,
            WeaponAnimationStyle.Polearm => 1.30f, _ => 1.05f
        };

        public float GetComboLinkRatio() => GetAnimationStyle() switch
        {
            WeaponAnimationStyle.Dagger1H => 0.82f, WeaponAnimationStyle.Hammer1H or WeaponAnimationStyle.Heavy2H => 0.86f, _ => 0.85f
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
        public string Description => $"Daño: {EffectiveDamage:F1} (Base: {baseDamage:F1}) | Vel: {attackSpeed:F2} | Alcance: {attackRange:F2}m | Filo: {currentSharpness:F0}/{maxSharpness:F0} ({SharpnessRatio * 100:F0}%) | Pureza: {PurityPercentage}%";
        public override string ToString() => $"{DisplayName} [{ingotSize}|{handleLength}] -> Dmg: {EffectiveDamage:F1}, Spd: {attackSpeed:F2}, Rng: {attackRange:F2}m";
    }
}
