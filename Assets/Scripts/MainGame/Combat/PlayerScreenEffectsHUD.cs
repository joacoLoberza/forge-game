using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ForjaGame.Core;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Marcos periféricos en un Canvas dedicado al fondo (sortingOrder = -500) para quedar siempre
    /// detrás de toda la UI (cronómetro, barras de vida/estamina, inventario y pausa).
    /// - Rojo: daño recibido y vida baja, expandiéndose suavemente al centro sin temblor.
    /// - Azul frío: cansancio estricto por debajo de 50 puntos de estamina, sin temblor.
    /// </summary>
    public sealed class PlayerScreenEffectsHUD : MonoBehaviour
    {
        private PlayerHealthController _health;
        private PlayerStaminaController _stamina;
        private PlayerMovement _movement;

        private float _damageFlashStrength, _baseFov = 60f, _staminaFrostBlend, _lowHpBlend;
        private bool _hasCapturedBaseFov;
        private GameObject _overlayRoot;
        private readonly RawImage[] _redEdges = new RawImage[4];
        private readonly RawImage[] _blueEdges = new RawImage[4];
        private static Texture2D _gradTopTex, _gradBottomTex, _gradLeftTex, _gradRightTex;

        private void Awake()
        {
            _health = GetComponent<PlayerHealthController>();
            _stamina = GetComponent<PlayerStaminaController>();
            _movement = GetComponent<PlayerMovement>();
        }

        private void OnDestroy()
        {
            if (_overlayRoot != null) Destroy(_overlayRoot);
        }

        public void TriggerDamageHit(int damage, int maxHp)
        {
            float sev = Mathf.Clamp01(damage / Mathf.Max(15f, maxHp * 0.25f));
            _damageFlashStrength = Mathf.Max(_damageFlashStrength, Mathf.Lerp(0.55f, 1.0f, sev));
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") { SetOverlayVisible(false); return; }
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && !netObj.IsOwner) { SetOverlayVisible(false); return; }

            float dt = Time.unscaledDeltaTime;
            _damageFlashStrength = Mathf.MoveTowards(_damageFlashStrength, 0f, dt * 1.9f);

            bool alive = _health == null || _health.IsAlive;
            float hpR = _health != null ? Mathf.Clamp01((float)_health.CurrentHealth / Mathf.Max(1, _health.MaxHealth)) : 1f;
            float curStm = _stamina != null ? _stamina.CurrentStamina : 100f;
            float missingHp = alive ? Mathf.Clamp01((0.88f - hpR) / 0.88f) : 1f;
            float rawFatigue = (alive && curStm < 50f) ? Mathf.Clamp01((50f - curStm) / 50f) : 0f;
            _lowHpBlend = Mathf.MoveTowards(_lowHpBlend, missingHp, dt * 2.8f);
            _staminaFrostBlend = Mathf.MoveTowards(_staminaFrostBlend, rawFatigue, dt * 3.2f);
            RestoreSteadyFov();

            bool blocked = GameLoopController.IsEndScreenOpen || VictoryCreditsOverlay.IsBackdropActive
                || (ShaderWarmupCurtain.Instance != null && ShaderWarmupCurtain.Instance.IsWarming);
            float redFrame = blocked ? 0f : Mathf.Clamp01(Mathf.Max(_lowHpBlend, _damageFlashStrength * 0.78f));
            float blueFrame = blocked ? 0f : _staminaFrostBlend;

            if (redFrame <= 0.01f && blueFrame <= 0.01f) { SetOverlayVisible(false); return; }

            EnsureOverlayCanvas();
            _overlayRoot.SetActive(true);
            UpdateEdgeFrame(_redEdges, redFrame, new Color(0.82f, 0.06f, 0.05f));
            UpdateEdgeFrame(_blueEdges, blueFrame, new Color(0.20f, 0.48f, 0.68f));
        }

        private void RestoreSteadyFov()
        {
            Camera cam = (_movement != null && _movement.PlayerCamera != null) ? _movement.PlayerCamera : Camera.main;
            if (cam == null) return;
            if (!_hasCapturedBaseFov) { _baseFov = cam.fieldOfView; _hasCapturedBaseFov = true; }
            if (!Mathf.Approximately(cam.fieldOfView, _baseFov))
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, _baseFov, Time.unscaledDeltaTime * 14f);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayRoot != null && _overlayRoot.activeSelf != visible) _overlayRoot.SetActive(visible);
        }

        private void EnsureOverlayCanvas()
        {
            if (_overlayRoot != null) return;
            EnsureTextures();
            _overlayRoot = new GameObject("PlayerScreenEffectsBackdropCanvas", typeof(Canvas));
            var canvas = _overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -500; // Siempre al fondo detrás de cronómetro, barras de vida, inventario y pausa
            canvas.overrideSorting = true;

            BuildEdgeGroup(_overlayRoot.transform, "RedDamage", _redEdges);
            BuildEdgeGroup(_overlayRoot.transform, "BlueFatigue", _blueEdges);
        }

        private static void BuildEdgeGroup(Transform parent, string prefix, RawImage[] target)
        {
            Texture2D[] texs = { _gradTopTex, _gradBottomTex, _gradLeftTex, _gradRightTex };
            Vector2[] aMin = { new(0f, 1f), new(0f, 0f), new(0f, 0f), new(1f, 0f) };
            Vector2[] aMax = { new(1f, 1f), new(1f, 0f), new(0f, 1f), new(1f, 1f) };
            Vector2[] piv  = { new(0.5f, 1f), new(0.5f, 0f), new(0f, 0.5f), new(1f, 0.5f) };

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"{prefix}_{i}", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = aMin[i]; rt.anchorMax = aMax[i]; rt.pivot = piv[i];
                rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
                var img = go.GetComponent<RawImage>();
                img.texture = texs[i];
                img.raycastTarget = false;
                img.enabled = false;
                target[i] = img;
            }
        }

        private static void UpdateEdgeFrame(RawImage[] edges, float intensity, Color col)
        {
            bool show = intensity > 0.01f;
            float tx = Mathf.Max(24f, Screen.width * Mathf.Lerp(0.025f, 0.34f, intensity));
            float ty = Mathf.Max(24f, Screen.height * Mathf.Lerp(0.03f, 0.34f, intensity));
            Color c = new Color(col.r, col.g, col.b, Mathf.Clamp01(0.26f + 0.54f * intensity));

            for (int i = 0; i < 4; i++)
            {
                if (edges[i] == null) continue;
                edges[i].enabled = show;
                if (!show) continue;
                edges[i].color = c;
                edges[i].rectTransform.sizeDelta = i < 2 ? new Vector2(0f, ty) : new Vector2(tx, 0f);
            }
        }

        private static void EnsureTextures()
        {
            if (_gradTopTex != null) return;
            _gradTopTex = MakeEdgeGrad(false, true);
            _gradBottomTex = MakeEdgeGrad(false, false);
            _gradLeftTex = MakeEdgeGrad(true, true);
            _gradRightTex = MakeEdgeGrad(true, false);
        }

        private static Texture2D MakeEdgeGrad(bool horizontal, bool fromZero)
        {
            const int N = 64;
            var t = new Texture2D(horizontal ? N : 1, horizontal ? 1 : N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < N; i++)
            {
                float u = i / (float)(N - 1);
                float edgeDist = horizontal ? (fromZero ? u : 1f - u) : (fromZero ? 1f - u : u);
                float a = Mathf.Pow(Mathf.Clamp01(1f - edgeDist), 1.65f);
                t.SetPixel(horizontal ? i : 0, horizontal ? 0 : i, new Color(1f, 1f, 1f, a));
            }
            t.Apply(); return t;
        }
    }
}
