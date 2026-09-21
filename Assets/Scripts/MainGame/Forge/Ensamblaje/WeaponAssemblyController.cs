using System;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Weapon assembly controller: calculates final stats combining
    /// ingot alloy composition, ingot size, weapon type, handle length,
    /// and preserves the player's custom forged 3D blade mesh.
    /// </summary>
    public class WeaponAssemblyController : MonoBehaviour
    {
        public WeaponStats AssembleWeapon(IngotData ingot, WeaponType weaponType, HandleData handle, Mesh customBladeMesh = null, float massRatio = 1.0f)
        {
            if (ingot == null)
            {
                Debug.LogError("Cannot assemble a weapon without an ingot.");
                return null;
            }

            handle ??= HandleData.Create(HandleLength.Medium);
            MetalProperties alloyProps = ingot.composition.CalculateWeightedProperties();

            (float typeDmgMult, float typeSpeedMult, float typeBaseRange) = weaponType switch
            {
                WeaponType.Sword => (1.0f, 1.0f, 1.25f),
                WeaponType.Axe => (1.35f, 0.85f, 1.10f),
                WeaponType.Hammer => (1.75f, 0.65f, 1.35f),
                _ => (1.0f, 1.0f, 1.25f)
            };

            (float baseSizeDmgMult, float baseSizeSpeedMod, float sizeDurabilityMult) = ingot.size switch
            {
                ItemSize.Small => (0.80f, +0.15f, 0.80f),
                ItemSize.Medium => (1.00f, 0.00f, 1.00f),
                ItemSize.Large => (1.35f, -0.20f, 1.40f),
                _ => (1.00f, 0.00f, 1.00f)
            };

            // Masa final adaptada por cortes y pulido (reducir material disminuye peso: menos daño pero mayor agilidad)
            float clampedMass = Mathf.Clamp(massRatio, 0.20f, 1.0f);
            float effectiveMass = baseSizeDmgMult * clampedMass;
            float sizeSpeedModifier = baseSizeSpeedMod + ((1.0f - clampedMass) * 0.30f);

            float quality = ingot.quality;
            (string qualityPrefix, float purityDmgMult, float puritySharpnessMult) = quality switch
            {
                >= 0.90f => ("Master", 1.15f, 1.20f),
                >= 0.70f => ("Standard", 1.00f, 1.00f),
                >= 0.50f => ("Impure", 0.80f, 0.65f),
                _ => ("Brittle", 0.60f, 0.40f)
            };

            float finalDamage = alloyProps.baseDamage * typeDmgMult * effectiveMass * purityDmgMult;
            float finalAttackSpeed = Mathf.Max(0.2f, (alloyProps.baseAttackSpeed * typeSpeedMult) + sizeSpeedModifier + handle.speedModifier);
            float finalRange = typeBaseRange * handle.rangeModifier;
            float finalSharpness = alloyProps.baseSharpness * sizeDurabilityMult * puritySharpnessMult;

            string weaponTypeName = weaponType switch
            {
                WeaponType.Sword => "Sword",
                WeaponType.Axe => "Axe",
                WeaponType.Hammer => "Hammer",
                _ => "Weapon"
            };

            string baseName = ingot.composition.GetAlloyName(weaponTypeName);
            string finalName = $"{baseName} ({qualityPrefix})";

            var stats = new WeaponStats
            {
                weaponName = finalName,
                weaponType = weaponType,
                ingotSize = ingot.size,
                handleLength = handle.length,
                composition = ingot.composition,
                customMesh = customBladeMesh,
                vertexPositions = customBladeMesh != null ? (Vector3[])customBladeMesh.vertices.Clone() : null,
                baseDamage = finalDamage,
                attackSpeed = finalAttackSpeed,
                attackRange = finalRange,
                maxSharpness = finalSharpness,
                currentSharpness = finalSharpness,
                quality = ingot.quality,
                qualityPrefix = qualityPrefix
            };

            var profile = stats.CalculateProfile();
            Debug.Log($"<color=#33FFAA><b>[WeaponProfile] {profile}</b></color>");
            return stats;
        }
    }
}
