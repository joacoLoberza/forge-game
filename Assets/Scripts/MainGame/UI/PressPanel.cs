using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using ForjaGame.UI.Core;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    /// <summary>
    /// UI Panel for the Mechanical Press with an inventory Bloom ScrollView.
    /// Displays Pressing Difficulty (Level 1 to 10) and handles mashing, extraction, and retrieval.
    /// </summary>
    public partial class PressPanel : UIPanelBase
    {
        [Header("Controllers")]
        [SerializeField] private PressController pressController;
        [SerializeField] private PressStationController stationController;

        [Header("Bloom Selection (ScrollView)")]
        [SerializeField] private GameObject bloomSelectionSection;
        [SerializeField] private Transform bloomsContainer;
        [SerializeField] private GameObject emptyBloomsNotice;

        [Header("Pressing Action")]
        [SerializeField] private GameObject pressActionSection;
        [SerializeField] private Button mashButton;
        [SerializeField] private Slider pressProgressSlider;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text bloomInfoText;
        [SerializeField] private Button retrieveBloomButton;
        [SerializeField] private Button extractIngotButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button closeButton;

        private IngotData _readyIngot;

        protected override void Awake()
        {
            base.Awake();
            EnsureReferences();
            if (mashButton != null) BindPressDown(mashButton, OnMashClicked);
            if (retrieveBloomButton != null) BindPressDown(retrieveBloomButton, OnRetrieveBloomClicked);
            if (extractIngotButton != null) BindPressDown(extractIngotButton, OnExtractIngotClicked);
            if (closeButton != null) BindPressDown(closeButton, Close);
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (pressController != null)
            {
                pressController.PressProgressChanged += OnProgressChanged;
                pressController.PressCompleted += OnPressCompleted;
                pressController.PressReset += OnPressReset;
            }
            UpdateDisplay();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (pressController != null)
            {
                pressController.PressProgressChanged -= OnProgressChanged;
                pressController.PressCompleted -= OnPressCompleted;
                pressController.PressReset -= OnPressReset;
            }
        }

        protected override void OnOpened() { base.OnOpened(); EnsureReferences(); UpdateDisplay(); }
        protected override void OnClosed() { base.OnClosed(); stationController?.ReleaseStation(); }

        private void Update()
        {
            if (!IsOpen || pressController == null || !pressController.HasBloom || pressController.IsCompleted) return;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) pressController.PushPress();
        }

        private void OnMashClicked()
        {
            if (pressController != null && pressController.HasBloom && !pressController.IsCompleted) pressController.PushPress();
        }

        private void OnRetrieveBloomClicked()
        {
            if (pressController == null || !pressController.HasBloom || pressController.IsCompleted) return;

            BloomData retrieved = pressController.CancelAndRetrieveBloom();
            if (retrieved != null)
            {
                GetCurrentPlayerInventory()?.AddBloom(retrieved);
                SetStatus($"Bloom '{retrieved.DisplayName}' returned to inventory.");
            }
            UpdateDisplay();
        }

        private void OnPressCompleted(IngotData ingot)
        {
            _readyIngot = ingot;
            SetStatus($"Pressing completed! Extract your {ingot.DisplayName}.");
            UpdateDisplay();
        }

        private void OnPressReset() => UpdateDisplay();

        private void OnExtractIngotClicked()
        {
            if (pressController == null) return;
            IngotData extracted = pressController.ExtractIngot();
            if (extracted != null)
            {
                GetCurrentPlayerInventory()?.AddIngot(extracted);
                SetStatus($"{extracted.DisplayName} stored in inventory!");
                _readyIngot = null;
            }
            UpdateDisplay();
        }

        private void OnProgressChanged(float progress)
        {
            if (pressProgressSlider != null) pressProgressSlider.value = progress;
            if (progressText != null) progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        }

        private void UpdateDisplay()
        {
            if (pressController == null) return;
            OnProgressChanged(pressController.PressProgress);
            bool hasBloom = pressController.HasBloom;
            bool isCompleted = pressController.IsCompleted;

            if (bloomSelectionSection != null && pressActionSection != null)
            {
                bloomSelectionSection.SetActive(!hasBloom);
                pressActionSection.SetActive(hasBloom);
            }

            RefreshBloomsList();

            if (!hasBloom)
            {
                if (bloomInfoText != null) bloomInfoText.text = "No Bloom placed on piston.";
                if (mashButton != null) mashButton.gameObject.SetActive(false);
                if (retrieveBloomButton != null) retrieveBloomButton.gameObject.SetActive(false);
                if (extractIngotButton != null) extractIngotButton.gameObject.SetActive(false);
            }
            else
            {
                if (mashButton != null) mashButton.gameObject.SetActive(!isCompleted);
                if (retrieveBloomButton != null) retrieveBloomButton.gameObject.SetActive(!isCompleted);
                if (extractIngotButton != null) extractIngotButton.gameObject.SetActive(isCompleted);
                if (bloomInfoText != null && pressController.CurrentBloom != null)
                {
                    var bloom = pressController.CurrentBloom;
                    bloomInfoText.text = $"<b>{bloom.DisplayName}</b> ({bloom.SizeLabel})\nPurity: {bloom.PurityPercentage}% | Pressing Difficulty: <b>Level {bloom.DifficultyLevel}</b>";
                }
            }
        }

        private void EnsureReferences()
        {
            if (pressController == null) pressController = FindFirstObjectByType<PressController>(FindObjectsInactive.Include);
            if (stationController == null) stationController = FindFirstObjectByType<PressStationController>(FindObjectsInactive.Include);
        }

        private InventoryController GetCurrentPlayerInventory()
        {
            if (stationController != null && stationController.CurrentInteractor != null)
                return stationController.CurrentInteractor.GetComponentInParent<InventoryController>() ??
                       stationController.CurrentInteractor.GetComponentInChildren<InventoryController>();

            return GameObject.FindWithTag("Player")?.GetComponent<InventoryController>() ?? FindFirstObjectByType<InventoryController>();
        }

        private void SetStatus(string message) { if (statusText != null) statusText.text = message; }
    }
}
