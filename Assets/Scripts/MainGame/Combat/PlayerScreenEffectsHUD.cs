using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Efectos visuales periféricos y atmosféricos en pantalla para estados de combate:
    /// impacto al recibir daño (+FOV kick y venas carmesí en bordes), vida baja <=15% (latido y pavesas)
    /// y estamina baja <=15% (bruma fría periférica de fatiga), sin cubrir ni entorpecer la interfaz central.
    /// </summary>
    public sealed class PlayerScreenEffectsHUD : MonoBehaviour
    {
        private struct EdgeSpark { public float xNorm, yNorm, speed, size, phase; public bool rightSide; }

        private PlayerHealthController _health;
        private PlayerStaminaController _stamina;
        private PlayerMovement _movement;

        private float _damageFlashStrength, _damageFovKick, _baseFov = 60f, _staminaFrostBlend, _lowHpBlend;
        private bool _hasCapturedBaseFov;
        private EdgeSpark[] _sparks;
        private static Texture2D _organicVignetteTex, _softMoteTex;

        private void Awake()
        {
            _health = GetComponent<PlayerHealthController>();
            _stamina = GetComponent<PlayerStaminaController>();
            _movement = GetComponent<PlayerMovement>();
            InitEdgeSparks();
        }

        private void InitEdgeSparks()
        {
            _sparks = new EdgeSpark[14];
            for (int i = 0; i < _sparks.Length; i++)
                _sparks[i] = new EdgeSpark
                {
                    xNorm = Random.Range(0.01f, 0.085f), yNorm = Random.value,
                    speed = Random.Range(0.14f, 0.34f), size = Random.Range(5f, 11f),
                    phase = Random.value * 6.28f, rightSide = (i & 1) == 1
                };
        }

        public void TriggerDamageHit(int damage, int maxHp)
        {
            float sev = Mathf.Clamp01(damage / Mathf.Max(15f, maxHp * 0.25f));
            _damageFlashStrength = Mathf.Max(_damageFlashStrength, Mathf.Lerp(0.55f, 1.0f, sev));
            _damageFovKick = Mathf.Max(_damageFovKick, Mathf.Lerp(1.8f, 3.6f, sev));
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            _damageFlashStrength = Mathf.MoveTowards(_damageFlashStrength, 0f, Time.deltaTime * 2.1f);
            _damageFovKick = Mathf.Lerp(_damageFovKick, 0f, Time.deltaTime * 9.5f);

            bool alive = _health == null || _health.IsAlive;
            float hpR = _health != null ? Mathf.Clamp01((float)_health.CurrentHealth / Mathf.Max(1, _health.MaxHealth)) : 1f;
            float stmR = _stamina != null ? _stamina.Normalized : 1f;
            _lowHpBlend = Mathf.MoveTowards(_lowHpBlend, (alive && hpR <= 0.15f) ? 1f : 0f, Time.deltaTime * 2.8f);
            _staminaFrostBlend = Mathf.MoveTowards(_staminaFrostBlend, (alive && stmR <= 0.15f) ? 1f : 0f, Time.deltaTime * 3.5f);

            UpdateFovFeedback();
            if (_lowHpBlend > 0.02f && _sparks != null)
            {
                for (int i = 0; i < _sparks.Length; i++)
                {
                    _sparks[i].yNorm -= _sparks[i].speed * Time.deltaTime;
                    if (_sparks[i].yNorm < -0.05f) { _sparks[i].yNorm = 1.04f; _sparks[i].xNorm = Random.Range(0.01f, 0.085f); }
                }
            }
        }

        private void UpdateFovFeedback()
        {
            Camera cam = (_movement != null && _movement.PlayerCamera != null) ? _movement.PlayerCamera : Camera.main;
            if (cam == null) return;
            if (!_hasCapturedBaseFov) { _baseFov = cam.fieldOfView; _hasCapturedBaseFov = true; }
            bool suppressed = UI.Core.UIPanelBase.IsAnyPanelOpen || GameLoopController.IsEndScreenOpen || PauseMenuPanel.IsGamePaused;
            float hbWave = _lowHpBlend > 0.01f ? (Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 3.7f)), 4f) * 1.15f * _lowHpBlend) : 0f;
            float targetFov = suppressed ? _baseFov : Mathf.Clamp(_baseFov + _damageFovKick + hbWave, 45f, 82f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 14f);
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && !netObj.IsOwner) return;
            if (UI.Core.UIPanelBase.IsAnyPanelOpen || GameLoopController.IsEndScreenOpen || VictoryCreditsOverlay.IsBackdropActive) return;
            if (ShaderWarmupCurtain.Instance != null && ShaderWarmupCurtain.Instance.IsWarming) return;
            if (_damageFlashStrength <= 0.01f && _lowHpBlend <= 0.01f && _staminaFrostBlend <= 0.01f) return;

            EnsureTextures();
            GUI.depth = -120;
            Color prev = GUI.color;
            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);

            // 1) Destello radial de venas carmesí-ámbar al recibir un golpe
            if (_damageFlashStrength > 0.01f)
            {
                float a = Mathf.Pow(_damageFlashStrength, 0.85f) * 0.56f;
                GUI.color = new Color(0.88f, 0.14f, 0.08f, a);
                GUI.DrawTexture(screen, _organicVignetteTex);
                GUI.color = new Color(1.0f, 0.52f, 0.16f, a * 0.35f);
                GUI.DrawTexture(new Rect(-18f, -18f, Screen.width + 36f, Screen.height + 36f), _organicVignetteTex);
            }

            // 2) Vida crítica <=15%: doble latido orgánico de fragua + pavesas ascendentes en los márgenes
            if (_lowHpBlend > 0.01f)
            {
                float cyc = (Time.time * 1.15f) % 1f;
                float beat = Mathf.Exp(-Mathf.Pow((cyc - 0.12f) / 0.07f, 2f)) + 0.65f * Mathf.Exp(-Mathf.Pow((cyc - 0.30f) / 0.08f, 2f));
                float a = _lowHpBlend * (0.18f + 0.24f * beat);
                GUI.color = new Color(0.76f, 0.07f, 0.06f, a);
                GUI.DrawTexture(screen, _organicVignetteTex);
                DrawCornerEmbers(_lowHpBlend * (0.45f + 0.35f * beat));
            }

            // 3) Estamina crítica <=15%: bruma fría azulada en el perímetro exterior (respiración lenta)
            if (_staminaFrostBlend > 0.01f)
            {
                float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * 3.1f);
                float a = _staminaFrostBlend * (0.14f + 0.14f * breath);
                GUI.color = new Color(0.24f, 0.52f, 0.72f, a);
                GUI.DrawTexture(screen, _organicVignetteTex);
            }

            GUI.color = prev;
        }

        private void DrawCornerEmbers(float alphaMult)
        {
            if (_sparks == null || _softMoteTex == null) return;
            for (int i = 0; i < _sparks.Length; i++)
            {
                var s = _sparks[i];
                float sway = Mathf.Sin(Time.time * 2.8f + s.phase) * 0.012f;
                float px = (s.rightSide ? (1f - s.xNorm + sway) : (s.xNorm + sway)) * Screen.width;
                float py = s.yNorm * Screen.height;
                float flicker = 0.65f + 0.35f * Mathf.Sin(Time.time * 7.5f + s.phase);
                GUI.color = new Color(1.0f, 0.55f, 0.16f, alphaMult * flicker * 0.65f);
                GUI.DrawTexture(new Rect(px - s.size * 0.5f, py - s.size * 0.5f, s.size, s.size), _softMoteTex);
            }
        }

        private static void EnsureTextures()
        {
            if (_organicVignetteTex == null)
            {
                const int S = 192;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                for (int y = 0; y < S; y++)
                {
                    float ny = (y / (float)(S - 1)) * 2f - 1f;
                    for (int x = 0; x < S; x++)
                    {
                        float nx = (x / (float)(S - 1)) * 2f - 1f;
                        float rad = Mathf.Sqrt(nx * nx * 0.92f + ny * ny * 1.08f);
                        float edge = Mathf.SmoothStep(0.58f, 1.32f, rad);
                        float vein = Mathf.PerlinNoise(nx * 4.5f + 13.7f, ny * 4.5f + 29.1f) * 0.65f
                                   + Mathf.PerlinNoise(nx * 10.5f + 5.3f, ny * 10.5f + 8.9f) * 0.35f;
                        float alpha = Mathf.Clamp01(edge * Mathf.Lerp(0.62f, 1.28f, vein) * Mathf.Pow(edge, 0.65f));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                tex.Apply();
                _organicVignetteTex = tex;
            }
            if (_softMoteTex == null)
            {
                const int M = 24;
                var mote = new Texture2D(M, M, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                for (int y = 0; y < M; y++)
                    for (int x = 0; x < M; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2((M - 1) * 0.5f, (M - 1) * 0.5f)) / (M * 0.5f);
                        float a = Mathf.Clamp01(1f - d);
                        mote.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                mote.Apply();
                _softMoteTex = mote;
            }
        }
    }
}

