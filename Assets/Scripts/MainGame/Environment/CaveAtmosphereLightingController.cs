using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Controla dinámicamente la oscuridad subterránea al adentrarse en la caverna (Z > 24m),
    /// atenuando la luz solar/ambiental exterior y habilitando niebla densa más una tenue luz cercana de visibilidad.
    /// </summary>
    public sealed class CaveAtmosphereLightingController : MonoBehaviour
    {
        private Light _sunLight;
        private Light _playerRimLight;
        private Color _outSky, _outEq, _outGnd;
        private float _outAmbInt = 1.05f, _outSunInt = 1.25f;
        private bool _capturedOutdoor;
        private float _caveBlend, _lastAppliedBlend = -1f;
        private Camera _cachedCam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string s = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(s) && s.ToLower().Contains("menu")) return;
            if (FindFirstObjectByType<CaveAtmosphereLightingController>() == null)
            {
                var go = new GameObject("CaveAtmosphereLightingController");
                go.AddComponent<CaveAtmosphereLightingController>();
            }
        }

        private void Start()
        {
            StripBakedCaveTorches();
            CaptureOutdoorDefaults();
        }

        private static void StripBakedCaveTorches()
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null) continue;
                string n = t.name;
                if (n == "CaveTorch" || n == "TorchPointLight" || n == "TorchArm" || n == "TorchIronCup" || n == "TorchFlame")
                    Destroy(t.gameObject);
            }
        }

        private void CaptureOutdoorDefaults()
        {
            if (_capturedOutdoor) return;
            var sunGo = GameObject.Find("Directional Light") ?? GameObject.Find("Sun");
            if (sunGo != null) _sunLight = sunGo.GetComponent<Light>();
            _outSky = RenderSettings.ambientSkyColor;
            _outEq = RenderSettings.ambientEquatorColor;
            _outGnd = RenderSettings.ambientGroundColor;
            _outAmbInt = Mathf.Max(0.4f, RenderSettings.ambientIntensity);
            _outSunInt = _sunLight != null ? Mathf.Max(0.5f, _sunLight.intensity) : 1.25f;
            _capturedOutdoor = true;
        }

        private void EnsurePlayerRimLight()
        {
            if (_playerRimLight != null) return;
            var go = new GameObject("PlayerCaveLanternFill", typeof(Light));
            go.transform.SetParent(transform, false);
            _playerRimLight = go.GetComponent<Light>();
            _playerRimLight.type = LightType.Point;
            _playerRimLight.color = new Color(0.95f, 0.84f, 0.68f);
            _playerRimLight.range = 12.0f;
            _playerRimLight.intensity = 0f;
            _playerRimLight.shadows = LightShadows.None;
        }

        private void LateUpdate()
        {
            if (!_capturedOutdoor) CaptureOutdoorDefaults();
            Transform focus = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.transform : null;
            if (focus == null)
            {
                if (_cachedCam == null || !_cachedCam.isActiveAndEnabled) _cachedCam = Camera.main;
                focus = _cachedCam != null ? _cachedCam.transform : null;
            }
            if (focus == null) return;

            float targetBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 44f, focus.position.z));
            _caveBlend = Mathf.MoveTowards(_caveBlend, targetBlend, Time.unscaledDeltaTime * 2.2f);

            if (Mathf.Abs(_caveBlend - _lastAppliedBlend) > 0.0005f)
            {
                _lastAppliedBlend = _caveBlend;
                Color darkSky = new Color(0.045f, 0.052f, 0.078f);
                Color darkEq = new Color(0.032f, 0.036f, 0.052f);
                Color darkGnd = new Color(0.015f, 0.015f, 0.022f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Color.Lerp(_outSky, darkSky, _caveBlend);
                RenderSettings.ambientEquatorColor = Color.Lerp(_outEq, darkEq, _caveBlend);
                RenderSettings.ambientGroundColor = Color.Lerp(_outGnd, darkGnd, _caveBlend);
                RenderSettings.ambientIntensity = Mathf.Lerp(_outAmbInt, 0.20f, _caveBlend);

                if (_sunLight != null) _sunLight.intensity = Mathf.Lerp(_outSunInt, 0.06f, _caveBlend);

                RenderSettings.fog = _caveBlend > 0.02f;
                if (RenderSettings.fog)
                {
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    RenderSettings.fogColor = new Color(0.022f, 0.025f, 0.038f);
                    RenderSettings.fogDensity = Mathf.Lerp(0.001f, 0.019f, _caveBlend);
                }
            }

            bool needLantern = _caveBlend > 0.01f;
            if (needLantern)
            {
                EnsurePlayerRimLight();
                _playerRimLight.enabled = true;
                _playerRimLight.transform.position = focus.position + Vector3.up * 1.95f + Vector3.back * 0.4f;
                _playerRimLight.intensity = Mathf.Lerp(0f, 0.85f, _caveBlend);
            }
            else if (_playerRimLight != null && _playerRimLight.enabled)
            {
                _playerRimLight.enabled = false;
            }
        }
    }
}

