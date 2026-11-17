using UnityEngine;

namespace ForjaGame.Core
{
    /// <summary>
    /// Controlador global de rendimiento y carga de GPU/CPU.
    /// Fija una tasa de refresco saludable (60 FPS) y VSync activo inmediatamente
    /// al iniciar el juego en el editor o en builds, evitando consumo térmico al 100%.
    /// </summary>
    public sealed class GamePerformanceController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializePerformanceCap()
        {
            ApplyTargetRate(60);
        }

        public static void ApplyTargetRate(int targetFps = 60)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = Mathf.Clamp(targetFps, 30, 144);
        }

        private void Awake()
        {
            ApplyTargetRate(60);
        }
    }
}

