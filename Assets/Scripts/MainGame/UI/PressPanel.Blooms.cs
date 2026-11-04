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
            Transform targetContainer = ForgeScrollViewStyler.ResolveContentContainer(bloomsContainer);
            ForgeScrollViewStyler.ConfigureFixedScrollView(bloomsContainer);

            for (int i = targetContainer.childCount - 1; i >= 0; i--)
            {
                Transform ch = targetContainer.GetChild(i);
                if (ch != null) ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(ch.gameObject);
            }

            InventoryController inventory = GetCurrentPlayerInventory();
            IReadOnlyList<BloomData> blooms = inventory != null ? inventory.Blooms : null;

            bool hasBlooms = blooms != null && blooms.Count > 0;
            if (emptyBloomsNotice != null) emptyBloomsNotice.SetActive(!hasBlooms);
            if (!hasBlooms) return;

            for (int i = 0; i < blooms.Count; i++) CreateBloomEntry(targetContainer, blooms[i]);
        }

        private void CreateBloomEntry(Transform container, BloomData bloom)
        {
            string title = $"{bloom.DisplayName} (Dif: {bloom.DifficultyLevel})";
            Color metalColor = BloomVisualBuilder.ResolveBloomDisplayColor(bloom);

            var spriteDb = ForgeSpriteDatabase.Instance;
            Sprite icon = (spriteDb != null ? spriteDb.defaultBloomSprite : null) ?? ProceduralItemIconProvider.GetItemIcon(bloom.DisplayName, "Bloom");
            bool isSelected = pressController != null && pressController.CurrentBloom != null && pressController.CurrentBloom.Id == bloom.Id;

            ForgeScrollViewStyler.CreateRowEntry(container, title, icon, metalColor, isSelected, () => SelectAndInsertBloom(bloom));
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
            SetStatus($"¡{toInsert.DisplayName} colocada en la base inferior de la prensa! Pulsa [ESPACIO].");
            UpdateDisplay();
        }
    }
}
