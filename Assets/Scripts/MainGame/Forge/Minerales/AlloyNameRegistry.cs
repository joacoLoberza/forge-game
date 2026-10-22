using System.Collections.Generic;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Registry of historical and lore material names for metal combinations (SRP).
    /// Open for extension as new alloy types are introduced.
    /// </summary>
    public static class AlloyNameRegistry
    {
        public static string ResolveAlloyMaterialName(List<AlloyIngredient> ingredients)
        {
            if (ingredients == null || ingredients.Count == 0)
                return "Metal";

            if (ingredients.Count == 1)
            {
                return ingredients[0].metal switch
                {
                    MetalType.Iron => "Iron",
                    MetalType.Copper => "Copper",
                    MetalType.Gold => "Gold",
                    _ => ingredients[0].metal.ToString()
                };
            }

            bool hasIron = ingredients.Exists(i => i.metal == MetalType.Iron);
            bool hasCopper = ingredients.Exists(i => i.metal == MetalType.Copper);
            bool hasGold = ingredients.Exists(i => i.metal == MetalType.Gold);

            // Special names depending on the metal combination
            if (hasIron && hasGold && hasCopper)
                return "Orichalcum";

            if (hasIron && hasGold)
                return "Blue Gold";

            if (hasCopper && hasGold)
                return "Rose Gold";

            if (hasIron && hasCopper)
                return "Ferric Bronze";

            return $"Alloy ({ingredients[0].metal}-{ingredients[1].metal})";
        }
    }
}
