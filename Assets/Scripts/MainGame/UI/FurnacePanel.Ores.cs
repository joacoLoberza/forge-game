using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    /// <summary>
    /// Selección previa de minerales en crudo (con vista 3D), botón "Ingresar minerales"
    /// por la parte superior del horno y devolución al inventario.
    /// </summary>
    public partial class FurnacePanel
    {
        public void RefreshOresList()
        {
            if (oresContainer == null) return;
            Transform targetContent = ForgeScrollViewStyler.ResolveContentContainer(oresContainer);
            ForgeScrollViewStyler.ConfigureFixedScrollView(targetContent);

            foreach (Transform child in targetContent)
                Destroy(child.gameObject);

            InventoryController inventory = GetCurrentPlayerInventory();
            if (inventory == null || inventory.RawMaterials == null || inventory.RawMaterials.Count == 0)
            {
                if (emptyOresNotice != null) emptyOresNotice.SetActive(true);
                return;
            }

            int validCount = 0;
            foreach (var kvp in inventory.RawMaterials)
            {
                if (System.Enum.TryParse<MetalType>(kvp.Key, true, out MetalType metal) && kvp.Value > 0)
                {
                    validCount++;
                    CreateOreEntry(metal, kvp.Value);
                }
            }

            if (emptyOresNotice != null) emptyOresNotice.SetActive(validCount == 0);
        }

        private void CreateOreEntry(MetalType metal, int count)
        {
            string displayName = RawMaterialCatalog.GetDisplayName(metal.ToString());
            var props = MetalProperties.GetDefault(metal);
            string title = $"{displayName} (x{count})  [{props.minOptimumTemp:F0}-{props.maxOptimumTemp:F0}°C]";
            Color metalColor = MetalProperties.GetMetalColor(metal);
            Sprite icon = ForjaGame.Data.ForgeSpriteDatabase.Instance?.GetMineralSprite(metal.ToString());
            bool isStaged = _stagedOres.Contains(metal);

            Transform targetContent = ForgeScrollViewStyler.ResolveContentContainer(oresContainer);
            ForgeScrollViewStyler.CreateRowEntry(targetContent, title, icon, metalColor, isStaged, () => OnStageOreClicked(metal, 1));
        }

        private void OnStageOreClicked(MetalType metal, int count = 1)
        {
            if (furnaceController == null) return;

            if (furnaceController.IsSmeltInProgress || furnaceController.SmeltProgress > 0f)
            {
                SetStatus("No puedes seleccionar minerales mientras la fundición está en curso.");
                return;
            }

            if (furnaceController.UnclaimedBloom != null || _readyBloom != null)
            {
                SetStatus("¡Primero debes sacar la Lupa de la parte inferior del horno!");
                return;
            }

            int combinedTotal = furnaceController.GetTotalOreCount() + _stagedOres.Count;
            int available = 9 - combinedTotal;
            if (available <= 0)
            {
                SetStatus("Límite máximo de 9 minerales alcanzado. Pulsa 'Ingresar minerales'.");
                return;
            }

            if (!HasMetalInCombinedSet(metal) && CountCombinedDistinctMetals() >= 3)
            {
                SetStatus("Límite de aleación: máximo 3 tipos distintos de mineral.");
                return;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (count == 1 && kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
                count = Mathf.Min(3, available);
            else
                count = Mathf.Min(count, available);

            InventoryController inventory = GetCurrentPlayerInventory();
            string mineralId = metal.ToString();
            while (count > 1 && (inventory == null || !inventory.HasItem(mineralId, count))) count--;

            if (inventory == null || !inventory.HasItem(mineralId, count))
            {
                SetStatus($"No tienes suficiente {RawMaterialCatalog.GetDisplayName(mineralId)}.");
                return;
            }

            if (inventory.RemoveItem(mineralId, count))
            {
                for (int i = 0; i < count; i++) _stagedOres.Add(metal);
                _visualController?.UpdateStagedOresPreview(_stagedOres);

                int newTotal = furnaceController.GetTotalOreCount() + _stagedOres.Count;
                string hint = (newTotal % 3 == 0)
                    ? $"¡Múltiplo de {newTotal} listo! Pulsa 'Ingresar minerales'."
                    : $"Faltan {3 - (newTotal % 3)} para múltiplo de 3 (3, 6 o 9).";
                SetStatus($"Seleccionado {RawMaterialCatalog.GetDisplayName(mineralId)} (+{count}). {hint}");

                _lastDisplayedOreHash = int.MinValue;
                UpdateDisplay();
                RefreshOresList();
            }
        }

        private void OnInsertStagedOresClicked()
        {
            if (furnaceController == null) return;
            if (_stagedOres.Count == 0)
            {
                SetStatus("Selecciona 3, 6 o 9 minerales primero para ingresarlos por arriba.");
                return;
            }

            if (furnaceController.IsSmeltInProgress || furnaceController.UnclaimedBloom != null)
            {
                SetStatus("Espera a que termine el proceso o saca la Lupa inferior.");
                return;
            }

            int combined = furnaceController.GetTotalOreCount() + _stagedOres.Count;
            if (combined < 3 || (combined % 3) != 0)
            {
                int missing = 3 - (combined % 3);
                SetStatus($"Solo puedes ingresar minerales en múltiplos de 3 (3, 6 o 9). Faltan {missing}.");
                return;
            }

            var committed = new List<MetalType>();
            InventoryController inv = GetCurrentPlayerInventory();

            for (int i = 0; i < _stagedOres.Count; i++)
            {
                MetalType m = _stagedOres[i];
                if (furnaceController.InsertOre(m, 1))
                    committed.Add(m);
                else
                    inv?.AddItem(m.ToString(), 1);
            }

            _stagedOres.Clear();
            if (committed.Count > 0)
            {
                _visualController?.PlayInsertOresTopAnimation(committed);
                stationController?.NotifyTopOreInsertion(1.35f);
                int total = furnaceController.GetTotalOreCount();
                string sizeHint = (total % 3 == 0)
                    ? $"¡{total} minerales en el crisol! Añade Carbón [Shift] para calentar."
                    : $"Ingresados {committed.Count} minerales ({total}/9). Añade {3 - (total % 3)} más para fundir.";
                SetStatus(sizeHint);
            }

            _lastDisplayedOreHash = int.MinValue;
            UpdateDisplay();
            RefreshOresList();
        }

        private void OnExtractLastOreClicked()
        {
            if (furnaceController == null) return;

            if (_stagedOres.Count > 0)
            {
                MetalType removed = _stagedOres[_stagedOres.Count - 1];
                _stagedOres.RemoveAt(_stagedOres.Count - 1);
                GetCurrentPlayerInventory()?.AddItem(removed.ToString(), 1);
                _visualController?.UpdateStagedOresPreview(_stagedOres);
                SetStatus($"Quitado {RawMaterialCatalog.GetDisplayName(removed.ToString())} de la selección.");
                _lastDisplayedOreHash = int.MinValue;
                UpdateDisplay();
                RefreshOresList();
                return;
            }

            if (furnaceController.IsSmeltInProgress || furnaceController.SmeltProgress > 0f)
            {
                SetStatus("No se pueden retirar minerales con la fundición iniciada.");
                return;
            }

            MetalType? extracted = furnaceController.ExtractLastOre();
            if (extracted.HasValue)
            {
                GetCurrentPlayerInventory()?.AddItem(extracted.Value.ToString(), 1);
                SetStatus($"Retirado 1 {RawMaterialCatalog.GetDisplayName(extracted.Value.ToString())} al inventario.");
                _lastDisplayedOreHash = int.MinValue;
                UpdateDisplay();
                RefreshOresList();
            }
            else
            {
                SetStatus("No hay minerales seleccionados ni dentro del horno.");
            }
        }

        private void RefundStagedOresToInventory()
        {
            if (_stagedOres.Count == 0) return;
            InventoryController inv = GetCurrentPlayerInventory();
            for (int i = 0; i < _stagedOres.Count; i++)
                inv?.AddItem(_stagedOres[i].ToString(), 1);
            _stagedOres.Clear();
            _visualController?.UpdateStagedOresPreview(_stagedOres);
        }

        private bool HasMetalInCombinedSet(MetalType metal)
        {
            if (furnaceController != null && furnaceController.InsertedOres.TryGetValue(metal, out int c) && c > 0) return true;
            return _stagedOres.Contains(metal);
        }

        private int CountCombinedDistinctMetals()
        {
            HashSet<MetalType> set = new();
            if (furnaceController != null)
                foreach (var kvp in furnaceController.InsertedOres)
                    if (kvp.Value > 0) set.Add(kvp.Key);
            for (int i = 0; i < _stagedOres.Count; i++) set.Add(_stagedOres[i]);
            return set.Count;
        }

        private InventoryController GetCurrentPlayerInventory()
        {
            if (stationController != null && stationController.CurrentInteractor != null)
            {
                var inv = stationController.CurrentInteractor.GetComponentInParent<InventoryController>();
                if (inv == null) inv = stationController.CurrentInteractor.GetComponentInChildren<InventoryController>();
                if (inv != null) return inv;
            }
            if (InventoryController.LocalInstance != null) return InventoryController.LocalInstance;
            return FindFirstObjectByType<InventoryController>();
        }
    }
}
