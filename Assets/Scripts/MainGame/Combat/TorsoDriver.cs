using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador anatómico de Torso y Cadena Cinética para el Paladín.
    /// Distribuye la torsión entre Caderas (20%), Columna (35%) y Pecho (45%),
    /// con retardos articulares secuenciales (30-80ms) y límites estrictos (torsión <= 75°, caderas <= 30°).
    /// </summary>
    public class TorsoDriver
    {
        private readonly Transform _hips;
        private readonly Transform _spine;
        private readonly Transform _chest;

        private Vector3 _hipsEulerDelayed;
        private Vector3 _spineEulerDelayed;
        private Vector3 _chestEulerDelayed;

        public const float MaxTotalTorsion = 75f;
        public const float MaxHipsTorsion = 30f;

        public TorsoDriver(Transform hips, Transform spine, Transform chest)
        {
            _hips = hips;
            _spine = spine;
            _chest = chest ?? spine;
        }

        public void ApplyTorsoPose(
            TorsoAngles targetPose,
            float inertia,
            float deltaTime,
            Transform rootTransform)
        {
            if (_hips == null || _spine == null || _chest == null) return;

            // 1. Duraciones de desfase articular (30-80ms) según inercia del arma
            float normInertia = Mathf.Clamp01(inertia / 3.5f);
            float lagHips = Mathf.Lerp(0.030f, 0.050f, normInertia);
            float lagSpine = Mathf.Lerp(0.045f, 0.065f, normInertia);
            float lagChest = Mathf.Lerp(0.060f, 0.080f, normInertia);

            // 2. Reparto anatómico de torsión (Caderas 20%, Columna 35%, Pecho 45%)
            Vector3 rawHips = targetPose.hips * 0.20f;
            Vector3 rawSpine = targetPose.spine * 0.35f;
            Vector3 rawChest = targetPose.chest * 0.45f;

            // Límites anatómicos de seguridad
            rawHips.y = Mathf.Clamp(rawHips.y, -MaxHipsTorsion, MaxHipsTorsion);
            float totalYaw = rawHips.y + rawSpine.y + rawChest.y;
            if (Mathf.Abs(totalYaw) > MaxTotalTorsion)
            {
                float factor = MaxTotalTorsion / Mathf.Abs(totalYaw);
                rawHips *= factor;
                rawSpine *= factor;
                rawChest *= factor;
            }

            // 3. Retardos secuenciales mediante SmoothDamp
            _hipsEulerDelayed = Vector3.Lerp(_hipsEulerDelayed, rawHips, deltaTime / lagHips);
            _spineEulerDelayed = Vector3.Lerp(_spineEulerDelayed, rawSpine, deltaTime / lagSpine);
            _chestEulerDelayed = Vector3.Lerp(_chestEulerDelayed, rawChest, deltaTime / lagChest);

            Vector3 up = rootTransform.up;
            Vector3 right = rootTransform.right;
            Vector3 fwd = rootTransform.forward;

            // Aplicar Caderas
            Quaternion qH = Quaternion.AngleAxis(_hipsEulerDelayed.y, up) *
                            Quaternion.AngleAxis(_hipsEulerDelayed.x, right) *
                            Quaternion.AngleAxis(_hipsEulerDelayed.z, fwd);
            _hips.rotation = qH * _hips.rotation;

            // Aplicar Columna
            Quaternion qS = Quaternion.AngleAxis(_spineEulerDelayed.y, up) *
                            Quaternion.AngleAxis(_spineEulerDelayed.x, right) *
                            Quaternion.AngleAxis(_spineEulerDelayed.z, fwd);
            _spine.rotation = qS * _spine.rotation;

            // Aplicar Pecho
            if (_chest != _spine)
            {
                Quaternion qC = Quaternion.AngleAxis(_chestEulerDelayed.y, up) *
                                Quaternion.AngleAxis(_chestEulerDelayed.x, right) *
                                Quaternion.AngleAxis(_chestEulerDelayed.z, fwd);
                _chest.rotation = qC * _chest.rotation;
            }
        }

        /// <summary>
        /// Calcula la posición de contrapeso en espacio del pecho para la mano libre en postura a una mano (1H).
        /// </summary>
        public static Vector3 CalculateFreeHandCounterweight(float attackProgress, AttackMotionType motion, float armLength)
        {
            float side = -0.32f * (armLength / 0.65f);
            float height = -0.15f;
            float fwd = -0.10f;

            if (motion == AttackMotionType.Thrust)
            {
                // En estocada, la mano libre se proyecta hacia atrás para abrir perfil y equilibrar zancada
                fwd = Mathf.Lerp(-0.05f, -0.35f, Mathf.Sin(attackProgress * Mathf.PI));
                height = Mathf.Lerp(-0.15f, 0.05f, attackProgress);
            }
            else
            {
                // En tajo, la mano libre actúa de contrapeso rotacional
                fwd = Mathf.Lerp(-0.10f, 0.15f, attackProgress);
                height = Mathf.Lerp(-0.18f, -0.05f, Mathf.Sin(attackProgress * Mathf.PI));
            }

            return new Vector3(side, height, fwd);
        }

        /// <summary>
        /// Calcula el deslizamiento dinámico del agarre delantero a lo largo del asta en armas tipo Polearm.
        /// </summary>
        public static float CalculatePolearmGripSlide(float attackProgress, AttackMotionType motion, float baseSeparation)
        {
            if (motion == AttackMotionType.Thrust)
            {
                // Durante la extensión de la estocada, la mano delantera desliza hacia el pomo para máxima penetración
                float slide = Mathf.Lerp(0f, -0.22f, Mathf.Sin(attackProgress * Mathf.PI));
                return Mathf.Max(0.12f, baseSeparation + slide);
            }
            return baseSeparation;
        }

        public void ResetPose()
        {
            _hipsEulerDelayed = Vector3.zero;
            _spineEulerDelayed = Vector3.zero;
            _chestEulerDelayed = Vector3.zero;
        }
    }
}
