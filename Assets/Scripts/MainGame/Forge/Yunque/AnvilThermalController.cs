using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Controlador térmico del lingote en el yunque:
    /// 1. Temperatura e incandescencia visual orgánica (sin sliders).
    /// 2. Maleabilidad del martillo según calor (rojo vivo = fluido, frío = rígido).
    /// 3. Recalentado suave detrás del yunque hacia el fuego con seguimiento.
    /// 4. Sobrecalentamiento: color oscuro quemado y reducción de calidad.
    /// Preparado para Netcode / RPCs (variables limpias y métodos de sincronización).
    /// </summary>
    public class AnvilThermalController : MonoBehaviour
    {
        [Header("Thermal Configuration")]
        [SerializeField] private float initialTemperature = 25f;
        [SerializeField] private float ambientTemperature = 25f;
        [SerializeField] private float coolingRate = 8f; // Grados por segundo en aire
        [SerializeField] private float heatingRate = 160f; // Grados por segundo en el fuego (dinámico y rápido)
        [SerializeField] private Vector3 reheatOffset = new Vector3(0f, 0.18f, 0.95f);

        private float _currentTemp = 25f;
        private bool _isReheating;
        private AnvilController _anvil;
        private Vector3 _baseLocalPos = Vector3.zero;
        private MaterialPropertyBlock _propBlock;

        public float CurrentTemperature => _currentTemp;
        public float AmbientTemperature => ambientTemperature;
        public bool IsReheating => _isReheating;
        public Vector3 CurrentIngotOffset => _isReheating ? reheatOffset : Vector3.zero;

        public void Initialize(AnvilController anvil)
        {
            _anvil = anvil;
            _currentTemp = initialTemperature;
            _isReheating = false;
            _propBlock = new MaterialPropertyBlock();
            if (_anvil != null && _anvil.IngotTransform != null)
                _baseLocalPos = _anvil.IngotTransform.localPosition;
        }

        public void ResetThermal(float temp = 25f)
        {
            _currentTemp = temp;
            _isReheating = false;
        }

        public void ToggleReheat() => SetReheating(!_isReheating);

        public void SetReheating(bool reheating)
        {
            var asm = _anvil != null ? _anvil.GetComponent<AnvilAssemblyController>() : null;
            if (_anvil == null || !_anvil.IsForging || _anvil.IsCarvingHandle || (asm != null && asm.IsAssembling))
            {
                _isReheating = false;
                return;
            }
            _isReheating = reheating;
        }

        public void SetTemperature(float temp) => _currentTemp = Mathf.Clamp(temp, ambientTemperature, 1400f);

        public float GetMalleabilityMultiplier()
        {
            if (_currentTemp >= 800f) return Mathf.Lerp(1.0f, 1.35f, Mathf.Clamp01((_currentTemp - 800f) / 350f));
            if (_currentTemp >= 500f) return Mathf.Lerp(0.20f, 1.0f, (_currentTemp - 500f) / 300f);
            if (_currentTemp >= 250f) return Mathf.Lerp(0.04f, 0.20f, (_currentTemp - 250f) / 250f);
            return 0.04f; // Castigo severo: casi nulo si está frío
        }

        private void Update()
        {
            var asm = _anvil != null ? _anvil.GetComponent<AnvilAssemblyController>() : null;
            if (_anvil == null || !_anvil.IsForging || _anvil.IsCarvingHandle || (asm != null && asm.IsAssembling)) return;
            bool isQuenching = (_anvil.QuenchDifferential != null && _anvil.QuenchDifferential.IsQuenching);

            if (!isQuenching)
            {
                if (_isReheating)
                {
                    _currentTemp = Mathf.Min(1400f, _currentTemp + heatingRate * Time.deltaTime);
                    if (_currentTemp > 1260f)
                    {
                        float penalty = 0.020f * Time.deltaTime;
                        _anvil.ReduceQualityFromOverheat(penalty);
                    }
                }
                else
                {
                    if (_currentTemp > ambientTemperature)
                        _currentTemp = Mathf.Max(ambientTemperature, _currentTemp - coolingRate * Time.deltaTime);
                }

                UpdateIngotTransform();
            }

            UpdateVisualColors();
        }

        private void UpdateIngotTransform()
        {
            if (_anvil.IngotTransform == null) return;
            if (_anvil.QuenchDifferential != null && _anvil.QuenchDifferential.IsQuenching) return;
            Vector3 targetPos = _baseLocalPos + (_isReheating ? reheatOffset : Vector3.zero);
            _anvil.IngotTransform.localPosition = Vector3.Lerp(_anvil.IngotTransform.localPosition, targetPos, Time.deltaTime * 3.5f);
        }

        public void UpdateVisualColors()
        {
            if (_anvil == null || _anvil.IngotRenderer == null) return;

            Color baseMetal = _anvil.CurrentMetalBaseColor;
            Color baseCol = baseMetal;
            Color emitCol = Color.black;

            if (_currentTemp > 450f)
            {
                if (_currentTemp <= 700f)
                {
                    float t = (_currentTemp - 450f) / 250f;
                    Color cherry = new Color(0.72f, 0.20f, 0.10f);
                    baseCol = Color.Lerp(baseMetal, cherry, t * 0.45f);
                    emitCol = cherry * (t * 0.08f);
                }
                else if (_currentTemp <= 950f)
                {
                    float t = (_currentTemp - 700f) / 250f;
                    Color cherry = new Color(0.72f, 0.20f, 0.10f);
                    Color warmOrange = new Color(0.92f, 0.48f, 0.12f);
                    Color inc = Color.Lerp(cherry, warmOrange, t);
                    baseCol = Color.Lerp(baseMetal, inc, 0.45f + t * 0.35f);
                    emitCol = inc * (0.08f + t * 0.07f);
                }
                else if (_currentTemp <= 1200f)
                {
                    float t = (_currentTemp - 950f) / 250f;
                    Color warmOrange = new Color(0.92f, 0.48f, 0.12f);
                    Color brightYellow = new Color(1.00f, 0.78f, 0.22f);
                    Color inc = Color.Lerp(warmOrange, brightYellow, t);
                    baseCol = Color.Lerp(baseMetal, inc, 0.80f + t * 0.20f);
                    emitCol = inc * (0.15f + t * 0.05f);
                }
                else
                {
                    float tOver = Mathf.Clamp01((_currentTemp - 1200f) / 180f);
                    Color hotYellow = new Color(1.00f, 0.78f, 0.22f);
                    Color burntScale = new Color(0.30f, 0.26f, 0.24f);
                    baseCol = Color.Lerp(hotYellow, burntScale, tOver * 0.70f);
                    emitCol = Color.Lerp(new Color(0.20f, 0.15f, 0.04f), new Color(0.10f, 0.02f, 0.01f), tOver);
                }
            }

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _anvil.IngotRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", baseCol);
            _propBlock.SetColor("_Color", baseCol);
            _propBlock.SetColor("_EmissionColor", emitCol);
            _anvil.IngotRenderer.SetPropertyBlock(_propBlock);
        }
    }
}
