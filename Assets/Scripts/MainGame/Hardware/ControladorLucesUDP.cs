using System;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Forge;
using ForjaGame.Combat;

namespace ForjaGame.Hardware
{
    /// <summary>
    /// Controlador reactivo de luces LED mediante protocolo UDP hacia Arduino/ESP32 de Lober.
    /// Envía paquetes de 5 bytes [ID_tira (0-2), segundos (1-255), R, G, B] con degradés continuos
    /// sincronizados con temperatura (horno/yunque), porcentaje de prensado, templado y estado del jugador.
    /// </summary>
    public class ControladorLucesUDP : MonoBehaviour
    {
        public static ControladorLucesUDP Instancia { get; private set; }

        [Header("Configuración de Red (Lober)")]
        [Tooltip("Usa 127.0.0.1 para probar con el simulador local. Deja 192.168.1.220 para el build final.")]
        public string ipArduino = "192.168.1.220";
        public int puerto = 3460;

        [Header("Identificador de Tira (0, 1 o 2)")]
        [Range(0, 2)] public byte idTira = 0;

        [Header("Atenuador por Software")]
        [Range(0.1f, 1.0f)] public float multiplicadorIntensidad = 1.0f;

        private UdpClient _clienteUdp;
        private float _nextPollAt, _nextGradSendAt, _burstLockUntil;
        private int _lastSentR = -1, _lastSentG = -1, _lastSentB = -1;
        private FurnaceStationController _furnaceSt;
        private PressStationController _pressSt;
        private AnvilController _anvil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoEnsureManagerLuces()
        {
            if (Instancia != null) return;
            var go = new GameObject("ManagerLuces");
            DontDestroyOnLoad(go);
            Instancia = go.AddComponent<ControladorLucesUDP>();
        }

        private void Awake()
        {
            if (Instancia == null)
            {
                Instancia = this;
                DontDestroyOnLoad(gameObject);
                if (PlayerPrefs.HasKey("LUCES_ID_TIRA"))
                    idTira = (byte)Mathf.Clamp(PlayerPrefs.GetInt("LUCES_ID_TIRA"), 0, 2);
                try
                {
                    string txtPath = System.IO.Path.Combine(Application.dataPath, "..", "tira.txt");
                    if (System.IO.File.Exists(txtPath) && byte.TryParse(System.IO.File.ReadAllText(txtPath).Trim(), out byte fId))
                        idTira = (byte)Mathf.Clamp(fId, 0, 2);
                }
                catch { }
                try { _clienteUdp = new UdpClient(); }
                catch (Exception ex) { Debug.LogWarning($"[LucesUDP] Error iniciando UDP: {ex.Message}"); }
            }
            else if (Instancia != this) Destroy(gameObject);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) SetTiraId(0, "PC 1");
                else if (kb.f2Key.wasPressedThisFrame) SetTiraId(1, "PC 2");
                else if (kb.f3Key.wasPressedThisFrame) SetTiraId(2, "PC 3");
            }

            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + 0.09f;
            PollReactiveGradientState();
        }

        private void PollReactiveGradientState()
        {
            if (Core.GameLoopController.IsEndScreenOpen) return;
            if (_pressSt == null) _pressSt = FindFirstObjectByType<PressStationController>();
            if (_furnaceSt == null) _furnaceSt = FindFirstObjectByType<FurnaceStationController>();
            if (_anvil == null) _anvil = AnvilController.Instance;

            if (_pressSt != null && _pressSt.IsOccupied && _pressSt.Press != null && _pressSt.Press.HasBloom)
            {
                ActualizarPrensado(_pressSt.Press.PressProgress);
                return;
            }
            if (_anvil != null && _anvil.IsForging && !_anvil.IsCarvingHandle && _anvil.ThermalController != null
                && (_anvil.QuenchDifferential == null || !_anvil.QuenchDifferential.IsQuenching))
            {
                ActualizarTemperaturaForja(_anvil.ThermalController.CurrentTemperature);
                return;
            }
            if (_furnaceSt != null && _furnaceSt.IsOccupied && _furnaceSt.Furnace != null)
            {
                ActualizarTemperaturaForja(_furnaceSt.Furnace.CurrentTemperature);
                return;
            }

            var ph = PlayerHealthController.LocalInstance;
            if (ph != null && ph.IsAlive)
            {
                float hpRatio = (float)ph.CurrentHealth / Mathf.Max(1, ph.MaxHealth);
                if (hpRatio <= 0.25f)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
                    EnviarGradienteContinuo(Color.Lerp(new Color(0.24f, 0.02f, 0.02f), new Color(0.82f, 0.06f, 0.04f), pulse), 0.14f);
                }
                else
                {
                    float z = ph.transform.position.z;
                    Color amb = z < 26f
                        ? new Color(0.32f, 0.16f, 0.06f) // Calidez de taller
                        : Color.Lerp(new Color(0.12f, 0.18f, 0.34f), new Color(0.38f, 0.12f, 0.08f), Mathf.Clamp01((z - 26f) / 280f));
                    EnviarGradienteContinuo(amb, 0.25f);
                }
            }
        }

        private void SetTiraId(byte id, string pcLabel)
        {
            idTira = id; PlayerPrefs.SetInt("LUCES_ID_TIRA", idTira); PlayerPrefs.Save();
            Debug.Log($"<color=yellow>[LucesUDP] Asignada y guardada Tira {idTira} ({pcLabel})</color>");
        }

        public void EnviarLuz(byte segundos, byte r, byte g, byte b) => EnviarLuzInternal(segundos, r, g, b, log: true);
        public void EnviarLuz(byte segundos, byte r, byte g, byte b, byte brillo) => EnviarLuzInternal(segundos, r, g, b, log: true);

        private void EnviarLuzInternal(byte segundos, byte r, byte g, byte b, bool log)
        {
            byte idSeguro = (byte)Mathf.Clamp(idTira, 0, 2);
            byte tSeg = (byte)Mathf.Clamp(segundos, 0, 255);
            byte rF = (byte)Mathf.Clamp(r * multiplicadorIntensidad, 0, 255);
            byte gF = (byte)Mathf.Clamp(g * multiplicadorIntensidad, 0, 255);
            byte bF = (byte)Mathf.Clamp(b * multiplicadorIntensidad, 0, 255);
            _lastSentR = rF; _lastSentG = gF; _lastSentB = bF;

            byte[] pkt = new byte[5] { idSeguro, tSeg, rF, gF, bF };
            if (_clienteUdp == null) { try { _clienteUdp = new UdpClient(); } catch { return; } }
            try
            {
                _clienteUdp.Send(pkt, pkt.Length, ipArduino, puerto);
                if (log) Debug.Log($"[UDP -> {ipArduino}:{puerto}] Tira {pkt[0]} | {pkt[1]}s | RGB({pkt[2]},{pkt[3]},{pkt[4]})");
            }
            catch (Exception e) { Debug.LogWarning("[LucesUDP] Error enviando UDP: " + e.Message); }
        }

        public void EnviarGradienteContinuo(Color c, float minInterval = 0.085f)
        {
            if (Time.unscaledTime < _burstLockUntil || Time.unscaledTime < _nextGradSendAt) return;
            byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            int delta = Mathf.Abs(r - _lastSentR) + Mathf.Abs(g - _lastSentG) + Mathf.Abs(b - _lastSentB);
            if (_lastSentR >= 0 && delta < 8) return;
            _nextGradSendAt = Time.unscaledTime + minInterval;
            EnviarLuzInternal(1, r, g, b, log: false);
        }

        public static Color EvaluarGradienteTemperatura(float tempC)
        {
            if (tempC <= 180f) return Color.Lerp(new Color(0.12f, 0.20f, 0.36f), new Color(0.45f, 0.08f, 0.03f), Mathf.Clamp01((tempC - 25f) / 155f));
            if (tempC <= 480f) return Color.Lerp(new Color(0.45f, 0.08f, 0.03f), new Color(0.88f, 0.15f, 0.03f), (tempC - 180f) / 300f);
            if (tempC <= 800f) return Color.Lerp(new Color(0.88f, 0.15f, 0.03f), new Color(1.00f, 0.45f, 0.04f), (tempC - 480f) / 320f);
            if (tempC <= 1150f) return Color.Lerp(new Color(1.00f, 0.45f, 0.04f), new Color(1.00f, 0.78f, 0.16f), (tempC - 800f) / 350f);
            return Color.Lerp(new Color(1.00f, 0.78f, 0.16f), new Color(1.00f, 0.96f, 0.68f), Mathf.Clamp01((tempC - 1150f) / 250f));
        }

        public static Color EvaluarGradientePrensado(float p01)
        {
            p01 = Mathf.Clamp01(p01);
            if (p01 <= 0.45f) return Color.Lerp(new Color(0.36f, 0.07f, 0.03f), new Color(0.96f, 0.36f, 0.04f), p01 / 0.45f);
            if (p01 <= 0.90f) return Color.Lerp(new Color(0.96f, 0.36f, 0.04f), new Color(1.00f, 0.82f, 0.20f), (p01 - 0.45f) / 0.45f);
            return Color.Lerp(new Color(1.00f, 0.82f, 0.20f), new Color(0.68f, 1.00f, 0.45f), (p01 - 0.90f) / 0.10f);
        }

        public void ActualizarTemperaturaForja(float tempC) => EnviarGradienteContinuo(EvaluarGradienteTemperatura(tempC), 0.085f);

        public void ActualizarPrensado(float progress01, bool isPushPulse = false)
        {
            Color c = EvaluarGradientePrensado(progress01);
            if (isPushPulse)
            {
                c = Color.Lerp(c, Color.white, 0.22f);
                _burstLockUntil = Time.unscaledTime + 0.14f;
                EnviarLuzInternal(1, (byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), log: progress01 >= 0.99f);
                return;
            }
            EnviarGradienteContinuo(c, 0.08f);
        }

        public void LuzGolpeForjaTemperatura(float tempC, float quality01)
        {
            Color spark = Color.Lerp(EvaluarGradienteTemperatura(tempC), new Color(1f, 0.94f, 0.60f), Mathf.Lerp(0.35f, 0.85f, Mathf.Clamp01(quality01)));
            _burstLockUntil = Time.unscaledTime + 0.24f;
            EnviarLuzInternal(1, (byte)(spark.r * 255f), (byte)(spark.g * 255f), (byte)(spark.b * 255f), log: true);
        }

        public void LuzTempladoGradiente(float progress01)
        {
            float p = Mathf.Clamp01(progress01);
            Color c = p < 0.42f ? Color.Lerp(new Color(1f, 0.38f, 0.08f), new Color(0f, 0.96f, 0.84f), p / 0.42f)
                                : Color.Lerp(new Color(0f, 0.96f, 0.84f), new Color(0.08f, 0.42f, 1f), (p - 0.42f) / 0.58f);
            _burstLockUntil = Time.unscaledTime + 0.18f;
            EnviarGradienteContinuo(c, 0.08f);
        }

        public void LuzDanioSegunVida(float hpRatio)
        {
            float sev = 1f - Mathf.Clamp01(hpRatio);
            Color c = Color.Lerp(new Color(1f, 0.24f, 0.04f), new Color(1f, 0f, 0f), sev);
            byte dur = (byte)(hpRatio <= 0.25f ? 2 : 1);
            _burstLockUntil = Time.unscaledTime + 0.45f;
            EnviarLuzInternal(dur, (byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), log: true);
        }

        // --- MÉTODOS BASE COMPATIBLES ---
        public void ApagarLuz() { _burstLockUntil = Time.unscaledTime + 1f; EnviarLuzInternal(0, 0, 0, 0, true); }
        public void LuzHornoFuego() { _burstLockUntil = Time.unscaledTime + 0.35f; Color c = _furnaceSt?.Furnace != null ? Color.Lerp(EvaluarGradienteTemperatura(_furnaceSt.Furnace.CurrentTemperature), new Color(1f, 0.32f, 0.04f), 0.45f) : new Color(1f, 0.24f, 0f); EnviarLuzInternal(1, (byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), true); }
        public void LuzHornoFuelle() { _burstLockUntil = Time.unscaledTime + 0.35f; Color c = _furnaceSt?.Furnace != null ? Color.Lerp(EvaluarGradienteTemperatura(_furnaceSt.Furnace.CurrentTemperature), new Color(0f, 0.78f, 1f), 0.72f) : new Color(0f, 0.70f, 1f); EnviarLuzInternal(1, (byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), true); }
        public void LuzGolpeForja() { _burstLockUntil = Time.unscaledTime + 0.22f; EnviarLuzInternal(1, 255, 220, 100, true); }
        public void LuzTempladoAgua() { _burstLockUntil = Time.unscaledTime + 0.8f; EnviarLuzInternal(2, 0, 255, 180, true); }
        public void LuzRecibirDanio() => LuzDanioSegunVida(0.4f);
        public void LuzMuerteRojoTotal() { _burstLockUntil = Time.unscaledTime + 5f; EnviarLuzInternal(5, 255, 0, 0, true); }
        public void LuzVictoriaVerde() { _burstLockUntil = Time.unscaledTime + 5f; EnviarLuzInternal(5, 0, 255, 0, true); }
        public void LuzVictoriaAltar() { _burstLockUntil = Time.unscaledTime + 5f; EnviarLuzInternal(5, 255, 200, 0, true); }

        private void OnApplicationQuit() { if (_clienteUdp != null) { try { _clienteUdp.Close(); } catch { } _clienteUdp = null; } }
    }
}
