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
    /// Panel de interfaz de la Prensa Mecánica: permite elegir la Lupa (colocándola en la parte inferior
    /// de la prensa en 3D), prensar repetidamente [ESPACIO] y recoger el Lingote 3D resultante.
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

        private PressMechanicalVisual _pressVisual;
        private IngotData _readyIngot;

        public TMP_Text BloomInfoText => bloomInfoText;
        public TMP_Text ProgressText => progressText;
        public TMP_Text StatusText => statusText;
        public Slider PressProgressSlider => pressProgressSlider;
        public Button MashButton => mashButton;
        public Button RetrieveBloomButton => retrieveBloomButton;
        public Button ExtractIngotButton => extractIngotButton;
        public Button CloseButton => closeButton;

        protected override void Awake()
        {
            base.Awake();
            EnsureReferences();
            if (pressProgressSlider != null) pressProgressSlider.interactable = false;
            if (mashButton != null)
            {
                BindPressDown(mashButton, OnMashClicked);
                SetBtnText(mashButton, "PRENSAR [ESPACIO]");
            }
            if (retrieveBloomButton != null)
            {
                BindPressDown(retrieveBloomButton, OnRetrieveBloomClicked);
                SetBtnText(retrieveBloomButton, "Retirar Lupa");
            }
            if (extractIngotButton != null)
            {
                BindPressDown(extractIngotButton, OnExtractIngotClicked);
                SetBtnText(extractIngotButton, "Recoger Lingote");
            }
            if (closeButton != null) BindPressDown(closeButton, Close);
        }

        private static void SetBtnText(Button b, string txt)
        {
            var t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
            if (t != null) t.text = txt;
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
            if (_pressVisual != null)
                _pressVisual.IngotClicked += OnExtractIngotClicked;

            ForgeCanvasThemeStyler.StylePressPanel(this);
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
            if (_pressVisual != null)
                _pressVisual.IngotClicked -= OnExtractIngotClicked;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            EnsureReferences();
            ForgeCanvasThemeStyler.StylePressPanel(this);
            UpdateDisplay();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            stationController?.ReleaseStation();
        }

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
                SetStatus($"Lupa '{retrieved.DisplayName}' devuelta al inventario.");
            }
            UpdateDisplay();
        }

        private void OnPressCompleted(IngotData ingot)
        {
            _readyIngot = ingot;
            SetStatus($"¡Prensado completado! El Lingote 3D está en la base: pulsa 'Recoger Lingote'.");
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
                SetStatus($"¡{extracted.DisplayName} guardado en el inventario!");
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
                if (bloomInfoText != null)
                    bloomInfoText.text = "<b>Ninguna Lupa cargada</b>  ·  Selecciona una Lupa en la lista izquierda para consultar su dificultad y detalles.";
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
                    string stateLabel = isCompleted
                        ? "<color=#1B6E32><b>¡LINGOTE 3D FORJADO!</b></color>"
                        : $"Dificultad: <color=#8B3A0E><b>Nivel {bloom.DifficultyLevel}/10</b></color>";
                    string compDesc = bloom.composition != null ? bloom.composition.GetCompositionDescription() : "";
                    bloomInfoText.text = $"<b>{bloom.DisplayName}</b> ({bloom.SizeLabel})  ·  {stateLabel}\nPureza: <b>{bloom.PurityPercentage}%</b>  |  Impurezas de Horno: <b>{bloom.ImpurityPercentage}%</b>  |  {compDesc}";
                }
            }
        }

        private void EnsureReferences()
        {
            if (pressController == null) pressController = FindFirstObjectByType<PressController>(FindObjectsInactive.Include);
            if (stationController == null) stationController = FindFirstObjectByType<PressStationController>(FindObjectsInactive.Include);
            if (_pressVisual == null && stationController != null) _pressVisual = stationController.GetComponent<PressMechanicalVisual>();
            if (_pressVisual == null) _pressVisual = FindFirstObjectByType<PressMechanicalVisual>(FindObjectsInactive.Include);
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

        private void SetStatus(string message) { if (statusText != null) statusText.text = message; }
    }
}
