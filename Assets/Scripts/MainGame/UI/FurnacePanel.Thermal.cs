using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    /// <summary>
    /// Gestión térmica de la UI del Horno: 40 casillas, zona perfecta de 2 celdas en rojo oscuro,
    /// color promedio para la aleación y colores sólidos individuales para zonas de un solo mineral.
    /// Principio SRP: Aisla la presentación y controles térmicos.
    /// </summary>
    public partial class FurnacePanel
    {
        private const int TotalThermalCells = 40;

        private void SetupRangeIndicator()
        {
            if (temperatureSlider == null) return;

            Transform existingGrid = temperatureSlider.transform.Find("ThermalCasillasGrid");
            RectTransform gridRect = existingGrid != null ? existingGrid as RectTransform : null;

            if (gridRect == null)
            {
                GameObject gridObj = new GameObject("ThermalCasillasGrid", typeof(RectTransform));
                gridObj.transform.SetParent(temperatureSlider.transform, false);
                gridRect = gridObj.GetComponent<RectTransform>();
                gridRect.anchorMin = Vector2.zero;
                gridRect.anchorMax = Vector2.one;
                gridRect.offsetMin = Vector2.zero;
                gridRect.offsetMax = Vector2.zero;
                gridRect.SetSiblingIndex(1); // Detrás de la aguja/cursor del slider
            }

            if (gridRect.childCount != TotalThermalCells)
            {
                for (int c = gridRect.childCount - 1; c >= 0; c--)
                {
                    var child = gridRect.GetChild(c);
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            _thermalCellImages.Clear();

            for (int i = 0; i < TotalThermalCells; i++)
            {
                string cellName = $"Casilla_{i}";
                Transform existingCell = gridRect.Find(cellName);
                RectTransform cellRect = existingCell != null ? existingCell as RectTransform : null;
                UnityEngine.UI.Image cellImg = null;

                if (cellRect == null)
                {
                    GameObject cellObj = new GameObject(cellName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    cellObj.transform.SetParent(gridRect, false);
                    cellRect = cellObj.GetComponent<RectTransform>();
                    cellImg = cellObj.GetComponent<UnityEngine.UI.Image>();
                    cellImg.raycastTarget = false;

                    // Posicionar celda con separación de 1px entre cada casilla
                    cellRect.anchorMin = new Vector2(i / (float)TotalThermalCells, 0.08f);
                    cellRect.anchorMax = new Vector2((i + 1) / (float)TotalThermalCells, 0.92f);
                    cellRect.offsetMin = new Vector2(0.5f, 0f);
                    cellRect.offsetMax = new Vector2(-0.5f, 0f);
                }
                else
                {
                    cellImg = cellRect.GetComponent<UnityEngine.UI.Image>();
                }

                cellImg.color = new Color(0.12f, 0.13f, 0.16f, 0.75f);
                _thermalCellImages.Add(cellImg);
            }
        }

        private Color GetAverageAlloyColor()
        {
            if (furnaceController == null) return Color.white;

            float r = 0f, g = 0f, b = 0f;
            int total = 0;

            foreach (var kvp in furnaceController.InsertedOres)
            {
                if (kvp.Value <= 0) continue;
                Color c = MetalProperties.GetMetalColor(kvp.Key);
                r += c.r * kvp.Value;
                g += c.g * kvp.Value;
                b += c.b * kvp.Value;
                total += kvp.Value;
            }

            if (total == 0) return new Color(0.12f, 0.13f, 0.16f, 0.75f);
            return new Color(r / total, g / total, b / total, 1.0f);
        }

        private void UpdateRangeIndicator()
        {
            if (temperatureSlider == null || furnaceController == null) return;
            if (_thermalCellImages.Count < TotalThermalCells) SetupRangeIndicator();

            float maxTemp = furnaceController.MaxTemperature > 0f ? furnaceController.MaxTemperature : 1600f;
            float tempPerCell = maxTemp / TotalThermalCells; // 40°C por casilla

            int totalOres = furnaceController.GetTotalOreCount();
            float minOpt = furnaceController.TargetMinTemp;
            float maxOpt = furnaceController.TargetMaxTemp;
            float sweetCenter = furnaceController.SweetSpotCenter;

            // La Zona Perfecta abarca exactamente 2 casillas centrales
            int sweetCell1 = -1;
            int sweetCell2 = -1;

            if (minOpt > 0f && maxOpt > minOpt && totalOres > 0)
            {
                sweetCell1 = Mathf.Clamp(Mathf.FloorToInt((sweetCenter - (tempPerCell * 0.5f)) / tempPerCell), 0, TotalThermalCells - 2);
                sweetCell2 = sweetCell1 + 1;
            }

            int currentHeatCell = Mathf.Clamp(Mathf.FloorToInt(furnaceController.CurrentTemperature / tempPerCell), 0, TotalThermalCells - 1);
            Color alloyColor = GetAverageAlloyColor();

            // Color de la Zona Perfecta: Rojo Oscuro (#B71C1C / #991B1B) de alto contraste contra oro puro, cobre y hierro
            Color sweetSpotColor = new Color(0.72f, 0.11f, 0.13f, 1.0f);

            for (int i = 0; i < TotalThermalCells; i++)
            {
                var img = _thermalCellImages[i];
                if (img == null) continue;

                float cellMid = (i + 0.5f) * tempPerCell;
                Color cellColor;

                if (totalOres == 0)
                {
                    // Sin minerales: casillas neutras
                    cellColor = new Color(0.12f, 0.13f, 0.16f, 0.75f);
                }
                else if (i == sweetCell1 || i == sweetCell2)
                {
                    // 1. Zona Perfecta de 2 casillas en Rojo Oscuro
                    cellColor = sweetSpotColor;
                }
                else if (minOpt > 0f && maxOpt > minOpt && cellMid >= minOpt && cellMid <= maxOpt)
                {
                    // 2. Rango compartido de fundición: Color promedio de la aleación (sin verde)
                    cellColor = alloyColor;
                }
                else
                {
                    // 3. Áreas donde solo un mineral se derrite: Mantiene el color sólido de dicho mineral
                    bool hasActiveMineral = false;
                    Color activeColor = Color.white;

                    foreach (var kvp in furnaceController.InsertedOres)
                    {
                        if (kvp.Value <= 0) continue;
                        var props = MetalProperties.GetDefault(kvp.Key);
                        if (cellMid >= props.minOptimumTemp && cellMid <= props.maxOptimumTemp)
                        {
                            hasActiveMineral = true;
                            activeColor = MetalProperties.GetMetalColor(kvp.Key);
                            activeColor.a = 1.0f; // Sólido, sin transparencias
                            break;
                        }
                    }

                    cellColor = hasActiveMineral
                        ? activeColor
                        : new Color(0.12f, 0.13f, 0.16f, 0.75f);
                }

                // Resaltar suavemente la casilla por donde está pasando el calor actualmente
                if (i == currentHeatCell)
                {
                    cellColor = Color.Lerp(cellColor, Color.white, 0.35f);
                    cellColor.a = 1.0f;
                }

                img.color = cellColor;
            }

            // Únicamente el rango en sí sin texto adicional
            if (targetRangeText != null)
            {
                targetRangeText.text = (totalOres > 0 && minOpt > 0f)
                    ? $"{minOpt:F0} - {maxOpt:F0} °C"
                    : "- °C";
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

        private void OnAddCoalClicked()
        {
            if (furnaceController == null) return;

            InventoryController inventory = GetCurrentPlayerInventory();
            if (inventory == null || !inventory.HasItem("Coal", 1))
            {
                SetStatus("You have no coal in your inventory.");
                return;
            }

            if (inventory.RemoveItem("Coal", 1))
            {
                furnaceController.AddCoal(1);
                SetStatus("Coal added (+Heat).");
                UpdateDisplay();
            }
        }
    }
}
