using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI.Core;
using ForjaGame.Forge;
using ForjaGame.Inventory;
using ForjaGame.Data;

namespace ForjaGame.UI
{
    /// <summary>
    /// UI Panel for the Anvil: Coordinates step-by-step weapon crafting.
    /// Listens to inventory changes to refresh ScrollViews dynamically.
    /// </summary>
    public partial class AnvilPanel : UIPanelBase
    {
        [Header("Controllers")]
        [SerializeField] private AnvilStationController stationController;
        [SerializeField] private WeaponAssemblyController assemblyController;
        [SerializeField] private AnvilController anvilController;

        [Header("Weapon Selection (Step 1 Buttons)")]
        [SerializeField] private Button swordButton;
        [SerializeField] private Button axeButton;
        [SerializeField] private Button hammerButton;

        [Header("Actions and Navigation")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button closeButton;

        private WeaponType _selectedWeapon = WeaponType.Sword;
        private HandleLength _selectedHandle = HandleLength.Medium;
        private ItemSize _selectedIngotSize = ItemSize.Medium;
        private MetalType _selectedMetal = MetalType.Iron;
        private InventoryController _cachedInventory;

        protected override void Awake()
        {
            base.Awake();

            if (assemblyController == null)
                assemblyController = FindFirstObjectByType<WeaponAssemblyController>(FindObjectsInactive.Include);

            if (stationController == null)
                stationController = FindFirstObjectByType<AnvilStationController>(FindObjectsInactive.Include);

            if (anvilController == null)
                anvilController = FindFirstObjectByType<AnvilController>(FindObjectsInactive.Include);

            // Step 1: Weapon selection
            if (swordButton != null) BindPressDown(swordButton, () => OnWeaponTypeChosen(WeaponType.Sword));
            if (axeButton != null) BindPressDown(axeButton, () => OnWeaponTypeChosen(WeaponType.Axe));
            if (hammerButton != null) BindPressDown(hammerButton, () => OnWeaponTypeChosen(WeaponType.Hammer));

            // Navigation buttons
            if (backToWeaponSelectionButton != null)
                BindPressDown(backToWeaponSelectionButton, () => SetStep(AnvilCraftingStep.WeaponSelection));

            if (backFromConformationButton != null)
                BindPressDown(backFromConformationButton, OnBackFromConformationClicked);

            if (startForgingButton != null)
                BindPressDown(startForgingButton, Close);

            if (closeButton != null) BindPressDown(closeButton, Close);
        }

        private void OnEnable()
        {
            if (anvilController != null)
                anvilController.ForgingCompleted += OnForgingAndQuenchingCompleted;

            _cachedInventory = GetCurrentPlayerInventory();
            if (_cachedInventory != null)
                _cachedInventory.InventoryUpdated += OnInventoryUpdated;

            if (anvilController != null && anvilController.HasIngotOnAnvil && anvilController.IsQuenched)
                SetStep(AnvilCraftingStep.HandleSelection);
            else if (anvilController != null && anvilController.HasIngotOnAnvil && !anvilController.IsQuenched && anvilController.CurrentHits == 0)
                SetStep(AnvilCraftingStep.IngotConformation);
            else if (_currentStep != AnvilCraftingStep.HandleSelection && _currentStep != AnvilCraftingStep.IngotSelection)
                SetStep(AnvilCraftingStep.WeaponSelection);
            else
                RefreshCurrentStep();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (anvilController != null)
                anvilController.ForgingCompleted -= OnForgingAndQuenchingCompleted;

            if (_cachedInventory != null)
                _cachedInventory.InventoryUpdated -= OnInventoryUpdated;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            EnsurePanelStyling();
            if (anvilController != null && anvilController.HasIngotOnAnvil && anvilController.IsQuenched)
                SetStep(AnvilCraftingStep.HandleSelection);
            else if (anvilController != null && anvilController.HasIngotOnAnvil && !anvilController.IsQuenched && anvilController.CurrentHits == 0)
                SetStep(AnvilCraftingStep.IngotConformation);
            else if (_currentStep != AnvilCraftingStep.HandleSelection && _currentStep != AnvilCraftingStep.IngotSelection)
                SetStep(AnvilCraftingStep.WeaponSelection);
            else
                RefreshCurrentStep();
        }

        private void EnsurePanelStyling()
        {
            var img = GetComponent<Image>();
            if (img == null) img = gameObject.AddComponent<Image>();
            if (img.color == Color.white || img.color.a < 0.2f)
            {
                img.color = new Color(0.11f, 0.12f, 0.15f, 0.96f);
            }
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            if (anvilController == null || !anvilController.IsForging)
            {
                stationController?.ReleaseStation();
                var gs = FindFirstObjectByType<ForjaGame.Core.GameStateController>();
                gs?.TryChangeState(ForjaGame.Core.GameState.Exploration);
            }
        }

        private void OnInventoryUpdated()
        {
            if (IsOpen) RefreshCurrentStep();
        }

        private void OnForgingAndQuenchingCompleted()
        {
            if (anvilController != null && (anvilController.IsCarvingHandle || anvilController.SelectedWeaponType == WeaponType.Axe || anvilController.SelectedWeaponType == WeaponType.Hammer || anvilController.SelectedWeaponType == WeaponType.Sword))
                return; // Mango artesanal 3D en la mesa del yunque

            Open();
            SetStep(AnvilCraftingStep.HandleSelection);
            SetStatus("Blade quenched! Select a handle to finish assembling your weapon.");
        }

        public void SelectWeapon(WeaponType weapon) => OnWeaponTypeChosen(weapon);
        public void SelectHandle(HandleLength handle) => _selectedHandle = handle;
        public void SelectIngotSize(ItemSize size) => _selectedIngotSize = size;
        public void SelectMetal(MetalType metal) => _selectedMetal = metal;

        public void OpenToIngotSelection()
        {
            _currentStep = AnvilCraftingStep.IngotSelection;
            gameObject.SetActive(true);
            Open();
            SetStep(AnvilCraftingStep.IngotSelection);
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

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void RefreshHandlesList()
        {
            if (handlesContainer == null) return;
            ForgeScrollViewStyler.ConfigureFixedScrollView(handlesContainer);
            foreach (Transform child in handlesContainer) Destroy(child.gameObject);

            InventoryController inventory = GetCurrentPlayerInventory();
            IReadOnlyList<HandleData> handles = inventory != null ? inventory.Handles : null;

            bool hasHandles = handles != null && handles.Count > 0;
            if (emptyHandlesNotice != null) emptyHandlesNotice.SetActive(!hasHandles);
            if (!hasHandles)
            {
                if (emptyHandlesNotice == null)
                    ForgeScrollViewStyler.CreateNoticeEntry(handlesContainer, "No hay mangos en el inventario.");
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
                HandleData handle = kvp.rep;
                int count = kvp.count;
                string spd = handle.speedModifier >= 0 ? $"+{handle.speedModifier * 100:F0}%" : $"{handle.speedModifier * 100:F0}%";
                string countSuffix = count > 1 ? $" x{count}" : "";
                string label = $"{handle.DisplayName}{countSuffix} (Alcance x{handle.rangeModifier:F2}, Vel {spd})";

                Sprite icon = GetHandleSprite(handle.length);
                ForgeScrollViewStyler.CreateRowEntry(handlesContainer, label, icon, new Color(0.6f, 0.45f, 0.3f, 1f), false, () => OnHandleChosen(handle));
            }
        }

        private Sprite GetHandleSprite(HandleLength length)
        {
            if (handleDefinitions != null)
            {
                for (int i = 0; i < handleDefinitions.Length; i++)
                    if (handleDefinitions[i] != null && handleDefinitions[i].length == length && handleDefinitions[i].sketchSprite != null)
                        return handleDefinitions[i].sketchSprite;
            }
            return defaultHandleSprite ?? ForgeSpriteDatabase.Instance?.GetHandleSprite(length);
        }
    }
}
