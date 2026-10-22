using UnityEngine;
using TMPro;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    /// <summary>
    /// Smelting progress, multiples of 3 size marks, and Bloom collection UI.
    /// Handles progress display, quality feedback, and collection.
    /// </summary>
    public partial class FurnacePanel
    {
        private void SetupProgressSizeMarks()
        {
            if (progressSlider == null) return;

            int[] thresholds = { 3, 6, 9 };
            float[] normalizedPositions = { 3f / 9f, 6f / 9f, 1f };

            for (int i = 0; i < thresholds.Length; i++)
            {
                int oreCount = thresholds[i];
                string markName = $"SizeMark_{oreCount}";
                Transform existing = progressSlider.transform.Find(markName);
                RectTransform rect = existing != null ? existing as RectTransform : null;
                UnityEngine.UI.Image img = null;

                if (rect != null)
                {
                    img = rect.GetComponent<UnityEngine.UI.Image>();
                }
                else
                {
                    GameObject markObj = new GameObject(markName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    markObj.transform.SetParent(progressSlider.transform, false);
                    rect = markObj.GetComponent<RectTransform>();
                    img = markObj.GetComponent<UnityEngine.UI.Image>();
                    img.color = new Color(0.35f, 0.35f, 0.35f, 0.6f);
                    img.raycastTarget = false;

                    // Subtle vertical mark over progress bar
                    rect.anchorMin = new Vector2(normalizedPositions[i], 0f);
                    rect.anchorMax = new Vector2(normalizedPositions[i], 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(3f, 6f);
                    rect.SetAsLastSibling();
                }

                _sizeMarkIndicators[oreCount] = rect;
                _sizeMarkImages[oreCount] = img;
            }
        }

        private void UpdateProgressSizeMarks()
        {
            if (furnaceController == null || progressSlider == null) return;

            float currentProgress = progressSlider.value;

            foreach (var kvp in _sizeMarkImages)
            {
                int threshold = kvp.Key;
                var img = kvp.Value;
                if (img == null) continue;

                float markPosition = threshold switch
                {
                    3 => 3f / 9f,
                    6 => 6f / 9f,
                    _ => 0.98f
                };

                img.color = currentProgress >= markPosition
                    ? new Color(0.0f, 0.95f, 0.45f, 1.0f) // Emerald green
                    : new Color(0.35f, 0.35f, 0.35f, 0.5f); // Inactive gray
            }
        }

        private void OnProgressChanged(float progress)
        {
            if (progressSlider != null) progressSlider.value = progress;
            UpdateProgressSizeMarks();
        }

        private void OnSmeltCompleted(BloomData bloom)
        {
            _readyBloom = bloom;
            if (progressSlider != null) progressSlider.value = 1.0f;
            UpdateProgressSizeMarks();
            UpdateDisplay();
            int qualityPercent = bloom != null ? Mathf.RoundToInt(bloom.quality * 100f) : 100;
            string displayName = bloom != null ? bloom.DisplayName : "Bloom";
            SetStatus($"Smelting complete: {displayName} ({bloom?.size}) at {qualityPercent}% purity.");
        }

        private void OnExtractBloomClicked()
        {
            if (_readyBloom == null) return;

            InventoryController inventory = GetCurrentPlayerInventory();
            inventory?.AddBloom(_readyBloom);

            SetStatus($"{_readyBloom.DisplayName} ({_readyBloom.size}) added to inventory.");
            _readyBloom = null;

            furnaceController?.TakeUnclaimedBloom();
            if (progressSlider != null) progressSlider.value = 0f;
            UpdateProgressSizeMarks();
            UpdateDisplay();
            RefreshOresList();
        }

        private void UpdateDisplay()
        {
            if (furnaceController == null) return;

            OnTemperatureChanged(furnaceController.CurrentTemperature);

            if (_readyBloom != null)
            {
                if (progressSlider != null) progressSlider.value = 1.0f;
            }
            else
            {
                OnProgressChanged(furnaceController.SmeltProgress);
            }

            if (oreCountText != null)
            {
                int oreCount = furnaceController.GetTotalOreCount();
                int impurity = furnaceController.CurrentImpurityPercentage;
                if (oreCount != _lastDisplayedOreCount || impurity != _lastDisplayedImpurity)
                {
                    _lastDisplayedOreCount = oreCount;
                    _lastDisplayedImpurity = impurity;
                    if (oreCount == 0)
                    {
                        oreCountText.text = "Ores: 0 / 9 (Requires multiples of 3: 3, 6, or 9)\nComposition: Empty Furnace";
                    }
                    else
                    {
                        string multInfo = (oreCount % 3 == 0)
                            ? $"<color=#00E676>[Valid Multiple {oreCount}]</color>"
                            : $"<color=#FFB74D>[{3 - (oreCount % 3)} more needed for multiple of 3]</color>";

                        oreCountText.text = $"Ores: {oreCount} / 9 {multInfo}\n{furnaceController.GetCurrentComposition().GetCompositionDescription()}";
                    }
                }
            }

            if (extractBloomButton != null)
                extractBloomButton.gameObject.SetActive(_readyBloom != null);

            if (_readyBloom != null)
            {
                SetStatus($"Smelting ready. Collect the {_readyBloom.DisplayName ?? "Bloom"} to forge.");
            }
            else if (furnaceController.IsSmeltInProgress)
            {
                if (!furnaceController.HasReachedSharedRangeOnce)
                    SetStatus("Heating furnace toward smelting range...");
                else if (furnaceController.IsInSweetSpot)
                    SetStatus("Sweet Spot active: Harmonic smelting (Master Quality).");
                else if (furnaceController.IsInOptimumRange)
                    SetStatus("Smelting range active: Stable melting.");
                else if (furnaceController.IsOverheating)
                    SetStatus("Overheating: Open vent [Space] to release heat.");
                else if (furnaceController.IsUnderheated)
                    SetStatus("Low temperature: Add coal [Shift] to heat up.");
                else
                    SetStatus("Temperature out of range. Regulate heat.");
            }
            else if (furnaceController.GetTotalOreCount() > 0)
            {
                int count = furnaceController.GetTotalOreCount();
                if (count % 3 == 0)
                {
                    string size = count switch { 3 => "Small", 6 => "Medium", _ => "Large" };
                    SetStatus(furnaceController.IsInOptimumRange
                        ? $"Optimal temperature. Starting {size} Bloom smelting..."
                        : $"Ores ready ({size} Bloom). Heat to smelting range.");
                }
                else
                {
                    int needed = 3 - (count % 3);
                    SetStatus(furnaceController.IsInOptimumRange
                        ? $"[Need Ores: {count}/9] Add {needed} more to complete multiple of 3."
                        : $"[Ores: {count}/9] Add {needed} more to reach multiple of 3.");
                }
            }
            else
            {
                SetStatus("Furnace ready. Add ores (multiples of 3: 3, 6, or 9) to forge.");
            }
        }
    }
}
