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

            CleanAndBindTabButton(tabMineralsButton, () => SelectCategory(ForgeItemCategory.RawMaterial));
            CleanAndBindTabButton(tabBloomsButton, () => SelectCategory(ForgeItemCategory.Bloom));
            CleanAndBindTabButton(tabIngotsButton, () => SelectCategory(ForgeItemCategory.Ingot));
            CleanAndBindTabButton(tabHandlesButton, () => SelectCategory(ForgeItemCategory.Handle));
            CleanAndBindTabButton(tabWeaponsButton, () => SelectCategory(ForgeItemCategory.Weapon));
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        private void CleanAndBindTabButton(Button btn, UnityEngine.Events.UnityAction action)
        {
            if (btn == null) return;
            var trigger = btn.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger != null) Destroy(trigger);

            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);

            var img = btn.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            var txt = btn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.raycastTarget = false;
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ForjaGame.Core.GameStateController.ChangeState(ForjaGame.Core.GameState.Inventory);
            EnsureController();
            SelectCategory(_currentCategory);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            ForjaGame.Core.GameStateController.ChangeState(ForjaGame.Core.GameState.Exploration);

            if (!IsAnyPanelOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
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
            EnsureController();
            PopulateLeftPage();
            UpdateTabVisuals();
            RenderRightPage(_selectedItem);
            Canvas.ForceUpdateCanvases();
        }

        public void SelectCategory(ForgeItemCategory category)
        {
            _currentCategory = category;
            _selectedItem = null;
            RefreshCurrentView();
        }

        public void OpenFor(InventoryController inv, PlayerEquipmentController equip)
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            if (inventoryController != null && inventoryController != inv)
            {
                inventoryController.InventoryUpdated -= OnInventoryUpdated;
            }

            inventoryController = inv ?? InventoryController.LocalInstance ?? FindFirstObjectByType<InventoryController>();
            _equipmentController = equip ?? PlayerEquipmentController.LocalInstance ?? FindFirstObjectByType<PlayerEquipmentController>();

            if (inventoryController != null)
            {
                inventoryController.InventoryUpdated += OnInventoryUpdated;
            }

            Open();
            RefreshCurrentView();
            Canvas.ForceUpdateCanvases();
        }

        private void EnsureController()
        {
            if (inventoryController == null)
            {
                inventoryController = InventoryController.LocalInstance ?? InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
                if (inventoryController == null)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null) inventoryController = player.GetComponent<InventoryController>();
                }
            }

            if (_equipmentController == null)
            {
                _equipmentController = PlayerEquipmentController.LocalInstance ?? FindFirstObjectByType<PlayerEquipmentController>();
                if (_equipmentController == null)
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null) _equipmentController = player.GetComponent<PlayerEquipmentController>();
                }
            }
        }

    }
}
