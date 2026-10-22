using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI.Core;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    /// <summary>
    /// Panel principal de interfaz del Horno.
    /// Coordinador de ciclo de vida, eventos y entradas de usuario (SOLID).
    /// Dividido en clases parciales modulares (< 250 líneas) para UI térmica, de minerales y progreso.
    /// </summary>
    public partial class FurnacePanel : UIPanelBase
    {
        [Header("Furnace Controller")]
        [SerializeField] private FurnaceController furnaceController;
        [SerializeField] private FurnaceStationController stationController;

        [Header("Temperature Controls")]
        [SerializeField] private Slider temperatureSlider;
        [SerializeField] private RectTransform rangeIndicator;
        [SerializeField] private RectTransform sweetSpotIndicator;
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private TMP_Text targetRangeText;
        [SerializeField] private Button addCoalButton;

        [Header("Ore Controls (ScrollView)")]
        [SerializeField] private Transform oresContainer;
        [SerializeField] private GameObject emptyOresNotice;
        [SerializeField] private Button extractOreButton;
        [SerializeField] private Button startSmeltButton;
        [SerializeField] private TMP_Text oreCountText;

        [Header("Progress and Extraction")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Button extractBloomButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button closeButton;

        // Shared private state between partial classes
        private BloomData _readyBloom;
        private readonly List<UnityEngine.UI.Image> _thermalCellImages = new();
        private readonly Dictionary<MetalType, RectTransform> _mineralRangeBars = new();
        private readonly Dictionary<int, RectTransform> _sizeMarkIndicators = new();
        private readonly Dictionary<int, UnityEngine.UI.Image> _sizeMarkImages = new();

        // Render cache for 0 GC Alloc in Update
        private int _lastDisplayedOreCount = -1;
        private int _lastDisplayedImpurity = -1;
        private int _lastDisplayedTempInt = -1;
        private string _lastStatusMessage = string.Empty;

        protected override void Awake()
        {
            base.Awake();

            if (furnaceController == null)
                furnaceController = FindFirstObjectByType<FurnaceController>(FindObjectsInactive.Include);

            if (stationController == null)
                stationController = FindFirstObjectByType<FurnaceStationController>(FindObjectsInactive.Include);

            // Button listener bindings
            if (addCoalButton != null) BindPressDown(addCoalButton, OnAddCoalClicked);
            if (extractOreButton != null) BindPressDown(extractOreButton, OnExtractLastOreClicked);

            if (startSmeltButton != null)
            {
                BindPressDown(startSmeltButton, OnExtractLastOreClicked);
                TMP_Text btnText = startSmeltButton.GetComponentInChildren<TMP_Text>();
                if (btnText != null) btnText.text = "Extract Ore";
            }

            if (extractBloomButton != null) BindPressDown(extractBloomButton, OnExtractBloomClicked);
            if (closeButton != null) BindPressDown(closeButton, Close);

            SetupSliders();
        }

        private void SetupSliders()
        {
            if (temperatureSlider != null)
            {
                temperatureSlider.minValue = 0f;
                temperatureSlider.maxValue = (furnaceController != null && furnaceController.MaxTemperature > 0f) ? furnaceController.MaxTemperature : 1600f;
                SetupRangeIndicator();
            }

            if (progressSlider == null)
            {
                Slider[] sliders = GetComponentsInChildren<Slider>(true);
                foreach (var s in sliders)
                {
                    if (s != temperatureSlider)
                    {
                        progressSlider = s;
                        break;
                    }
                }
            }

            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                SetupProgressSizeMarks();
            }
        }

        private void OnEnable()
        {
            SetupSliders();

            if (furnaceController != null)
            {
                furnaceController.TemperatureChanged += OnTemperatureChanged;
                furnaceController.SmeltProgressChanged += OnProgressChanged;
                furnaceController.SmeltCompleted += OnSmeltCompleted;

                if (furnaceController.UnclaimedBloom != null)
                {
                    _readyBloom = furnaceController.UnclaimedBloom;
                }
            }
            UpdateDisplay();
            RefreshOresList();
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            RefreshOresList();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            stationController?.ReleaseStation();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (furnaceController != null)
            {
                furnaceController.TemperatureChanged -= OnTemperatureChanged;
                furnaceController.SmeltProgressChanged -= OnProgressChanged;
                furnaceController.SmeltCompleted -= OnSmeltCompleted;
            }
        }

        private void Update()
        {
            if (furnaceController == null || !IsOpen) return;

            // Atajos de forjador DENTRO de la UI del horno (exclusivos de la UI):
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                // [Espacio]: Mantener para abrir la compuerta de ventilación (-calor)
                if (keyboard.spaceKey.isPressed)
                {
                    furnaceController.VentFurnace(1.5f);
                    SetStatus("[Vent Opened] Venting excess heat...");
                }

                // [Shift]: Press to add coal (+heat)
                if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                {
                    OnAddCoalClicked();
                }
            }

            UpdateDisplay();
        }

        private void SetStatus(string message)
        {
            if (statusText != null && _lastStatusMessage != message)
            {
                _lastStatusMessage = message;
                statusText.text = message;
            }
        }
    }
}
