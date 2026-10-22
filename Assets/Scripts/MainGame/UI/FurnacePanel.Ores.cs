using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    /// <summary>
    /// Ore and inventory management for the Furnace UI.
    /// Handles player inventory ore ScrollView and ore extraction.
    /// </summary>
    public partial class FurnacePanel
    {
        public void RefreshOresList()
        {
            if (oresContainer == null) return;

            ForgeScrollViewStyler.ConfigureFixedScrollView(oresContainer);

            foreach (Transform child in oresContainer)
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
            string title = $"{displayName}  (x{count})";
            Color metalColor = MetalProperties.GetMetalColor(metal);
            Sprite icon = ForjaGame.Data.ForgeSpriteDatabase.Instance?.GetMineralSprite(metal.ToString());

            ForgeScrollViewStyler.CreateRowEntry(oresContainer, title, icon, metalColor, false, () => OnAddOreClicked(metal, 1));
        }

        private void OnAddOreClicked(MetalType metal, int count = 1)
        {
            if (furnaceController == null) return;

            if (furnaceController.IsSmeltInProgress || furnaceController.SmeltProgress > 0f)
            {
                SetStatus("Cannot add ores while smelting is in progress.");
                return;
            }

            if (furnaceController.UnclaimedBloom != null || _readyBloom != null)
            {
                SetStatus("You must retrieve the Bloom before adding new ores!");
                return;
            }

            int available = 9 - furnaceController.GetTotalOreCount();
            if (available <= 0)
            {
                SetStatus("Furnace reached maximum limit of 9 ores.");
                return;
            }

            if (!furnaceController.InsertedOres.ContainsKey(metal) && furnaceController.GetActiveDistinctMetalCount() >= 3)
            {
                SetStatus("Alloy limit reached: maximum 3 distinct metals.");
                return;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (count == 1 && kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                count = Mathf.Min(3, available);
            }
            else
            {
                count = Mathf.Min(count, available);
            }

            InventoryController inventory = GetCurrentPlayerInventory();
            string mineralId = metal.ToString();

            if (inventory == null || !inventory.HasItem(mineralId, count))
            {
                if (count > 1 && inventory != null)
                {
                    while (count > 1 && !inventory.HasItem(mineralId, count)) count--;
                }

                if (inventory == null || !inventory.HasItem(mineralId, count))
                {
                    SetStatus($"Not enough {metal} ores in your inventory.");
                    return;
                }
            }

            if (inventory.RemoveItem(mineralId, count))
            {
                bool inserted = furnaceController.InsertOre(metal, count);
                if (inserted)
                {
                    int total = furnaceController.GetTotalOreCount();
                    string sizeInfo = (total % 3 == 0)
                        ? $"{(total == 3 ? "Small" : total == 6 ? "Medium" : "Large")} Bloom mark ready (3/3)! Heat into the green zone."
                        : $"{3 - (total % 3)} more needed for the next Bloom mark.";
                    SetStatus($"Added {count} {metal}. Total: {total}/9. {sizeInfo}");
                    UpdateDisplay();
                    RefreshOresList();
                }
                else
                {
                    inventory.AddItem(mineralId, count);
                    SetStatus($"Failed to add {metal} ore.");
                }
            }
        }

        private void OnExtractLastOreClicked()
        {
            if (furnaceController == null) return;

            if (furnaceController.IsSmeltInProgress || furnaceController.SmeltProgress > 0f)
            {
                SetStatus("Cannot extract ores while smelting is in progress.");
                return;
            }

            MetalType? extracted = furnaceController.ExtractLastOre();
            if (extracted.HasValue)
            {
                InventoryController inventory = GetCurrentPlayerInventory();
                inventory?.AddItem(extracted.Value.ToString(), 1);
                SetStatus($"Extracted 1 {extracted.Value} back to inventory.");
                UpdateDisplay();
                RefreshOresList();
            }
            else
            {
                SetStatus("Furnace is empty. No ores to extract.");
            }
        }

        private InventoryController GetCurrentPlayerInventory()
        {
            if (stationController != null && stationController.CurrentInteractor != null)
            {
                return stationController.CurrentInteractor.GetComponentInParent<InventoryController>() ??
                       stationController.CurrentInteractor.GetComponentInChildren<InventoryController>();
            }

            GameObject player = GameObject.FindWithTag("Player");
            return player != null ? player.GetComponent<InventoryController>() : FindFirstObjectByType<InventoryController>();
        }
    }
}
