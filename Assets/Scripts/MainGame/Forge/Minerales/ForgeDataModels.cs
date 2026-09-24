using System;
using UnityEngine;

namespace ForjaGame.Forge
{
    public enum ForgeItemCategory
    {
        RawMaterial, // Raw ores and coal
        Bloom,       // Smelted bloom (furnace output)
        Ingot,       // Pressed ingot (mechanical press output)
        Handle,      // Weapon handle
        Weapon       // Assembled weapon
    }

    public enum WeaponType
    {
        Sword,   // Balanced sword
        Axe,     // Higher damage, slower speed
        Hammer   // Heavy impact, highest damage, slowest speed
    }

    public enum ItemSize
    {
        Small,   // 3 ores: lower damage, higher speed
        Medium,  // 6 ores: balanced base stats
        Large    // 9 ores: higher damage, lower speed
    }

    public enum HandleLength
    {
        Short,   // Shorter reach, higher speed
        Medium,  // Balanced
        Long     // Longer reach, slower speed
    }

    public enum MetalType
    {
        Iron,    // Balanced and sturdy
        Copper,  // Light, fast, lower damage
        Gold     // Malleable, high damage, lower durability
    }

    /// <summary>
    /// Common contract for forge items storable in the player's inventory.
    /// </summary>
    public interface IForgeItem
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        ForgeItemCategory Category { get; }
    }

    [Serializable]
    public class BloomData : IForgeItem
    {
        public string id = Guid.NewGuid().ToString();
        public ItemSize size;
        public float quality = 1f;
        public float resistance = 35f; // Internal resistance (35 to 44)
        public AlloyComposition composition = new();

        public string Id => id;
        public ForgeItemCategory Category => ForgeItemCategory.Bloom;
        public int PurityPercentage => Mathf.RoundToInt(quality * 100f);
        public string PurityRank => PurityPercentage >= 90 ? "Master" : PurityPercentage >= 70 ? "Standard" : PurityPercentage >= 50 ? "Impure" : "Brittle";
        public string PurityColorHex => PurityPercentage >= 90 ? "#FFD700" : PurityPercentage >= 70 ? "#00E676" : PurityPercentage >= 50 ? "#FFB74D" : "#FF5252";
        public string SizeLabel => size switch { ItemSize.Small => "Small (3)", ItemSize.Medium => "Medium (6)", ItemSize.Large => "Large (9)", _ => "Medium" };
        public string DisplayName => composition != null ? composition.GetAlloyName("Bloom") : "Bloom";
        public int DifficultyLevel => Mathf.Clamp(Mathf.RoundToInt(resistance - 34f), 1, 10);
        public string Description => $"Size: {SizeLabel} | Purity: {PurityPercentage}% [{PurityRank}] | Pressing Difficulty: Level {DifficultyLevel} | {composition?.GetCompositionDescription()}";

        public BloomData()
        {
            id = Guid.NewGuid().ToString();
            composition = new AlloyComposition();
        }

        public BloomData(ItemSize size, AlloyComposition composition, float quality = 1f)
        {
            id = Guid.NewGuid().ToString();
            this.size = size;
            this.composition = composition ?? new AlloyComposition();
            this.quality = quality;
            float sizeOffset = size switch { ItemSize.Small => -2.0f, ItemSize.Large => +2.0f, _ => 0f };
            float baseRes = this.composition != null ? this.composition.CalculateWeightedProperties().baseResistance : 40f;
            this.resistance = Mathf.Clamp(baseRes + sizeOffset, 35f, 44f);
        }
    }

    [Serializable]
    public class IngotData : IForgeItem
    {
        public string id = Guid.NewGuid().ToString();
        public string sourceBloomId;
        public ItemSize size;
        public float quality = 1f;
        public AlloyComposition composition = new();

        public string Id => id;
        public ForgeItemCategory Category => ForgeItemCategory.Ingot;
        public int PurityPercentage => Mathf.RoundToInt(quality * 100f);
        public string PurityRank => PurityPercentage >= 90 ? "Master" : PurityPercentage >= 70 ? "Standard" : PurityPercentage >= 50 ? "Impure" : "Brittle";
        public string PurityColorHex => PurityPercentage >= 90 ? "#FFD700" : PurityPercentage >= 70 ? "#00E676" : PurityPercentage >= 50 ? "#FFB74D" : "#FF5252";
        public string SizeLabel => size switch { ItemSize.Small => "Small (3)", ItemSize.Medium => "Medium (6)", ItemSize.Large => "Large (9)", _ => "Medium" };
        public string DisplayName => composition != null ? composition.GetAlloyName("Ingot") : "Ingot";
        public string Description => $"Size: {SizeLabel} | Purity: {PurityPercentage}% [{PurityRank}] | {composition?.GetCompositionDescription()}";

        public IngotData()
        {
            id = Guid.NewGuid().ToString();
            composition = new AlloyComposition();
        }

        public IngotData(BloomData bloom)
        {
            id = Guid.NewGuid().ToString();
            if (bloom != null)
            {
                sourceBloomId = bloom.id;
                size = bloom.size;
                quality = bloom.quality;
                composition = bloom.composition ?? new AlloyComposition();
            }
        }

        public IngotData(ItemSize size, AlloyComposition composition, float quality = 1f)
        {
            id = Guid.NewGuid().ToString();
            this.size = size;
            this.composition = composition ?? new AlloyComposition();
            this.quality = quality;
        }
    }

    [Serializable]
    public class HandleData : IForgeItem
    {
        public string id = Guid.NewGuid().ToString();
        public HandleLength length;
        public string handleName;
        public float rangeModifier;
        public float speedModifier;

        public string Id => id;
        public string DisplayName => handleName;
        public ForgeItemCategory Category => ForgeItemCategory.Handle;
        public string Description => $"Range: x{rangeModifier:F2} | Speed: {(speedModifier >= 0 ? "+" : "")}{speedModifier * 100:F0}%";

        public static HandleData CreateDefault() =>
            new HandleData { length = HandleLength.Medium, handleName = "Mango de Madera", rangeModifier = 1.0f, speedModifier = 0.0f };

        public static HandleData Create(HandleLength length)
        {
            return length switch
            {
                HandleLength.Short => new HandleData { length = HandleLength.Short, handleName = "Short Handle", rangeModifier = 0.80f, speedModifier = +0.15f },
                HandleLength.Medium => new HandleData { length = HandleLength.Medium, handleName = "Medium Handle", rangeModifier = 1.0f, speedModifier = 0.0f },
                HandleLength.Long => new HandleData { length = HandleLength.Long, handleName = "Long Handle", rangeModifier = 1.35f, speedModifier = -0.20f },
                _ => new HandleData { length = HandleLength.Medium, handleName = "Medium Handle", rangeModifier = 1.0f, speedModifier = 0.0f }
            };
        }
    }

    [Serializable]
    public class HandleGroupData : IForgeItem
    {
        public HandleData Handle { get; }
        public int Count { get; }

        public HandleGroupData(HandleData handle, int count)
        {
            Handle = handle;
            Count = count;
        }

        public string Id => Handle?.Id ?? string.Empty;
        public string DisplayName => Count > 1 ? $"{Handle?.DisplayName} x{Count}" : Handle?.DisplayName;
        public ForgeItemCategory Category => ForgeItemCategory.Handle;
        public string Description => Handle?.Description ?? string.Empty;
    }

    public static class RawMaterialCatalog
    {
        public static bool IsRawMaterial(string itemId) =>
            itemId == "Iron" || itemId == "Copper" || itemId == "Gold" || itemId == "Coal";

        public static string GetDisplayName(string itemId) => itemId switch
        {
            "Iron" => "Iron Ore",
            "Copper" => "Copper Ore",
            "Gold" => "Gold Ore",
            "Coal" => "Coal",
            _ => itemId
        };

        public static string GetColorHex(string itemId) => itemId switch
        {
            "Iron" => "#CCCCCC",
            "Copper" => "#E07A5F",
            "Gold" => "#FFD700",
            "Coal" => "#4B5563",
            _ => "#FFFFFF"
        };
    }
}
