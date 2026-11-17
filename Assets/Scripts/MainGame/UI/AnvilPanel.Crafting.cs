using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.Forge;
using ForjaGame.Inventory;
using ForjaGame.Data;

namespace ForjaGame.UI
{
    public enum AnvilCraftingStep
    {
        WeaponSelection,
        IngotSelection,
        IngotConformation,
        HandleSelection
    }

    /// <summary>
    /// Partial class for AnvilPanel: Sequential weapon crafting selection.
    /// Supports IngotConformation preview with instant ingot return to inventory on Back.
    /// </summary>
    public partial class AnvilPanel
    {
        [Header("Sequential Steps Containers")]
        [SerializeField] private GameObject step1WeaponSelection;
        [SerializeField] private GameObject step2IngotSelection;
        [SerializeField] private GameObject stepIngotConformation;
        [SerializeField] private GameObject step3HandleSelection;

        [Header("Navigation Buttons")]
        [SerializeField] private Button backToWeaponSelectionButton;
        [SerializeField] private Button backFromConformationButton;
        [SerializeField] private Button startForgingButton;

        [Header("Sprite Slots for Journal Illustrations")]
        [SerializeField] private Sprite defaultIngotSprite;
        [SerializeField] private Sprite defaultHandleSprite;
        [SerializeField] private HandleDefinitionSO[] handleDefinitions;

        [Header("Real Inventory Selection (ScrollViews)")]
        [SerializeField] private Transform ingotsContainer;
        [SerializeField] private GameObject emptyIngotsNotice;
        [SerializeField] private Transform handlesContainer;
        [SerializeField] private GameObject emptyHandlesNotice;

        public GameObject Step1WeaponSelection => step1WeaponSelection;
        public GameObject Step2IngotSelection => step2IngotSelection;
        public GameObject StepIngotConformation => stepIngotConformation;
        public GameObject Step3HandleSelection => step3HandleSelection;

        private IngotData _selectedInventoryIngot;
        private HandleData _selectedInventoryHandle;
        private AnvilCraftingStep _currentStep = AnvilCraftingStep.WeaponSelection;

        public void SetStep(AnvilCraftingStep step)
        {
            EnsureStepContainers();
            if (anvilController != null && anvilController.HasMeshBeenModified && (step == AnvilCraftingStep.IngotSelection || step == AnvilCraftingStep.WeaponSelection))
                step = AnvilCraftingStep.IngotConformation;
            _currentStep = step;

            if (step1WeaponSelection != null) step1WeaponSelection.SetActive(step == AnvilCraftingStep.WeaponSelection);
            if (step2IngotSelection != null) step2IngotSelection.SetActive(step == AnvilCraftingStep.IngotSelection);
            if (stepIngotConformation != null) stepIngotConformation.SetActive(step == AnvilCraftingStep.IngotConformation);
            if (step3HandleSelection != null) step3HandleSelection.SetActive(step == AnvilCraftingStep.HandleSelection);
            ToggleFallbackComponents(step);

            if (step == AnvilCraftingStep.IngotSelection) RefreshIngotsList();
            else if (step == AnvilCraftingStep.IngotConformation) RefreshConformationView();
            else if (step == AnvilCraftingStep.HandleSelection) RefreshHandlesList();
        }

        private void ToggleFallbackComponents(AnvilCraftingStep step)
        {
            var ingotsScroll = ingotsContainer != null ? ingotsContainer.GetComponentInParent<ScrollRect>(true) : null;
            if (ingotsScroll != null && (step2IngotSelection == null || !ingotsScroll.transform.IsChildOf(step2IngotSelection.transform)))
                ingotsScroll.gameObject.SetActive(step == AnvilCraftingStep.IngotSelection);

            var handlesScroll = handlesContainer != null ? handlesContainer.GetComponentInParent<ScrollRect>(true) : null;
            if (handlesScroll != null && (step3HandleSelection == null || !handlesScroll.transform.IsChildOf(step3HandleSelection.transform)))
                handlesScroll.gameObject.SetActive(step == AnvilCraftingStep.HandleSelection);

            if (swordButton != null && (step1WeaponSelection == null || !swordButton.transform.IsChildOf(step1WeaponSelection.transform)))
            {
                bool isStep1 = step == AnvilCraftingStep.WeaponSelection;
                swordButton.gameObject.SetActive(isStep1);
                if (axeButton != null) axeButton.gameObject.SetActive(isStep1);
                if (hammerButton != null) hammerButton.gameObject.SetActive(isStep1);
            }
        }

        public void EnsureStepContainers()
        {
            if (step1WeaponSelection == null) step1WeaponSelection = FindChild("WeaponSelection") ?? FindChild("Step1");
            if (step2IngotSelection == null) step2IngotSelection = FindChild("IngotSelection") ?? FindChild("Step2");
            if (stepIngotConformation == null) stepIngotConformation = FindChild("IngotConformation") ?? FindChild("Conformation");
            if (step3HandleSelection == null) step3HandleSelection = FindChild("HandleSelection") ?? FindChild("Step3");
            EnsureConformationButtons();
        }

        private void EnsureConformationButtons()
        {
            if (stepIngotConformation == null) return;
            var buttons = stepIngotConformation.GetComponentsInChildren<Button>(true);
            if (backFromConformationButton == null)
                foreach (var b in buttons) { string n = b.name.ToLower(); if (n.Contains("back") || n.Contains("atras") || n.Contains("regresar")) { backFromConformationButton = b; break; } }
            if (startForgingButton == null)
                foreach (var b in buttons) if (b != backFromConformationButton) { startForgingButton = b; break; }

            if (backFromConformationButton != null) BindPressDown(backFromConformationButton, OnBackFromConformationClicked);
            if (startForgingButton != null) BindPressDown(startForgingButton, Close);
        }

        private GameObject FindChild(string tag)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name.ToLower().Contains(tag.ToLower())) return t.gameObject;
            return null;
        }

        public void RefreshCurrentStep()
        {
            if (_currentStep == AnvilCraftingStep.IngotSelection) RefreshIngotsList();
            else if (_currentStep == AnvilCraftingStep.IngotConformation) RefreshConformationView();
            else if (_currentStep == AnvilCraftingStep.HandleSelection) RefreshHandlesList();
        }

        public void OnWeaponTypeChosen(WeaponType weaponType)
        {
            _selectedWeapon = weaponType;
            _selectedInventoryIngot = null;
            ForjaGame.Audio.ForgeAudioSystem.PlayItemSelect();
            SetStep(AnvilCraftingStep.IngotSelection);
        }

        public void SelectIngot(IngotData ingot)
        {
            if (ingot == null) return;
            _selectedInventoryIngot = ingot;
            _selectedIngotSize = ingot.size;

            GetCurrentPlayerInventory()?.RemoveIngot(ingot);
            anvilController?.PlaceIngot(ingot, _selectedWeapon);
            ForjaGame.Audio.ForgeAudioSystem.PlayItemSelect();

            SetStatus($"{ingot.DisplayName} placed on anvil. Ready to hammer!");
            Close();
        }

        public void OnBackFromConformationClicked()
        {
            if (anvilController != null && anvilController.HasIngotOnAnvil)
            {
                if (anvilController.HasMeshBeenModified)
                {
                    SetStatus("Mesh modified. Continue forging or complete the piece.");
                    return;
                }
                IngotData retrieved = anvilController.CancelAndRetrieveIngot();
                if (retrieved != null)
                    GetCurrentPlayerInventory()?.AddIngot(retrieved);
            }
            _selectedInventoryIngot = null;
            SetStep(AnvilCraftingStep.IngotSelection);
            SetStatus("Ingot returned to inventory. Choose another ingot.");
        }

        private void RefreshConformationView()
        {
            if (_selectedInventoryIngot != null)
                SetStatus($"Ingot {_selectedInventoryIngot.DisplayName} on anvil. Click Back to change it.");

            if (stepIngotConformation == null) return;
            foreach (var t in stepIngotConformation.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<Button>() == null)
                {
                    t.text = $"{_selectedInventoryIngot?.DisplayName} ({_selectedInventoryIngot?.SizeLabel})\nWeapon: {_selectedWeapon}";
                    break;
                }
            }
        }

        public void OnHandleChosen(HandleData handle)
        {
            if (handle == null || assemblyController == null) return;
            InventoryController inventory = GetCurrentPlayerInventory();
            inventory?.RemoveHandle(handle);

            if (handle.customMesh == null && anvilController?.CarvedHandleMesh != null)
                handle.customMesh = anvilController.CarvedHandleMesh;

            IngotData ingot = _selectedInventoryIngot ?? anvilController?.CurrentIngot ?? new IngotData(_selectedIngotSize, new AlloyComposition(), 1.0f);
            Mesh bladeMesh = anvilController != null ? anvilController.ForgedBladeMesh : null;
            float massRatio = anvilController != null ? anvilController.RemainingMassRatio : 1.0f;
            WeaponStats weapon = assemblyController.AssembleWeapon(ingot, _selectedWeapon, handle, bladeMesh, massRatio);

            if (weapon != null)
            {
                if (weapon.customHandleMesh == null && handle.customMesh != null)
                    weapon.customHandleMesh = handle.customMesh;
                inventory?.AddWeapon(weapon);
                ForjaGame.Audio.ForgeAudioSystem.PlayWeaponClaimed(transform.position);
                SetStatus($"{weapon.DisplayName} completed and added to inventory!");
            }

            anvilController?.ClearAnvilSurface();
            _selectedInventoryIngot = null;
            SetStep(AnvilCraftingStep.WeaponSelection);
            Close();
            stationController?.ReleaseStation();
            FindFirstObjectByType<ForjaGame.Core.GameStateController>()?.TryChangeState(ForjaGame.Core.GameState.Exploration);
        }
    }
}
