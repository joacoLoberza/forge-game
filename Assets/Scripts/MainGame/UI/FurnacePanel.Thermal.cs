using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    /// <summary>
    /// Gestión térmica de la UI del Horno: 80 casillas de alta precisión (timing arcade),
    /// subida rápida por carbón (320°C/s) y enfriamiento ultra-rápido por fuelle (520°C/s).
    /// </summary>
    public partial class FurnacePanel
    {
        private const int TotalThermalCells = 80;

        [SerializeField] private Button bellowsButton;
        public Button BellowsButton => bellowsButton;

        public void EnsureBellowsButtonExists()
        {
            if (bellowsButton == null)
            {
                Transform existing = transform.Find("RightStationCard/ParchmentBody/BellowsButton") ?? transform.Find("BellowsButton");
                if (existing != null && existing.TryGetComponent<Button>(out var foundBtn)) bellowsButton = foundBtn;
                else bellowsButton = CreateActionButton("BellowsButton", "Fuelle [Espacio]");
            }
            if (bellowsButton != null)
            {
                BindPressDown(bellowsButton, OnBellowsClicked);
                SetButtonLabel(bellowsButton, "Fuelle [Espacio]");
            }
        }

        private void SetupRangeIndicator()
        {
            if (temperatureSlider == null) return;
            Transform existingGrid = temperatureSlider.transform.Find("ThermalCasillasGrid");
            RectTransform gridRect = existingGrid != null ? existingGrid as RectTransform : null;

            if (gridRect == null)
            {
                var gridObj = new GameObject("ThermalCasillasGrid", typeof(RectTransform));
                gridObj.transform.SetParent(temperatureSlider.transform, false);
                gridRect = gridObj.GetComponent<RectTransform>();
                gridRect.anchorMin = Vector2.zero; gridRect.anchorMax = Vector2.one;
                gridRect.offsetMin = Vector2.zero; gridRect.offsetMax = Vector2.zero;
                gridRect.SetSiblingIndex(1);
            }

            if (gridRect.childCount != TotalThermalCells)
            {
                for (int c = gridRect.childCount - 1; c >= 0; c--)
                {
                    var child = gridRect.GetChild(c);
                    if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
                }
            }

            _thermalCellImages.Clear();
            for (int i = 0; i < TotalThermalCells; i++)
            {
                string cellName = $"Casilla_{i}";
                Transform existingCell = gridRect.Find(cellName);
                RectTransform cellRect = existingCell != null ? existingCell as RectTransform : null;
                Image cellImg;

                if (cellRect == null)
                {
                    var cellObj = new GameObject(cellName, typeof(RectTransform), typeof(Image));
                    cellObj.transform.SetParent(gridRect, false);
                    cellRect = cellObj.GetComponent<RectTransform>();
                    cellImg = cellObj.GetComponent<Image>();
                    cellImg.raycastTarget = false;
                    cellRect.anchorMin = new Vector2(i / (float)TotalThermalCells, 0.08f);
                    cellRect.anchorMax = new Vector2((i + 1) / (float)TotalThermalCells, 0.92f);
                    cellRect.offsetMin = new Vector2(0.5f, 0f);
                    cellRect.offsetMax = new Vector2(-0.5f, 0f);
                }
                else cellImg = cellRect.GetComponent<Image>();

                cellImg.color = new Color(0.12f, 0.09f, 0.08f, 0.85f);
                _thermalCellImages.Add(cellImg);
            }
        }

        private Dictionary<MetalType, int> BuildPreviewOreCounts()
        {
            var dict = new Dictionary<MetalType, int>();
            if (furnaceController != null)
            {
                foreach (var kv in furnaceController.InsertedOres)
                    if (kv.Value > 0) dict[kv.Key] = kv.Value;
            }
            for (int i = 0; i < _stagedOres.Count; i++)
            {
                var m = _stagedOres[i];
                dict[m] = dict.TryGetValue(m, out int c) ? c + 1 : 1;
            }
            return dict;
        }

        private void UpdateRangeIndicator()
        {
            if (temperatureSlider == null || furnaceController == null) return;
            if (_thermalCellImages.Count < TotalThermalCells) SetupRangeIndicator();

            float maxTemp = furnaceController.MaxTemperature > 0f ? furnaceController.MaxTemperature : 1600f;
            float tempPerCell = maxTemp / TotalThermalCells;
            var previewOres = BuildPreviewOreCounts();
            int totalOres = 0;
            float r = 0f, g = 0f, b = 0f;
            foreach (var kv in previewOres)
            {
                totalOres += kv.Value;
                Color mc = MetalProperties.GetMetalColor(kv.Key);
                r += mc.r * kv.Value; g += mc.g * kv.Value; b += mc.b * kv.Value;
            }

            float minOpt = 0f, maxOpt = 0f;
            if (totalOres > 0)
            {
                var comp = AlloyComposition.FromOres(previewOres);
                var wp = comp.CalculateWeightedProperties();
                minOpt = wp.minOptimumTemp; maxOpt = wp.maxOptimumTemp;
            }

            float sweetCenter = (minOpt + maxOpt) * 0.5f;
            int sweetCell1 = -1, sweetCell2 = -1;
            if (minOpt > 0f && maxOpt > minOpt && totalOres > 0)
            {
                sweetCell1 = Mathf.Clamp(Mathf.FloorToInt((sweetCenter - tempPerCell * 0.5f) / tempPerCell), 0, TotalThermalCells - 2);
                sweetCell2 = sweetCell1 + 1;
            }

            int currentHeatCell = Mathf.Clamp(Mathf.FloorToInt(furnaceController.CurrentTemperature / tempPerCell), 0, TotalThermalCells - 1);
            Color alloyColor = totalOres > 0 ? new Color(r / totalOres, g / totalOres, b / totalOres, 1f) : new Color(0.12f, 0.09f, 0.08f, 0.85f);
            Color sweetSpotColor = new Color(0.95f, 0.20f, 0.15f, 1.0f);
            Color emptyCell = new Color(0.12f, 0.09f, 0.08f, 0.85f);

            for (int i = 0; i < TotalThermalCells; i++)
            {
                var img = _thermalCellImages[i];
                if (img == null) continue;
                float cellMid = (i + 0.5f) * tempPerCell;
                Color cellColor = emptyCell;

                if (totalOres > 0)
                {
                    if (i == sweetCell1 || i == sweetCell2) cellColor = sweetSpotColor;
                    else if (minOpt > 0f && maxOpt > minOpt && cellMid >= minOpt && cellMid <= maxOpt) cellColor = alloyColor;
                    else
                    {
                        Color overlapCol = Color.black;
                        int overlapCount = 0;
                        foreach (var kv in previewOres)
                        {
                            var props = MetalProperties.GetDefault(kv.Key);
                            if (cellMid >= props.minOptimumTemp && cellMid <= props.maxOptimumTemp)
                            {
                                overlapCol += MetalProperties.GetMetalColor(kv.Key);
                                overlapCount++;
                            }
                        }
                        if (overlapCount > 1) { cellColor = overlapCol / overlapCount; cellColor.a = 0.72f; }
                        else if (overlapCount == 1) { cellColor = overlapCol; cellColor.a = 0.50f; }
                    }
                }

                if (furnaceController.CurrentTemperature > 4f)
                {
                    if (i == currentHeatCell)
                    {
                        cellColor = new Color(1.0f, 0.98f, 0.70f, 1f);
                    }
                    else if (i == currentHeatCell - 1 || i == currentHeatCell + 1)
                    {
                        cellColor = Color.Lerp(cellColor, new Color(1.0f, 0.82f, 0.35f, 1f), 0.40f);
                    }
                }
                img.color = cellColor;
            }

            if (targetRangeText != null)
            {
                targetRangeText.text = (totalOres > 0 && minOpt > 0f)
                    ? $"Fusión: {minOpt:F0}-{maxOpt:F0}°C [Zona Estrecha]"
                    : "Rango: Selecciona minerales";
            }
        }

        private void OnTemperatureChanged(float temp)
        {
            if (temperatureSlider != null) temperatureSlider.value = temp;
            int tempInt = Mathf.RoundToInt(temp);
            if (temperatureText != null && tempInt != _lastDisplayedTempInt)
            {
                _lastDisplayedTempInt = tempInt;
                temperatureText.text = $"{tempInt} °C";
            }
            UpdateRangeIndicator();
        }

        public void UpdateThermalButton()
        {
            int coal = 0;
            var inv = GetCurrentPlayerInventory();
            if (inv != null) coal = inv.GetCount("Coal");
            if (addCoalButton != null) SetButtonLabel(addCoalButton, $"+ Carbón ({coal}) [Shift]");
            if (bellowsButton != null) SetButtonLabel(bellowsButton, "Fuelle [Espacio]");
        }

        private void OnAddCoalClicked()
        {
            if (furnaceController == null) return;
            InventoryController inventory = GetCurrentPlayerInventory();
            if (inventory == null || !inventory.HasItem("Coal", 1))
            {
                SetStatus("No tienes Carbón en el inventario (ve a la mina).");
                return;
            }

            if (inventory.RemoveItem("Coal", 1))
            {
                furnaceController.AddCoal(1);
                SetStatus("Carbón añadido (+140°C). Subida constante y uniforme.");
                UpdateDisplay();
            }
        }

        private void OnBellowsClicked()
        {
            if (furnaceController == null) return;
            furnaceController.PulseBellows(45f);
            SetStatus("Fuelle: frenada suave (-45°C). Mantén [Espacio] para caída continua.");
            UpdateDisplay();
        }
    }
}
