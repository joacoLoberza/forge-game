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
        public bool isPrebuiltModel = false;
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
                HandleLength.Short => 0.56f,
                HandleLength.Medium => 0.80f,
                HandleLength.Long => 1.45f,
                _ => 0.80f
            };
            if (customHandleMesh != null && customHandleMesh.bounds.size.x > 0.05f)
                hLen = customHandleMesh.bounds.size.x;

            float headL = 0.80f, headW = 0.06f, headT = 0.015f, latExt = 0.02f;
            if (customMesh != null && customMesh.bounds.size.sqrMagnitude > 0.001f)
            {
                Bounds rb = ComputeRotatedMeshBounds(customMesh, bladeMountRotation);
                headL = Mathf.Max(0.10f, rb.size.x);
                headW = Mathf.Max(0.02f, rb.size.z);
                headT = Mathf.Max(0.008f, rb.size.y);
                float latOffset = Mathf.Max(Mathf.Abs(rb.center.z), Mathf.Abs(bladeMountPosition.z));
                latExt = Mathf.Max(weaponType == WeaponType.Axe ? 0.22f : 0f, latOffset / Mathf.Max(0.05f, headW));
            }
            else
            {
                switch (weaponType)
                {
                    case WeaponType.Axe: headL = 0.26f; headW = 0.22f; headT = 0.04f; latExt = 0.35f; break;
                    case WeaponType.Hammer: headL = 0.24f; headW = 0.18f; headT = 0.16f; latExt = 0.05f; break;
                    default: headL = 0.85f; headW = 0.06f; headT = 0.015f; latExt = 0.02f; break;
                }
            }

            float sh = maxSharpness > 0.01f ? Mathf.Clamp01(currentSharpness / maxSharpness) : 0.65f;
            if (weaponType == WeaponType.Hammer) sh = 0.10f;
            else if (sh < 0.2f && quality >= 0.5f) sh = 0.75f;

            float headMass = ingotSize switch { ItemSize.Small => 1.25f, ItemSize.Medium => 2.85f, ItemSize.Large => 5.60f, _ => 2.4f };
            float handleMass = Mathf.Clamp(hLen * 0.72f, 0.22f, 1.35f);
            _profile = WeaponProfile.Calculate(hLen, headL, headW, headT, sh, latExt, headMass + handleMass, weaponType);
            return _profile;
        }

        public static Bounds ComputeRotatedMeshBounds(Mesh mesh, Quaternion rot)
        {
            var verts = mesh.vertices;
            if (verts == null || verts.Length == 0) return mesh.bounds;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = rot * verts[i];
                if (p.x < min.x) min.x = p.x; if (p.x > max.x) max.x = p.x;
                if (p.y < min.y) min.y = p.y; if (p.y > max.y) max.y = p.y;
                if (p.z < min.z) min.z = p.z; if (p.z > max.z) max.z = p.z;
            }
            Bounds b = new Bounds(); b.SetMinMax(min, max); return b;
        }

        /// <summary>
        /// Determina el estilo de animación real según la morfología forjada:
        /// - Lingote pequeño + mango corto o medio -> siempre arma LIGERA de 1 MANO (Axe1H, Hammer1H, Sword1H/Dagger1H).
        /// - Mango largo -> Asta / Alabarda (o Pesada 2H si la cabeza también es pesada).
        /// - Lingote grande (o mediano muy pesado de asta/cabeza pesada) -> Heavy2H.
        /// </summary>
        public WeaponAnimationStyle GetAnimationStyle()
        {
            if (weaponType == WeaponType.Pickaxe) return WeaponAnimationStyle.Hammer1H;
            if (handleLength == HandleLength.Long || (ingotSize == ItemSize.Large && handleLength != HandleLength.Short))
                return WeaponAnimationStyle.Heavy2H;
            return weaponType switch
            {
                WeaponType.Axe => WeaponAnimationStyle.Axe1H,
                WeaponType.Hammer => WeaponAnimationStyle.Hammer1H,
                WeaponType.Sword when handleLength == HandleLength.Short => WeaponAnimationStyle.Dagger1H,
                _ => WeaponAnimationStyle.Sword1H
            };
        }

        public WeaponCombatStance GetCombatStance()
        {
            if (weaponType == WeaponType.Pickaxe || handleLength == HandleLength.Long || GetAnimationStyle() == WeaponAnimationStyle.Heavy2H)
                return WeaponCombatStance.TwoHanded;
            return WeaponCombatStance.OneHanded;
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

        public float GetPassiveBlockDamageMultiplier()
        {
            float headVal = ingotSize switch { ItemSize.Small => 0f, ItemSize.Medium => 0.5f, ItemSize.Large => 1f, _ => 0.4f };
            float handleVal = handleLength switch { HandleLength.Short => 0f, HandleLength.Medium => 0.5f, HandleLength.Long => 1f, _ => 0.4f };
            float sizeFactor = Mathf.Clamp01(headVal * 0.65f + handleVal * 0.35f);
            return Mathf.Lerp(0.68f, 0.36f, sizeFactor);
        }

        public int MaxComboCount => GetAnimationStyle() == WeaponAnimationStyle.Unarmed ? 3 : 5;

        public float GetBaseAttackDuration()
        {
            if (weaponType == WeaponType.Pickaxe) return 0.80f;
            return handleLength switch { HandleLength.Short => 0.82f, HandleLength.Long => 1.24f, _ => 1.02f };
        }

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
        public string Description => $"Dmg: {EffectiveDamage:F1} (Base: {baseDamage:F1}) | Spd: {attackSpeed:F2} | Reach: {attackRange:F2}m | Edge: {currentSharpness:F0}/{maxSharpness:F0} ({SharpnessRatio * 100:F0}%) | Purity: {PurityPercentage}%";
        public override string ToString() => $"{DisplayName} [{ingotSize}|{handleLength}] -> Dmg: {EffectiveDamage:F1}, Spd: {attackSpeed:F2}, Rng: {attackRange:F2}m";
    }
}
