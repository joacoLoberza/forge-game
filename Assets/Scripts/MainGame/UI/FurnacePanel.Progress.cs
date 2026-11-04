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
            if (furnaceController == null || !IsOpen) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame)
                    furnaceController.PulseBellows(45f);
                else if (keyboard.spaceKey.isPressed)
                    furnaceController.ApplyBellowsCooling(Time.deltaTime);

                if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
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
            int qualityPercent = bloom != null ? Mathf.RoundToInt(bloom.quality * 100f) : 100;
            SetStatus($"¡Fundición lista! Saca {bloom?.DisplayName ?? "la Lupa"} ({qualityPercent}%) de la boca inferior.");
        }

        private void OnExtractBloomClicked()
        {
            if (_readyBloom == null && furnaceController != null)
                _readyBloom = furnaceController.UnclaimedBloom;
            if (_readyBloom == null) return;

            InventoryController inventory = GetCurrentPlayerInventory();
            inventory?.AddBloom(_readyBloom);

            SetStatus($"¡{_readyBloom.DisplayName} ({_readyBloom.SizeLabel}) extraída por debajo y guardada!");
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
                ? "<color=#8B5A2B>Ingresa 3 (Peq.), 6 (Med.) o 9 (Gde.)</color>"
                : (combined % 3 == 0)
                    ? $"<color=#1B6E32><b>[Receta válida: {combined}/9]</b></color>"
                    : $"<color=#A84300><b>[Faltan {3 - (combined % 3)} para múltiplo de 3]</b></color>";

            var sb = new StringBuilder();
            sb.AppendLine($"<b>Carga Total: {combined}/9</b>  {multBadge}");
            sb.AppendLine($"En bandeja 3D: <b>{FormatStagedSummary()}</b>");
            sb.Append(inFurnace > 0
                ? $"En crisol ({inFurnace}): {furnaceController.GetCurrentComposition().GetCompositionDescription()}"
                : "Guía de Fusión: Cobre (560-780°C) · Oro (670-890°C) · Hierro (780-1000°C)");
            oreCountText.text = sb.ToString();
        }

        private string FormatStagedSummary()
        {
            if (_stagedOres.Count == 0) return "Ninguno seleccionado";
            Dictionary<MetalType, int> counts = new();
            for (int i = 0; i < _stagedOres.Count; i++)
                counts[_stagedOres[i]] = counts.TryGetValue(_stagedOres[i], out int c) ? c + 1 : 1;

            List<string> parts = new();
            foreach (var kv in counts)
                parts.Add($"{RawMaterialCatalog.GetDisplayName(kv.Key.ToString()).Replace("Mineral de ", "")} x{kv.Value}");
            return string.Join(", ", parts);
        }

        private void UpdateStatusFeedback()
        {
            if (_readyBloom != null)
                SetStatus($"¡Lupa lista en la boca inferior! Pulsa 'Sacar Lupa' o haz clic en ella.");
            else if (_stagedOres.Count > 0)
            {
                int total = furnaceController.GetTotalOreCount() + _stagedOres.Count;
                SetStatus(total % 3 == 0
                    ? $"¡Múltiplo válido ({total}/9)! Pulsa 'Ingresar minerales' para echarlos por arriba."
                    : $"Seleccionados {_stagedOres.Count} minerales ({total}/9). Elige {3 - (total % 3)} más para alcanzar múltiplo de 3.");
            }
            else if (furnaceController.IsSmeltInProgress)
            {
                if (furnaceController.IsInSweetSpot)
                    SetStatus("[ZONA ÓPTIMA] (+60% Rapidez) Equilibra con Carbón [Shift] y Fuelle [Espacio].");
                else if (furnaceController.IsInOptimumRange)
                    SetStatus("Rango de fusión activo: compensa con Fuelle [Espacio] o Carbón [Shift].");
                else if (furnaceController.IsOverheating)
                    SetStatus("¡Sobrecalentamiento! Pulsa o mantén Fuelle [Espacio] para bajar de golpe.");
                else
                    SetStatus("Fuego bajo: pulsa Carbón [Shift] para disparar la aguja hacia la zona.");
            }
            else if (furnaceController.GetTotalOreCount() > 0)
            {
                int c = furnaceController.GetTotalOreCount();
                SetStatus(c % 3 == 0
                    ? $"Crisol cargado ({c}/9). Echa Carbón [Shift] para subir a {furnaceController.TargetMinTemp:F0}°C."
                    : $"Crisol con {c}/9 minerales. Selecciona e ingresa {3 - (c % 3)} más.");
            }
            else
                SetStatus("Selecciona 3, 6 o 9 minerales y pulsa 'Ingresar minerales'.");
        }
    }
}
