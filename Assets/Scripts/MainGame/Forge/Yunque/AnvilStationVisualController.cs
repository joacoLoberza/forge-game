using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Controlador visual 3D integral de la estación del yunque:
    /// Gestiona la fragua/fogón de recalentado (tecla [H]) y el barril de agua para templado diferencial.
    /// Sincroniza dinámicamente llamas, brasas, luz y vapor según el estado de forja.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public partial class AnvilStationVisualController : MonoBehaviour
    {
        public static AnvilStationVisualController Instance { get; private set; }

        private AnvilController _anvil;
        private AnvilThermalController _thermal;
        private AnvilQuenchDifferential _quench;

        private Transform _toolsRoot;
        private Transform _hearthRoot;
        private Transform _barrelRoot;

        private Light _hearthLight;
        private ParticleSystem _hearthFlamePs;
        private ParticleSystem _hearthSparksPs;
        private Material _hearthEmbersMat;

        private ParticleSystem _quenchSteamPs;
        private bool _isBuilt;
        private bool _wasQuenching;

        public bool IsBuilt => _isBuilt;
        public Transform HearthRoot => _hearthRoot;
        public Transform BarrelRoot => _barrelRoot;

        private void Awake()
        {
            Instance = this;
            ResolveReferences();
        }

        private void Start() => EnsureBuilt();

        private void OnEnable()
        {
            Instance = this;
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (_anvil == null) _anvil = GetComponent<AnvilController>() ?? GetComponentInChildren<AnvilController>();
            if (_thermal == null) _thermal = GetComponent<AnvilThermalController>() ?? GetComponentInChildren<AnvilThermalController>();
            if (_quench == null) _quench = GetComponent<AnvilQuenchDifferential>() ?? GetComponentInChildren<AnvilQuenchDifferential>();
        }

        public void EnsureBuilt()
        {
            ResolveReferences();
            if (_toolsRoot == null)
            {
                Transform existing = transform.Find("AnvilTools3D_Visual");
                if (existing != null)
                {
                    if (Application.isPlaying) Destroy(existing.gameObject);
                    else DestroyImmediate(existing.gameObject);
                }

                _toolsRoot = new GameObject("AnvilTools3D_Visual").transform;
                _toolsRoot.SetParent(transform, false);
                _toolsRoot.localPosition = Vector3.zero;
                _toolsRoot.localRotation = Quaternion.identity;

                BuildHearthVisual();
                BuildBarrelVisual();
            }

            _isBuilt = true;
        }

        private void Update()
        {
            if (!_isBuilt || _toolsRoot == null) EnsureBuilt();
            ResolveReferences();

            UpdateHearthThermalVisuals();
            UpdateQuenchSteamVisuals();
        }

        private void UpdateHearthThermalVisuals()
        {
            if (_hearthLight == null) return;

            bool isReheating = _thermal != null && _thermal.IsReheating;
            float targetIntensity = isReheating ? 3.6f : 1.15f;
            float targetRange = isReheating ? 4.8f : 2.8f;
            float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(Time.time * 8.5f, 3.4f);

            _hearthLight.intensity = Mathf.Lerp(_hearthLight.intensity, targetIntensity * flicker, Time.deltaTime * 6f);
            _hearthLight.range = Mathf.Lerp(_hearthLight.range, targetRange, Time.deltaTime * 3f);

            Color emberCol = isReheating
                ? new Color(1.0f, 0.58f, 0.12f) * 2.8f
                : new Color(0.85f, 0.22f, 0.04f) * (0.8f + 0.4f * flicker);

            if (_hearthEmbersMat != null)
            {
                _hearthEmbersMat.SetColor("_EmissionColor", emberCol);
            }

            if (_hearthFlamePs != null)
            {
                var em = _hearthFlamePs.emission;
                em.rateOverTime = isReheating ? 5f : 1.5f;
            }

            if (_hearthSparksPs != null)
            {
                var em = _hearthSparksPs.emission;
                em.rateOverTime = isReheating ? 3f : 0.5f;
            }
        }

        private void UpdateQuenchSteamVisuals()
        {
            if (_quench == null || _quenchSteamPs == null) return;

            bool isQuenching = _quench.IsQuenching;
            if (isQuenching && !_wasQuenching)
            {
                _quenchSteamPs.Play();
            }
            else if (!isQuenching && _wasQuenching)
            {
                _quenchSteamPs.Stop();
            }

            var em = _quenchSteamPs.emission;
            em.rateOverTime = isQuenching ? 9f : 0f;
            _wasQuenching = isQuenching;
        }
    }
}

