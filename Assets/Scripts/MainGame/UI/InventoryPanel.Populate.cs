using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    public partial class InventoryPanel
    {
        private void PopulateLeftPage()
        {
            if (leftItemsContainer == null) return;
            Transform targetContent = ForgeScrollViewStyler.ResolveContentContainer(leftItemsContainer);
            ForgeScrollViewStyler.ConfigureFixedScrollView(leftItemsContainer);

            _activeEntries.Clear();
            for (int i = targetContent.childCount - 1; i >= 0; i--)
            {
                Transform ch = targetContent.GetChild(i);
                if (ch != null) ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(ch.gameObject);
            }

            if (leftPageTitleText != null)
            {
                leftPageTitleText.text = _currentCategory switch
                {
                    ForgeItemCategory.RawMaterial => "Minerales y Carbón",
                    ForgeItemCategory.Bloom => "Lupas Forjadas",
                    ForgeItemCategory.Ingot => "Lingotes Prensados",
                    ForgeItemCategory.Handle => "Mangos de Armas",
                    ForgeItemCategory.Weapon => "Armas Forjadas",
                    _ => "Inventario"
                };
            }

            if (inventoryController == null) EnsureController();
            if (inventoryController == null) return;

            switch (_currentCategory)
            {
                case ForgeItemCategory.RawMaterial:
                    PopulateMinerals();
                    break;
                case ForgeItemCategory.Bloom:
                    PopulateItems(inventoryController.Blooms, new Color(0.95f, 0.48f, 0.20f), "No hay lupas forjadas en el inventario.");
                    break;
                case ForgeItemCategory.Ingot:
                    PopulateItems(inventoryController.Ingots, new Color(0.72f, 0.78f, 0.88f), "No hay lingotes prensados en el inventario.");
                    break;
                case ForgeItemCategory.Handle:
                    PopulateHandles();
                    break;
                case ForgeItemCategory.Weapon:
                    PopulateItems(inventoryController.Weapons, Color.white, "No hay armas forjadas en el inventario.", weaponDefaultIcon);
                    break;
            }

            if (targetContent is RectTransform contentRt)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            }
        }

        private void PopulateHandles()
        {
            var handles = inventoryController.Handles;
            if (handles == null || handles.Count == 0)
            {
                ForgeScrollViewStyler.CreateNoticeEntry(leftItemsContainer, "No hay mangos tallados en el inventario.");
                return;
            }

            var groups = new Dictionary<(string name, HandleLength len, float rng, float spd), (HandleData rep, int count)>();
            for (int i = 0; i < handles.Count; i++)
            {
                var h = handles[i];
                var key = (h.handleName, h.length, h.rangeModifier, h.speedModifier);
                if (groups.TryGetValue(key, out var val))
                {
                    groups[key] = (val.rep, val.count + 1);
                }
                else
                {
                    groups[key] = (h, 1);
                }
            }

            foreach (var kvp in groups.Values)
            {
                string title = kvp.count > 1 ? $"{kvp.rep.DisplayName}  x{kvp.count}" : kvp.rep.DisplayName;
                var groupData = new HandleGroupData(kvp.rep, kvp.count);
                Sprite icon = ProceduralItemIconProvider.GetItemIcon(kvp.rep.DisplayName, "Handle");
                CreateItemEntry(title, groupData, () => SelectItem(groupData), icon, new Color(0.62f, 0.42f, 0.22f));
            }
        }

        private void PopulateMinerals()
        {
            var raw = inventoryController.RawMaterials;
            if (raw == null || raw.Count == 0)
            {
                ForgeScrollViewStyler.CreateNoticeEntry(leftItemsContainer, "No tienes minerales ni carbón recolectados.");
                return;
            }

            bool hasAny = false;
            foreach (var kvp in raw)
            {
                if (kvp.Key == "Lupa" || kvp.Key.StartsWith("Lingote") || kvp.Key.StartsWith("Mango"))
                    continue;

                hasAny = true;
                string displayName = RawMaterialCatalog.GetDisplayName(kvp.Key);
                string title = $"{displayName}  x{kvp.Value}";
                var mineralData = new KeyValuePair<string, int>(kvp.Key, kvp.Value);
                Color color = GetMineralColor(kvp.Key);
                Sprite icon = ProceduralItemIconProvider.GetItemIcon(displayName, "Mineral");

                CreateItemEntry(title, mineralData, () => SelectItem(mineralData), icon, color);
            }

            if (!hasAny)
            {
                ForgeScrollViewStyler.CreateNoticeEntry(leftItemsContainer, "No tienes minerales ni carbón recolectados.");
            }
        }

        private void PopulateItems<T>(IReadOnlyList<T> items, Color fallbackColor, string emptyNotice, Sprite icon = null) where T : class
        {
            if (items == null || items.Count == 0)
            {
                ForgeScrollViewStyler.CreateNoticeEntry(leftItemsContainer, emptyNotice);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                string title = item is IForgeItem forgeItem ? forgeItem.DisplayName : item.ToString();
                Color itemColor = fallbackColor;
                if (item is BloomData bd)
                    itemColor = BloomVisualBuilder.ResolveBloomDisplayColor(bd);
                else if (item is IngotData id)
                    itemColor = BloomVisualBuilder.ResolveCompositionColor(id.composition);
                Sprite itemIcon = icon ?? ProceduralItemIconProvider.GetItemIcon(title, item is BloomData ? "Bloom" : (item is IngotData ? "Ingot" : "Weapon"));
                CreateItemEntry(title, item, () => SelectItem(item), itemIcon, itemColor);
            }
        }
    }
}
