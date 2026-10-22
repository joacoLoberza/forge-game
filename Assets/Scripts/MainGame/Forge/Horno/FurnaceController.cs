using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Controlador físico del Horno de Fundición (Bloomery).
    /// Simula la física térmica continua, oxígeno por fuelle, ventilación y combustión (SRP).
    /// </summary>
    public partial class FurnaceController : MonoBehaviour
    {
        [Header("Configuración Térmica")]
        [SerializeField] private float ambientTemperature = 0f; // Comienza en 0°C apagado
        [SerializeField] private float coolingRate = 12f;  // °C por segundo
        [SerializeField] private float heatingRate = 15f;  // °C por segundo (subida suave y controlable del carbón)
        [SerializeField] private float maxTemperature = 1600f;

        [Header("Combustible")]
        [SerializeField] private float coalHeatBonus = 110f; // °C aportados por unidad de carbón

        [Header("Calidad y Tolerancia")]
        [SerializeField] private float currentQuality = 1.0f;
        [SerializeField] private int currentImpurityPercentage = 0;

        [Header("Estado Actual")]
        [System.NonSerialized] private float currentTemperature = 0f;
        [System.NonSerialized] private float targetTemperature = 0f;
        [System.NonSerialized] private MetalType? _firstEnteredMetal;
        [System.NonSerialized] private float _bellowsCooldown = 0f;
        [SerializeField] private float targetMinTemp;
        [SerializeField] private float targetMaxTemp;
        [SerializeField] private float requiredSmeltTime;
        [SerializeField] private float currentSmeltTime;
        [SerializeField] private bool isSmelting;
        [SerializeField] private bool hasReachedSharedRangeOnce;

        private readonly Dictionary<MetalType, int> _insertedOres = new();
        private readonly Stack<MetalType> _oreHistory = new();
        private readonly Dictionary<MetalType, float> _metalErrorSeconds = new();
        private readonly Dictionary<MetalType, int> _metalImpurityDeductions = new();
        [System.NonSerialized] private BloomData _unclaimedBloom;
        [System.NonSerialized] private AlloyComposition _cachedComposition;
        [System.NonSerialized] private bool _isCompositionDirty = true;
        [System.NonSerialized] private float _sweetSpotTime;
        [System.NonSerialized] private float _totalSmeltTimeSpent;

        public IReadOnlyDictionary<MetalType, int> InsertedOres => _insertedOres;
        public IReadOnlyDictionary<MetalType, int> MetalImpurityDeductions => _metalImpurityDeductions;
        public float MaxTemperature => maxTemperature;
        public float CurrentTemperature => currentTemperature;
        public float TargetMinTemp => targetMinTemp;
        public float TargetMaxTemp => targetMaxTemp;
        public float CurrentQuality => currentQuality;
        public int CurrentImpurityPercentage => currentImpurityPercentage;
        public MetalType? FirstEnteredMetal => _firstEnteredMetal;
        public float SweetSpotCenter => (targetMinTemp + targetMaxTemp) * 0.5f;
        public bool IsInOptimumRange => currentTemperature >= targetMinTemp && currentTemperature <= targetMaxTemp && targetMinTemp > 0f;
        public bool IsInSweetSpot => IsInOptimumRange && Mathf.Abs(currentTemperature - SweetSpotCenter) <= 45f;
        public float SweetSpotRatio => _totalSmeltTimeSpent > 0f ? (_sweetSpotTime / _totalSmeltTimeSpent) : (IsInSweetSpot ? 1f : 0f);
        public bool HasReachedSharedRangeOnce => hasReachedSharedRangeOnce;
        public bool IsOverheating => hasReachedSharedRangeOnce && currentTemperature > targetMaxTemp && targetMaxTemp > 0f;
        public bool IsUnderheated => hasReachedSharedRangeOnce && currentTemperature < targetMinTemp && targetMinTemp > 0f;
        public float SmeltProgress => requiredSmeltTime > 0f ? Mathf.Clamp01(currentSmeltTime / requiredSmeltTime) : 0f;
        public bool IsSmeltCompleted => isSmelting && currentSmeltTime >= requiredSmeltTime && requiredSmeltTime > 0f;
        public bool IsSmeltInProgress => isSmelting;
        public BloomData UnclaimedBloom => _unclaimedBloom;

        public event Action<float> TemperatureChanged;
        public event Action<float> SmeltProgressChanged;
        public event Action<float> QualityChanged;
        public event Action<BloomData> SmeltCompleted;

        private void Awake()
        {
            ambientTemperature = 0f;
            currentTemperature = 0f;
            targetTemperature = 0f;
            currentQuality = 1.0f;
            currentImpurityPercentage = 0;
            _firstEnteredMetal = null;
            hasReachedSharedRangeOnce = false;
            _metalErrorSeconds.Clear();
            _metalImpurityDeductions.Clear();
            _isCompositionDirty = true;
        }

        private void Update()
        {
            UpdateFurnace(Time.deltaTime);
        }

        public void UpdateFurnace(float deltaTime)
        {
            float previousTemp = currentTemperature;

            if (_bellowsCooldown > 0f)
                _bellowsCooldown -= deltaTime;

            // 1. Dinámica térmica: sube de forma suave hacia targetTemperature
            if (currentTemperature < targetTemperature)
            {
                currentTemperature = Mathf.MoveTowards(currentTemperature, targetTemperature, heatingRate * deltaTime);
            }
            else if (currentTemperature > ambientTemperature)
            {
                // Enfriamiento rápido hacia 0°C (exige uso de fuelle)
                currentTemperature = Mathf.MoveTowards(currentTemperature, ambientTemperature, coolingRate * deltaTime);
            }

            // TargetTemperature decae suavemente si no se añade más carbón
            if (targetTemperature > ambientTemperature)
            {
                targetTemperature = Mathf.MoveTowards(targetTemperature, ambientTemperature, (coolingRate * 0.5f) * deltaTime);
            }

            if (!Mathf.Approximately(previousTemp, currentTemperature))
            {
                TemperatureChanged?.Invoke(currentTemperature);
            }

            // Si alcanza el rango óptimo compartido por primera vez, se activa el inicio del balanceo
            if (IsInOptimumRange)
            {
                hasReachedSharedRangeOnce = true;
            }

            // 2. Comprobación de inicio de fundición al alcanzar el rango óptimo compartido
            int totalOres = GetTotalOreCount();
            bool hasValidOreCount = (totalOres == 3 || totalOres == 6 || totalOres == 9);

            if (!isSmelting && _unclaimedBloom == null && hasValidOreCount && IsInOptimumRange)
            {
                StartSmelting();
            }

            // 3. Lógica de fundición y control de calidad
            if (isSmelting)
            {
                if (!hasValidOreCount)
                {
                    ResetProgress();
                    return;
                }

                if (IsInOptimumRange)
                {
                    // El progreso SOLAMENTE avanza cuando la temperatura está dentro del rango óptimo (área verde / dorada)
                    float speedMultiplier = IsInSweetSpot ? 1.6f : 1.0f;
                    currentSmeltTime += deltaTime * speedMultiplier;
                    _totalSmeltTimeSpent += deltaTime;
                    if (IsInSweetSpot) _sweetSpotTime += deltaTime;
                    UpdateQualityFromSmelt();
                    SmeltProgressChanged?.Invoke(SmeltProgress);

                    if (currentSmeltTime >= requiredSmeltTime)
                    {
                        isSmelting = false;
                        _unclaimedBloom = GenerateBloom();
                        currentSmeltTime = requiredSmeltTime; // Se mantiene al 100% hasta la extracción
                        SmeltProgressChanged?.Invoke(1.0f);
                        SmeltCompleted?.Invoke(_unclaimedBloom);
                        return;
                    }
                }
                else
                {
                    // Fuera del área verde: el progreso se congela y las impurezas se acumulan por error térmico
                    if (hasReachedSharedRangeOnce)
                    {
                        ProcessThermalImpurities(deltaTime);
                    }
                }
            }
        }

        private void ProcessThermalImpurities(float deltaTime)
        {
            if (currentTemperature > targetMaxTemp)
            {
                // Sobrecalentamiento: fuego excesivo oxida el mineral más blando (1% cada 1.0s)
                MetalType victim = GetLowestMeltingActiveMetal();
                AccumulateImpurity(victim, 1.0f, deltaTime);
            }
            else if (currentTemperature < targetMinTemp)
            {
                // Sub-temperatura: enfriamiento genera escoria en el mineral más duro (1% cada 1.5s)
                MetalType victim = GetHighestMeltingActiveMetal();
                AccumulateImpurity(victim, 1.5f, deltaTime);
            }
        }

        private void AccumulateImpurity(MetalType metal, float secondsPerPercent, float deltaTime)
        {
            if (!_metalErrorSeconds.ContainsKey(metal)) _metalErrorSeconds[metal] = 0f;
            _metalErrorSeconds[metal] += deltaTime;

            while (_metalErrorSeconds[metal] >= secondsPerPercent)
            {
                _metalErrorSeconds[metal] -= secondsPerPercent;
                if (!_metalImpurityDeductions.ContainsKey(metal)) _metalImpurityDeductions[metal] = 0;
                _metalImpurityDeductions[metal]++;
                currentImpurityPercentage = Mathf.Min(95, currentImpurityPercentage + 1);
                _isCompositionDirty = true;
                UpdateQualityFromSmelt();
            }
        }

        private void UpdateQualityFromSmelt()
        {
            float sweetSpotRatio = _totalSmeltTimeSpent > 0f ? (_sweetSpotTime / _totalSmeltTimeSpent) : (IsInSweetSpot ? 1f : 0f);
            float baseQuality;

            if (sweetSpotRatio >= 0.70f && currentImpurityPercentage == 0)
            {
                baseQuality = 1.0f; // Maestra: 70%+ en el punto perfecto y sin impurezas
            }
            else if (sweetSpotRatio >= 0.35f && currentImpurityPercentage <= 10)
            {
                baseQuality = 0.85f; // Estándar
            }
            else if (currentImpurityPercentage <= 25)
            {
                baseQuality = 0.60f; // Impura
            }
            else
            {
                baseQuality = 0.35f; // Quebradiza
            }

            currentQuality = Mathf.Clamp(baseQuality - (currentImpurityPercentage / 100f), 0.10f, 1.0f);
            QualityChanged?.Invoke(currentQuality);
        }
    }
}
