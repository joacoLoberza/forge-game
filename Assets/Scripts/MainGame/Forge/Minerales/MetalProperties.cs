using System;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Extensible contract for furnace fuel sources (Open/Closed Principle).
    /// </summary>
    public interface IFuelSource
    {
        string FuelName { get; }
        float HeatPower { get; }
    }

    [Serializable]
    public class CoalFuel : IFuelSource
    {
        public string FuelName => "Coal";
        public float HeatPower => 150f; // °C added per coal unit
    }

    [Serializable]
    public struct MetalProperties
    {
        public MetalType metalType;
        public float minOptimumTemp;   // Minimum recommended temperature in °C
        public float maxOptimumTemp;   // Maximum recommended temperature in °C
        public float smeltDuration;    // Smelting duration required inside range (seconds)
        public float baseDamage;       // Base damage modifier
        public float baseAttackSpeed;  // Base attack speed modifier
        public float baseSharpness;    // Base sharpness / durability
        public float baseResistance;   // Mechanical resistance to press compaction
        public float baseDurability => baseSharpness; // Backward compatibility

        public static Color GetMetalColor(MetalType type)
        {
            return type switch
            {
                MetalType.Iron => new Color(0.88f, 0.92f, 0.96f, 1f),   // Bright silvery gray
                MetalType.Copper => new Color(0.95f, 0.52f, 0.28f, 1f), // Vibrant copper orange
                MetalType.Gold => new Color(1.00f, 0.85f, 0.20f, 1f),   // Golden yellow
                _ => Color.white
            };
        }

        public static MetalProperties GetDefault(MetalType type)
        {
            return type switch
            {
                // Iron: Hardest and sturdy (Level 6 to 10 depending on size, max 44)
                MetalType.Iron => new MetalProperties
                {
                    metalType = MetalType.Iron,
                    minOptimumTemp = 750f,
                    maxOptimumTemp = 1100f,
                    smeltDuration = 12f,
                    baseDamage = 20f,
                    baseAttackSpeed = 1.0f,
                    baseSharpness = 100f,
                    baseResistance = 42.0f
                },
                // Copper: Malleable and easiest to press (Level 1 to 5 depending on size, min 35)
                MetalType.Copper => new MetalProperties
                {
                    metalType = MetalType.Copper,
                    minOptimumTemp = 550f,
                    maxOptimumTemp = 950f,
                    smeltDuration = 9f,
                    baseDamage = 14f,
                    baseAttackSpeed = 1.25f,
                    baseSharpness = 75f,
                    baseResistance = 37.0f
                },
                // Gold: Intermediate hardness (Level 4 to 8 depending on size)
                MetalType.Gold => new MetalProperties
                {
                    metalType = MetalType.Gold,
                    minOptimumTemp = 650f,
                    maxOptimumTemp = 1000f,
                    smeltDuration = 10f,
                    baseDamage = 26f,
                    baseAttackSpeed = 1.05f,
                    baseSharpness = 50f,
                    baseResistance = 40.0f
                },
                _ => new MetalProperties
                {
                    metalType = MetalType.Iron,
                    minOptimumTemp = 750f,
                    maxOptimumTemp = 1100f,
                    smeltDuration = 12f,
                    baseDamage = 20f,
                    baseAttackSpeed = 1.0f,
                    baseSharpness = 100f,
                    baseResistance = 42.0f
                }
            };
        }
    }
}
