using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Gestión de inicio de fundición, reset, cálculo de requisitos y generación de la Lupa (SRP).
    /// </summary>
    public partial class FurnaceController
    {
        public BloomData TakeUnclaimedBloom()
        {
            BloomData bloom = _unclaimedBloom;
            _unclaimedBloom = null;
            ResetProgress();
            return bloom;
        }

        public void StartSmelting()
        {
            int totalOres = GetTotalOreCount();
            if (totalOres != 3 && totalOres != 6 && totalOres != 9)
                return;

            currentSmeltTime = 0f;
            currentQuality = 1.0f;
            _sweetSpotTime = 0f;
            _totalSmeltTimeSpent = 0f;
            hasReachedSharedRangeOnce = false;
            isSmelting = true;
        }

        public void ResetProgress()
        {
            isSmelting = false;
            currentSmeltTime = 0f;
            currentImpurityPercentage = 0;
            currentQuality = 1.0f;
            _sweetSpotTime = 0f;
            _totalSmeltTimeSpent = 0f;
            hasReachedSharedRangeOnce = false;
            _metalErrorSeconds.Clear();
            _metalImpurityDeductions.Clear();
            _isCompositionDirty = true;
            if (GetTotalOreCount() == 0) _firstEnteredMetal = null;
            SmeltProgressChanged?.Invoke(0f);
        }

        public void ResetFurnace()
        {
            ResetProgress();
            _insertedOres.Clear();
            _oreHistory.Clear();
            _firstEnteredMetal = null;
            targetTemperature = ambientTemperature;
            currentTemperature = ambientTemperature;
            _isCompositionDirty = true;
            RecalculateThermalRequirements();
        }

        public AlloyComposition GetCurrentComposition()
        {
            if (_isCompositionDirty || _cachedComposition == null)
            {
                _cachedComposition = AlloyComposition.FromOres(_insertedOres, currentImpurityPercentage, _firstEnteredMetal, _metalImpurityDeductions);
                _isCompositionDirty = false;
            }
            return _cachedComposition;
        }

        private void RecalculateThermalRequirements()
        {
            if (_insertedOres.Count == 0)
            {
                targetMinTemp = 0f;
                targetMaxTemp = 0f;
                requiredSmeltTime = 0f;
                return;
            }

            AlloyComposition composition = GetCurrentComposition();
            MetalProperties weightedProps = composition.CalculateWeightedProperties();

            targetMinTemp = weightedProps.minOptimumTemp;
            targetMaxTemp = weightedProps.maxOptimumTemp;
            requiredSmeltTime = weightedProps.smeltDuration;
        }

        public BloomData GenerateBloom()
        {
            int total = GetTotalOreCount();
            ItemSize size = total switch
            {
                3 => ItemSize.Small,
                6 => ItemSize.Medium,
                9 => ItemSize.Large,
                _ => ItemSize.Medium
            };

            AlloyComposition composition = GetCurrentComposition();
            BloomData bloom = new BloomData(size, composition, currentQuality);

            _insertedOres.Clear();
            _oreHistory.Clear();
            _firstEnteredMetal = null;
            currentImpurityPercentage = 0;
            hasReachedSharedRangeOnce = false;
            _metalErrorSeconds.Clear();
            _metalImpurityDeductions.Clear();
            _isCompositionDirty = true;
            RecalculateThermalRequirements();
            return bloom;
        }
    }
}
