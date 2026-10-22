using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI.Core;
using ForjaGame.Inventory;
using ForjaGame.Forge;
using ForjaGame.Combat;

namespace ForjaGame.UI
{
    /// <summary>
    /// Panel del Diario / Manual de Inventario con formato de dos páginas (Hoja Izquierda y Hoja Derecha).
    /// Hoja Izquierda: Pestañas de categorías y lista scrolleable de ítems.
    /// Hoja Derecha: Detalles, estadísticas completas y lore del ítem seleccionado (ver InventoryPanel.Details.cs).
    /// </summary>
    public partial class InventoryPanel : UIPanelBase
    {
        [Header("Controlador")]
        [SerializeField] private InventoryController inventoryController;

        [Header("Pestañas de Categoría (Páginas)")]
        [SerializeField] private Button tabMineralsButton;
        [SerializeField] private Button tabBloomsButton;
        [SerializeField] private Button tabIngotsButton;
        [SerializeField] private Button tabHandlesButton;
        [SerializeField] private Button tabWeaponsButton;
        [SerializeField] private Button closeButton;

        [Header("Hoja Izquierda (Lista de Ítems)")]
        [SerializeField] private TMP_Text leftPageTitleText;
        [SerializeField] private Transform leftItemsContainer;
        [SerializeField] private ScrollRect leftScrollRect;
        [SerializeField] private GameObject itemEntryPrefab;

        [Header("Hoja Derecha (Detalles del Ítem)")]
        [SerializeField] private TMP_Text rightItemTitleText;
        [SerializeField] private TMP_Text rightItemCategoryText;
        [SerializeField] private TMP_Text rightItemDescriptionText;
        [SerializeField] private ScrollRect rightScrollRect;
        [SerializeField] private GameObject emptySelectionNotice;

        [Header("Equipamiento de Armas")]
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipButtonText;

        public Button EquipButton => equipButton;
        public TMP_Text EquipButtonText => equipButtonText;

        public static InventoryPanel Instance { get; private set; }

        private ForgeItemCategory _currentCategory = ForgeItemCategory.RawMaterial;
        private object _selectedItem;
        private readonly List<(object item, InventoryItemEntryUI ui)> _activeEntries = new();
        [SerializeField] private Sprite weaponDefaultIcon;

        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            EnsureController();

#if UNITY_EDITOR
            if (weaponDefaultIcon == null)
                weaponDefaultIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Journal/sword_sketch_example.png");
#endif

            if (equipButton == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn.name.IndexOf("equip", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        equipButton = btn;
                        break;
                    }
                }
            }

            if (tabMineralsButton != null) BindPressDown(tabMineralsButton, () => SelectCategory(ForgeItemCategory.RawMaterial));
            if (tabBloomsButton != null) BindPressDown(tabBloomsButton, () => SelectCategory(ForgeItemCategory.Bloom));
            if (tabIngotsButton != null) BindPressDown(tabIngotsButton, () => SelectCategory(ForgeItemCategory.Ingot));
            if (tabHandlesButton != null) BindPressDown(tabHandlesButton, () => SelectCategory(ForgeItemCategory.Handle));
            if (tabWeaponsButton != null) BindPressDown(tabWeaponsButton, () => SelectCategory(ForgeItemCategory.Weapon));
            if (closeButton != null) BindPressDown(closeButton, Close);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
        }

        private void OnEnable()
        {
            EnsureController();
            if (inventoryController != null)
            {
                inventoryController.InventoryUpdated += OnInventoryUpdated;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (inventoryController != null)
            {
                inventoryController.InventoryUpdated -= OnInventoryUpdated;
            }
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            EnsureController();
            SelectCategory(_currentCategory);
        }

        private void OnInventoryUpdated()
        {
            if (IsOpen)
            {
                RefreshCurrentView();
            }
        }

        public void RefreshCurrentView()
        {
            PopulateLeftPage();
            UpdateTabVisuals();
            RenderRightPage(_selectedItem);
        }

        public void SelectCategory(ForgeItemCategory category)
        {
            _currentCategory = category;
            _selectedItem = null;
            RefreshCurrentView();
        }

        public void OpenFor(InventoryController inv, PlayerEquipmentController equip)
        {
            if (IsOpen && inventoryController == inv)
            {
                Close();
                return;
            }

            if (inventoryController != null && inventoryController != inv)
            {
                inventoryController.InventoryUpdated -= OnInventoryUpdated;
            }

            inventoryController = inv;
            _equipmentController = equip;

            if (inventoryController != null)
            {
                inventoryController.InventoryUpdated += OnInventoryUpdated;
            }

            Open();
            RefreshCurrentView();
        }

        private void EnsureController()
        {
            if (inventoryController == null)
            {
                inventoryController = FindFirstObjectByType<InventoryController>();
                if (inventoryController == null)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null) inventoryController = player.GetComponent<InventoryController>();
                }
            }
        }

        private void PopulateLeftPage()
        {
            if (leftItemsContainer == null) return;

            _activeEntries.Clear();
            foreach (Transform child in leftItemsContainer)
            {
                Destroy(child.gameObject);
            }

            ForgeScrollViewStyler.ConfigureFixedScrollView(leftItemsContainer);

            if (leftPageTitleText != null)
            {
                leftPageTitleText.text = _currentCategory switch
                {
                    ForgeItemCategory.RawMaterial => "Página: Minerales y Carbón",
                    ForgeItemCategory.Bloom => "Página: Lupas Forjadas",
                    ForgeItemCategory.Ingot => "Página: Lingotes Prensados",
                    ForgeItemCategory.Handle => "Página: Mangos de Armas",
                    ForgeItemCategory.Weapon => "Página: Armas Forjadas",
                    _ => "Página: Inventario"
                };
            }

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
                CreateItemEntry(title, groupData, () => SelectItem(groupData), null, new Color(0.62f, 0.42f, 0.22f));
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

                CreateItemEntry(title, mineralData, () => SelectItem(mineralData), null, color);
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
                CreateItemEntry(title, item, () => SelectItem(item), icon, fallbackColor);
            }
        }
    }
}
