using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Gestión de inserción, extracción y validación de minerales en el horno (SRP).
    /// </summary>
    public partial class FurnaceController
    {
        public int GetActiveDistinctMetalCount()
        {
            int count = 0;
            foreach (var kvp in _insertedOres)
            {
                if (kvp.Value > 0) count++;
            }
            return count;
        }

        public int GetMeltingMetalCount()
        {
            int count = 0;
            foreach (var kvp in _insertedOres)
            {
                if (kvp.Value > 0 && IsMetalMelting(kvp.Key)) count++;
            }
            return count;
        }

        public bool IsAnyMetalMelting()
        {
            if (_insertedOres.Count == 0) return false;
            foreach (var kvp in _insertedOres)
            {
                if (kvp.Value <= 0) continue;
                var props = MetalProperties.GetDefault(kvp.Key);
                if (currentTemperature >= props.minOptimumTemp && currentTemperature <= props.maxOptimumTemp)
                    return true;
            }
            return false;
        }

        public bool IsMetalMelting(MetalType metal)
        {
            var props = MetalProperties.GetDefault(metal);
            return currentTemperature >= props.minOptimumTemp && currentTemperature <= props.maxOptimumTemp;
        }

        public int GetTotalOreCount()
        {
            int total = 0;
            foreach (var kvp in _insertedOres)
                total += kvp.Value;
            return total;
        }

        /// <summary>
        /// Ingresa minerales al horno. Bloqueado si hay una lupa pendiente o si la fundición está en progreso.
        /// Límite de aleación de 3 minerales diferentes como máximo.
        /// </summary>
        public bool InsertOre(MetalType metal, int amount = 1)
        {
            if (_unclaimedBloom != null || IsSmeltInProgress) return false;
            if (amount <= 0 || GetTotalOreCount() + amount > 9) return false;

            // Límite de aleación: no se pueden mezclar más de 3 tipos distintos de minerales
            if (!_insertedOres.ContainsKey(metal) && _insertedOres.Count >= 3)
                return false;

            if (!_firstEnteredMetal.HasValue)
                _firstEnteredMetal = metal;

            if (!_insertedOres.ContainsKey(metal))
                _insertedOres[metal] = 0;

            _insertedOres[metal] += amount;

            for (int i = 0; i < amount; i++)
            {
                _oreHistory.Push(metal);
            }

            _isCompositionDirty = true;
            RecalculateThermalRequirements();
            return true;
        }

        /// <summary>
        /// Extrae el último mineral. Bloqueado si la fundición está en progreso.
        /// </summary>
        public MetalType? ExtractLastOre()
        {
            if (IsSmeltInProgress || _oreHistory.Count == 0)
                return null;

            MetalType lastMetal = _oreHistory.Pop();
            if (_insertedOres.ContainsKey(lastMetal))
            {
                _insertedOres[lastMetal]--;
                if (_insertedOres[lastMetal] <= 0)
                {
                    _insertedOres.Remove(lastMetal);
                }
            }

            if (GetTotalOreCount() == 0)
                _firstEnteredMetal = null;

            _isCompositionDirty = true;
            RecalculateThermalRequirements();
            return lastMetal;
        }

        public MetalType GetLowestMeltingActiveMetal()
        {
            MetalType best = MetalType.Gold;
            float lowestMax = float.MaxValue;
            foreach (var kvp in _insertedOres)
            {
                if (kvp.Value <= 0) continue;
                var props = MetalProperties.GetDefault(kvp.Key);
                if (props.maxOptimumTemp < lowestMax)
                {
                    lowestMax = props.maxOptimumTemp;
                    best = kvp.Key;
                }
            }
            return best;
        }

        public MetalType GetHighestMeltingActiveMetal()
        {
            MetalType best = MetalType.Iron;
            float highestMin = float.MinValue;
            foreach (var kvp in _insertedOres)
            {
                if (kvp.Value <= 0) continue;
                var props = MetalProperties.GetDefault(kvp.Key);
                if (props.minOptimumTemp > highestMin)
                {
                    highestMin = props.minOptimumTemp;
                    best = kvp.Key;
                }
            }
            return best;
        }
    }
}
