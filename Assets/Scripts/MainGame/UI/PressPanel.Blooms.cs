using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;
using ForjaGame.Inventory;
using ForjaGame.Data;

namespace ForjaGame.UI
{
    /// <summary>
    /// Partial class for PressPanel: Handles the Bloom ScrollView selection list.
    /// Uses ForgeScrollViewStyler for uniform fixed list layout with sprite icons.
    /// </summary>
    public partial class PressPanel
    {
        private void RefreshBloomsList()
        {
            if (bloomsContainer == null) return;
            ForgeScrollViewStyler.ConfigureFixedScrollView(bloomsContainer);

            foreach (Transform child in bloomsContainer) Destroy(child.gameObject);

            InventoryController inventory = GetCurrentPlayerInventory();
            IReadOnlyList<BloomData> blooms = inventory != null ? inventory.Blooms : null;

            bool hasBlooms = blooms != null && blooms.Count > 0;
            if (emptyBloomsNotice != null) emptyBloomsNotice.SetActive(!hasBlooms);
            if (!hasBlooms) return;

            for (int i = 0; i < blooms.Count; i++) CreateBloomEntry(blooms[i]);
        }

        private void CreateBloomEntry(BloomData bloom)
        {
            string title = $"{bloom.DisplayName} (Diff: {bloom.DifficultyLevel})";
            Color metalColor = bloom.composition != null
                ? MetalProperties.GetMetalColor(bloom.composition.CalculateWeightedProperties().metalType)
                : Color.gray;

            Sprite icon = ForgeSpriteDatabase.Instance?.defaultBloomSprite;
            bool isSelected = pressController != null && pressController.CurrentBloom != null && pressController.CurrentBloom.Id == bloom.Id;

            ForgeScrollViewStyler.CreateRowEntry(bloomsContainer, title, icon, metalColor, isSelected, () => SelectAndInsertBloom(bloom));
        }

        private void SelectAndInsertBloom(BloomData bloom)
        {
            if (pressController == null || bloom == null) return;
            if (pressController.HasBloom) OnRetrieveBloomClicked();

            InventoryController inventory = GetCurrentPlayerInventory();
            BloomData toInsert = bloom;
            if (inventory != null)
            {
                for (int i = 0; i < inventory.Blooms.Count; i++)
                {
                    if (inventory.Blooms[i].Id == bloom.Id) { toInsert = inventory.Blooms[i]; break; }
                }
                inventory.RemoveBloom(toInsert);
            }

            pressController.InsertBloom(toInsert);
            SetStatus($"{toInsert.DisplayName} placed on piston! Mash [SPACE].");
            UpdateDisplay();
        }
    }
}
