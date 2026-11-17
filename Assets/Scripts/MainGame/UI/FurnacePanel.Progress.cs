using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    /// <summary>
    /// Progreso de fundición, guía de minerales a ingresar (3, 6 o 9) y extracción de la Lupa inferior.
    /// </summary>
    public partial class FurnacePanel
    {
        private void Update()
        {
            if (furnaceController == null || !IsOpen || PauseMenuPanel.IsGamePaused) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool inTut = ForgeTutorialDirector.Instance != null && ForgeTutorialDirector.Instance.IsTutorialActive;
            bool canRegulate = !inTut || (furnaceController.GetTotalOreCount() >= 3 && _readyBloom == null && furnaceController.UnclaimedBloom == null);
            if (keyboard != null && canRegulate)
            {
                bool tooHot = furnaceController.TargetMaxTemp > 0f && furnaceController.CurrentTemperature > furnaceController.TargetMaxTemp;
                bool tooCold = furnaceController.TargetMinTemp > 0f && furnaceController.CurrentTemperature < furnaceController.TargetMinTemp;
                bool allowBellows = !inTut || !tooCold;
                bool allowCoal = !inTut || !tooHot;
                if (allowBellows && keyboard.spaceKey.wasPressedThisFrame)
                {
                    furnaceController.PulseBellows(45f);
                    ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzHornoFuelle();
                }
                else if (allowBellows && keyboard.spaceKey.isPressed)
                    furnaceController.ApplyBellowsCooling(Time.deltaTime);

                if (allowCoal && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame))
                    OnAddCoalClicked();
            }
            UpdateThermalButton();
            UpdateDisplay();
        }

        private void SetStatus(string message)
        {
            if (statusText != null && _lastStatusMessage != message)
            {
                _lastStatusMessage = message;
                statusText.text = message;
            }
        }

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
                UnityEngine.UI.Image img;

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
                    img.color = new Color(0.35f, 0.30f, 0.24f, 0.7f);
                    img.raycastTarget = false;

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
                var img = kvp.Value;
                if (img == null) continue;
                float markPosition = kvp.Key switch { 3 => 3f / 9f, 6 => 6f / 9f, _ => 0.98f };
                img.color = currentProgress >= markPosition
                    ? new Color(1.0f, 0.84f, 0.28f, 1.0f)
                    : new Color(0.28f, 0.22f, 0.16f, 0.65f);
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
            _lastDisplayedOreHash = int.MinValue;
            UpdateDisplay();
            ForjaGame.Audio.ForgeAudioSystem.PlayProcessComplete(bloom != null ? bloom.quality : 0.85f);
            int qualityPercent = bloom != null ? Mathf.RoundToInt(bloom.quality * 100f) : 100;
            SetStatus($"Smelting complete! Extract {bloom?.DisplayName ?? "Bloom"} ({qualityPercent}%) from bottom opening.");
        }

        private void OnExtractBloomClicked()
        {
            if (_readyBloom == null && furnaceController != null)
                _readyBloom = furnaceController.UnclaimedBloom;
            if (_readyBloom == null) return;

            InventoryController inventory = GetCurrentPlayerInventory();
            inventory?.AddBloom(_readyBloom);
            ForjaGame.Audio.ForgeAudioSystem.PlayItemPickup(transform.position);

            SetStatus($"{_readyBloom.DisplayName} ({_readyBloom.SizeLabel}) extracted and stored in inventory!");
            _readyBloom = null;

            furnaceController?.TakeUnclaimedBloom();
            if (progressSlider != null) progressSlider.value = 0f;
            _lastDisplayedOreHash = int.MinValue;
            UpdateProgressSizeMarks();
            UpdateDisplay();
            RefreshOresList();
        }

        private void UpdateDisplay()
        {
            if (furnaceController == null) return;
            if (_readyBloom == null && furnaceController.UnclaimedBloom != null)
                _readyBloom = furnaceController.UnclaimedBloom;

            OnTemperatureChanged(furnaceController.CurrentTemperature);
            if (_readyBloom != null)
            {
                if (progressSlider != null) progressSlider.value = 1.0f;
            }
            else
            {
                OnProgressChanged(furnaceController.SmeltProgress);
            }

            int combined = furnaceController.GetTotalOreCount() + _stagedOres.Count;
            bool validMultipleOf3 = _stagedOres.Count > 0 && combined >= 3 && (combined % 3 == 0);
            if (insertOresButton != null)
                insertOresButton.interactable = validMultipleOf3 && !furnaceController.IsSmeltInProgress && _readyBloom == null;

            if (extractBloomButton != null)
                extractBloomButton.gameObject.SetActive(_readyBloom != null);

            UpdateOreGuideAndSummaryText();
            UpdateStatusFeedback();
        }

        private void UpdateOreGuideAndSummaryText()
        {
            if (oreCountText == null) return;
            int inFurnace = furnaceController.GetTotalOreCount();
            int staged = _stagedOres.Count;
            int impurity = furnaceController.CurrentImpurityPercentage;
            int hash = (inFurnace * 397) ^ (staged * 31) ^ impurity ^ (_stagedOres.Count > 0 ? (int)_stagedOres[_stagedOres.Count - 1] * 7 : 0);
            if (hash == _lastDisplayedOreHash) return;
            _lastDisplayedOreHash = hash;

            int combined = inFurnace + staged;
            string multBadge = combined == 0
                ? "<color=#8B5A2B>Insert 3 (Small), 6 (Med) or 9 (Large)</color>"
                : (combined % 3 == 0)
                    ? $"<color=#1B6E32><b>[Valid Recipe: {combined}/9]</b></color>"
                    : $"<color=#A84300><b>[Need {3 - (combined % 3)} more to reach multiple of 3]</b></color>";

            var sb = new StringBuilder();
            sb.AppendLine($"<b>Total Load: {combined}/9</b>  {multBadge}");
            sb.AppendLine($"In 3D tray: <b>{FormatStagedSummary()}</b>");
            sb.Append(inFurnace > 0
                ? $"In crucible ({inFurnace}): {furnaceController.GetCurrentComposition().GetCompositionDescription()}"
                : "Smelt Guide: Copper (560-780°C) · Gold (670-890°C) · Iron (780-1000°C)");
            oreCountText.text = sb.ToString();
        }

        private string FormatStagedSummary()
        {
            if (_stagedOres.Count == 0) return "None staged";
            Dictionary<MetalType, int> counts = new();
            for (int i = 0; i < _stagedOres.Count; i++)
                counts[_stagedOres[i]] = counts.TryGetValue(_stagedOres[i], out int c) ? c + 1 : 1;

            List<string> parts = new();
            foreach (var kv in counts)
                parts.Add($"{RawMaterialCatalog.GetDisplayName(kv.Key.ToString()).Replace("Mineral de ", "").Replace(" Ore", "")} x{kv.Value}");
            return string.Join(", ", parts);
        }

        private void UpdateStatusFeedback()
        {
            if (_readyBloom != null)
                SetStatus("Bloom ready at bottom opening! Click 'Extract Bloom' or click on it.");
            else if (_stagedOres.Count > 0)
            {
                int total = furnaceController.GetTotalOreCount() + _stagedOres.Count;
                SetStatus(total % 3 == 0
                    ? $"Valid recipe ({total}/9)! Click 'Insert Ores' to load them into crucible."
                    : $"Staged {_stagedOres.Count} ores ({total}/9). Pick {3 - (total % 3)} more for a valid multiple of 3.");
            }
            else if (furnaceController.IsSmeltInProgress)
            {
                if (furnaceController.IsInSweetSpot)
                    SetStatus("[SWEET SPOT] (+60% Speed) Balance with Coal [Shift] and Bellows [Space].");
                else if (furnaceController.IsInOptimumRange)
                    SetStatus("Melting range active: regulate with Bellows [Space] or Coal [Shift].");
                else if (furnaceController.IsOverheating)
                    SetStatus("Overheating! Tap or hold Bellows [Space] to cool down quickly.");
                else
                    SetStatus("Heat low: press Coal [Shift] to raise temperature into the zone.");
            }
            else if (furnaceController.GetTotalOreCount() > 0)
            {
                int c = furnaceController.GetTotalOreCount();
                SetStatus(c % 3 == 0
                    ? $"Crucible loaded ({c}/9). Add Coal [Shift] to reach {furnaceController.TargetMinTemp:F0}°C."
                    : $"Crucible has {c}/9 ores. Select and insert {3 - (c % 3)} more.");
            }
            else
                SetStatus("Select 3, 6 or 9 ores and click 'Insert Ores'.");
        }
    }
}
