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
    /// Coordina selección previa 3D de minerales, botón "Ingresar minerales", calor y extracción de Lupa.
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
        [SerializeField] private Button insertOresButton;
        [SerializeField] private TMP_Text oreCountText;

        [Header("Progress and Extraction")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Button extractBloomButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button closeButton;

        private FurnaceVisualController _visualController;
        private BloomData _readyBloom;
        private readonly List<MetalType> _stagedOres = new();
        private readonly List<Image> _thermalCellImages = new();
        private readonly Dictionary<MetalType, RectTransform> _mineralRangeBars = new();
        private readonly Dictionary<int, RectTransform> _sizeMarkIndicators = new();
        private readonly Dictionary<int, Image> _sizeMarkImages = new();

        private int _lastDisplayedOreHash = int.MinValue;
        private int _lastDisplayedTempInt = -1;
        private string _lastStatusMessage = string.Empty;

        public IReadOnlyList<MetalType> StagedOres => _stagedOres;
        public Slider TemperatureSlider => temperatureSlider;
        public Slider ProgressSlider => progressSlider;
        public TMP_Text TemperatureText => temperatureText;
        public TMP_Text TargetRangeText => targetRangeText;
        public TMP_Text OreCountText => oreCountText;
        public TMP_Text StatusText => statusText;
        public Button AddCoalButton => addCoalButton;
        public Button ExtractOreButton => extractOreButton;
        public Button ExtractBloomButton => extractBloomButton;
        public Button InsertOresButton => insertOresButton;
        public Button CloseButton => closeButton;

        protected override void Awake()
        {
            base.Awake();
            ResolveControllers();

            var bgImage = GetComponent<Image>();
            if (bgImage != null) bgImage.enabled = false;

            if (extractOreButton != null && extractOreButton == extractBloomButton) extractOreButton = null;

            EnsureInsertOresButtonExists();
            EnsureExtractOreButtonExists();
            EnsureBellowsButtonExists();

            if (addCoalButton != null)
            {
                BindPressDown(addCoalButton, OnAddCoalClicked);
                SetButtonLabel(addCoalButton, "+ Carbón [Shift]");
            }
            if (extractOreButton != null)
            {
                BindPressDown(extractOreButton, OnExtractLastOreClicked);
                SetButtonLabel(extractOreButton, "Quitar Mineral");
            }
            if (insertOresButton != null)
            {
                BindPressDown(insertOresButton, OnInsertStagedOresClicked);
                SetButtonLabel(insertOresButton, "Ingresar minerales");
            }
            if (extractBloomButton != null)
            {
                BindPressDown(extractBloomButton, OnExtractBloomClicked);
                SetButtonLabel(extractBloomButton, "Sacar Lupa (Inferior)");
            }
            if (closeButton != null) BindPressDown(closeButton, Close);

            SetupSliders();
        }

        private void ResolveControllers()
        {
            if (furnaceController == null)
                furnaceController = FindFirstObjectByType<FurnaceController>(FindObjectsInactive.Include);
            if (stationController == null)
                stationController = FindFirstObjectByType<FurnaceStationController>(FindObjectsInactive.Include);
            if (_visualController == null && stationController != null)
                _visualController = stationController.GetComponent<FurnaceVisualController>();
            if (_visualController == null)
                _visualController = FindFirstObjectByType<FurnaceVisualController>(FindObjectsInactive.Include);
        }

        private void EnsureInsertOresButtonExists()
        {
            if (insertOresButton == null && startSmeltButton != null && startSmeltButton != extractOreButton && startSmeltButton != extractBloomButton)
                insertOresButton = startSmeltButton;
            if (insertOresButton != null) return;

            Transform existing = transform.Find("InsertOresButton");
            if (existing == null) existing = transform.Find("LeftStationCard/ParchmentBody/InsertOresButton");
            if (existing != null && existing.TryGetComponent<Button>(out var foundBtn))
            {
                insertOresButton = foundBtn;
                return;
            }
            insertOresButton = CreateActionButton("InsertOresButton", "Ingresar minerales");
        }

        private void EnsureExtractOreButtonExists()
        {
            if (extractOreButton != null) return;
            Transform existing = transform.Find("ExtractOreButton_Dedicated");
            if (existing == null) existing = transform.Find("LeftStationCard/ParchmentBody/ExtractOreButton_Dedicated");
            if (existing != null && existing.TryGetComponent<Button>(out var foundBtn))
            {
                extractOreButton = foundBtn;
                return;
            }
            extractOreButton = CreateActionButton("ExtractOreButton_Dedicated", "Quitar Mineral");
        }

        private Button CreateActionButton(string objName, string labelText)
        {
            GameObject btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(transform, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 44f);

            var labelObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(btnObj.transform, false);
            var lrt = labelObj.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = labelObj.GetComponent<TextMeshProUGUI>();
            tmp.text = labelText;
            tmp.fontSize = 16f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            return btnObj.GetComponent<Button>();
        }

        private static void SetButtonLabel(Button btn, string label)
        {
            var tmp = btn != null ? btn.GetComponentInChildren<TMP_Text>(true) : null;
            if (tmp != null) tmp.text = label;
        }

        private void SetupSliders()
        {
            if (temperatureSlider != null)
            {
                temperatureSlider.minValue = 0f;
                temperatureSlider.maxValue = (furnaceController != null && furnaceController.MaxTemperature > 0f) ? furnaceController.MaxTemperature : 1600f;
                temperatureSlider.interactable = false;
                SetupRangeIndicator();
            }

            if (progressSlider == null)
            {
                foreach (var s in GetComponentsInChildren<Slider>(true))
                    if (s != temperatureSlider) { progressSlider = s; break; }
            }

            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.interactable = false;
                SetupProgressSizeMarks();
            }
        }

        private void OnEnable()
        {
            ResolveControllers();
            SetupSliders();
            if (furnaceController != null)
            {
                furnaceController.TemperatureChanged += OnTemperatureChanged;
                furnaceController.SmeltProgressChanged += OnProgressChanged;
                furnaceController.SmeltCompleted += OnSmeltCompleted;
                if (furnaceController.UnclaimedBloom != null) _readyBloom = furnaceController.UnclaimedBloom;
            }
            if (_visualController != null)
                _visualController.BottomBloomClicked += OnExtractBloomClicked;

            ForgeCanvasThemeStyler.StyleFurnacePanel(this);
            _lastDisplayedOreHash = int.MinValue;
            UpdateDisplay();
            RefreshOresList();
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            ForgeCanvasThemeStyler.StyleFurnacePanel(this);
            RefreshOresList();
            UpdateDisplay();
        }

        protected override void OnClosed()
        {
            RefundStagedOresToInventory();
            base.OnClosed();
            stationController?.ReleaseStation();
        }

        protected override void OnDisable()
        {
            RefundStagedOresToInventory();
            base.OnDisable();
            if (furnaceController != null)
            {
                furnaceController.TemperatureChanged -= OnTemperatureChanged;
                furnaceController.SmeltProgressChanged -= OnProgressChanged;
                furnaceController.SmeltCompleted -= OnSmeltCompleted;
            }
            if (_visualController != null)
                _visualController.BottomBloomClicked -= OnExtractBloomClicked;
        }
    }
}
