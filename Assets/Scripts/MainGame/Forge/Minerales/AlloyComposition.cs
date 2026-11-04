using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ForjaGame.Forge
{
    [Serializable]
    public struct AlloyIngredient
    {
        public MetalType metal;
        public int count;
        public int percentage; // Integer in multiples of 5 (e.g. 65, 35)
    }

    /// <summary>
    /// Manages ore percentage composition (multiples of 5 summing to 100%)
    /// and calculates weighted average properties (SRP).
    /// </summary>
    [Serializable]
    public class AlloyComposition
    {
        public List<AlloyIngredient> ingredients = new();
        public int totalOres;
        public int impurityPercentage = 0; // Accumulated impurities from improper smelting temps

        public static AlloyComposition FromOres(Dictionary<MetalType, int> oreCounts, int impurity = 0, MetalType? firstMetal = null, Dictionary<MetalType, int> deductionsPerMetal = null)
        {
            AlloyComposition comp = new AlloyComposition();
            int total = 0;
            if (oreCounts != null)
            {
                foreach (var kvp in oreCounts) total += kvp.Value;
                comp.totalOres = total;

                if (total > 0)
                {
                    List<KeyValuePair<MetalType, int>> activeOres = new();
                    foreach (var kvp in oreCounts)
                    {
                        if (kvp.Value > 0) activeOres.Add(kvp);
                    }

                    activeOres.Sort((a, b) => b.Value.CompareTo(a.Value));

                    // Calculate percentages rounded to multiples of 5 summing to 100%
                    int allocatedPercentage = 0;
                    for (int i = 0; i < activeOres.Count; i++)
                    {
                        int pct;
                        if (i == activeOres.Count - 1)
                        {
                            pct = Mathf.Max(5, 100 - allocatedPercentage);
                        }
                        else
                        {
                            float rawPct = ((float)activeOres[i].Value / total) * 100f;
                            pct = Mathf.RoundToInt(rawPct / 5f) * 5;
                            pct = Mathf.Clamp(pct, 5, 95);
                        }

                        allocatedPercentage += pct;
                        comp.ingredients.Add(new AlloyIngredient
                        {
                            metal = activeOres[i].Key,
                            count = activeOres[i].Value,
                            percentage = pct
                        });
                    }

                    // Deduct impurities if specific deductions per metal exist
                    if (deductionsPerMetal != null && deductionsPerMetal.Count > 0)
                    {
                        int totalDed = 0;
                        for (int i = 0; i < comp.ingredients.Count; i++)
                        {
                            var ing = comp.ingredients[i];
                            if (deductionsPerMetal.TryGetValue(ing.metal, out int dedAmount) && dedAmount > 0)
                            {
                                int actualDeduct = Mathf.Min(ing.percentage, dedAmount);
                                ing.percentage -= actualDeduct;
                                totalDed += actualDeduct;
                                comp.ingredients[i] = ing;
                            }
                        }
                        comp.impurityPercentage = Mathf.Max(totalDed, Mathf.Clamp(impurity, 0, 95));
                    }
                    else if (impurity > 0 && comp.ingredients.Count > 0)
                    {
                        comp.impurityPercentage = Mathf.Clamp(impurity, 0, 99);
                        int remainingDeduction = comp.impurityPercentage;

                        // Locate first inserted metal
                        int targetIndex = 0;
                        if (firstMetal.HasValue)
                        {
                            int foundIdx = comp.ingredients.FindIndex(x => x.metal == firstMetal.Value);
                            if (foundIdx >= 0) targetIndex = foundIdx;
                        }

                        var targetIng = comp.ingredients[targetIndex];
                        int deductFromFirst = Mathf.Min(targetIng.percentage, remainingDeduction);
                        targetIng.percentage -= deductFromFirst;
                        remainingDeduction -= deductFromFirst;
                        comp.ingredients[targetIndex] = targetIng;

                        // Deduct remaining from subsequent metals
                        for (int i = 0; i < comp.ingredients.Count && remainingDeduction > 0; i++)
                        {
                            if (i == targetIndex) continue;
                            var ing = comp.ingredients[i];
                            int deduct = Mathf.Min(ing.percentage, remainingDeduction);
                            ing.percentage -= deduct;
                            remainingDeduction -= deduct;
                            comp.ingredients[i] = ing;
                        }
                    }
                }
            }
            return comp;
        }

        public string GetMaterialName() => AlloyNameRegistry.ResolveAlloyMaterialName(ingredients);

        public string GetAlloyName(string prefix = "Ingot")
        {
            string material = GetMaterialName();
            return $"{material} {prefix}";
        }

        public string GetCompositionDescription()
        {
            if (ingredients.Count == 0) return "Composition: None";

            StringBuilder sb = new StringBuilder();
            sb.Append("Composition: ");

            for (int i = 0; i < ingredients.Count; i++)
            {
                sb.Append($"{ingredients[i].metal} ({ingredients[i].percentage}%)");
                sb.Append(", ");
            }

            if (impurityPercentage > 0)
            {
                sb.Append($"Impurities ({impurityPercentage}%)");
            }
            else if (sb.Length >= 2)
            {
                sb.Length -= 2;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Calculates weighted average stats from alloy percentages.
        /// Thermal range requires the MINIMUM MAXIMUM and MAXIMUM MINIMUM across active metals.
        /// </summary>
        public MetalProperties CalculateWeightedProperties()
        {
            if (ingredients.Count == 0) return MetalProperties.GetDefault(MetalType.Iron);

            float totalDamage = 0f;
            float totalSpeed = 0f;
            float totalDurability = 0f;
            float totalResistance = 0f;
            float smeltTime = 0f;

            float highestMinTemp = float.MinValue;
            float lowestMaxTemp = float.MaxValue;
            float weightedCenter = 0f;
            int distinctMetals = 0;

            foreach (var ing in ingredients)
            {
                if (ing.count <= 0 && ing.percentage <= 0) continue;

                float weight = ing.percentage / 100f;
                MetalProperties props = MetalProperties.GetDefault(ing.metal);

                totalDamage += props.baseDamage * weight;
                totalSpeed += props.baseAttackSpeed * weight;
                totalDurability += props.baseDurability * weight;
                totalResistance += props.baseResistance * weight;
                smeltTime += props.smeltDuration * weight;

                highestMinTemp = Mathf.Max(highestMinTemp, props.minOptimumTemp);
                lowestMaxTemp = Mathf.Min(lowestMaxTemp, props.maxOptimumTemp);
                weightedCenter += ((props.minOptimumTemp + props.maxOptimumTemp) * 0.5f) * weight;
                distinctMetals++;
            }

            if (highestMinTemp == float.MinValue) highestMinTemp = 780f;

            float finalMin, finalMax;
            if (distinctMetals <= 1)
            {
                // Metal puro: área generosa (~220°C) para forja accesible
                finalMin = highestMinTemp;
                finalMax = lowestMaxTemp;
            }
            else
            {
                // Aleación: se intersectan los rangos y se concentra en 80°C para mantener el reto de timing
                float center = (highestMinTemp <= lowestMaxTemp) ? (highestMinTemp + lowestMaxTemp) * 0.5f : weightedCenter;
                finalMin = center - 40f;
                finalMax = center + 40f;
            }

            return new MetalProperties
            {
                metalType = ingredients[0].metal,
                baseDamage = totalDamage,
                baseAttackSpeed = totalSpeed,
                baseSharpness = totalDurability,
                baseResistance = totalResistance,
                minOptimumTemp = finalMin,
                maxOptimumTemp = finalMax,
                smeltDuration = smeltTime
            };
        }
    }
}
