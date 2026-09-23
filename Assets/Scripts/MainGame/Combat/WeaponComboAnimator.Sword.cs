using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class WeaponComboAnimator
    {
        /// <summary>
        /// Secuencia cinemática armónica de 5 golpes para espada de 1 mano.
        /// Posiciones elevadas al plano del pecho/hombros relativas a guardPos (Y ~ 1.18m),
        /// filo orientado tangencialmente a la trayectoria de corte y aceleración tipo látigo.
        /// </summary>
        private void GetSwordKeyframe(int comboIndex, float t, Vector3 guardPos, Quaternion guardRot,
            out Vector3 outPos, out Quaternion outRot, out float hitApex)
        {
            Vector3 windupPos, strikePos;
            Quaternion windupRot, strikeRot;
            float windupEnd;
            bool isThrust = false;

            int step = Mathf.Clamp(comboIndex, 0, 4);

            switch (step)
            {
                case 0:
                    // GOLPE 1: CORTE DIAGONAL DESCENDENTE (Der -> Izq)
                    // Carga alta a la derecha del hombro, corte diagonal descendente cruzando el pecho
                    windupEnd = 0.20f;
                    hitApex = 0.44f;
                    windupPos = guardPos + new Vector3(0.06f, 0.12f, -0.08f);
                    windupRot = Quaternion.Euler(48f, 25f, -10f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.36f, -0.08f, 0.02f);
                    strikeRot = Quaternion.Euler(72f, -30f, 10f) * guardRot;
                    break;

                case 1:
                    // GOLPE 2: TAJO ASCENDENTE DE RETORNO (Izq -> Der)
                    // Inicia desde el follow-through en la izquierda baja, tajo ascendente hacia el hombro derecho
                    windupEnd = 0.18f;
                    hitApex = 0.42f;
                    windupPos = guardPos + new Vector3(-0.34f, -0.08f, -0.04f);
                    windupRot = Quaternion.Euler(60f, -25f, -10f) * guardRot;
                    strikePos = guardPos + new Vector3(0.04f, 0.12f, 0.02f);
                    strikeRot = Quaternion.Euler(65f, 30f, 5f) * guardRot;
                    break;

                case 2:
                    // GOLPE 3: TAJO HORIZONTAL AL PECHO (Cleave Der -> Izq)
                    // Carga abierta a la derecha y corte horizontal a nivel del esternón
                    windupEnd = 0.20f;
                    hitApex = 0.44f;
                    windupPos = guardPos + new Vector3(0.08f, 0.00f, -0.08f);
                    windupRot = Quaternion.Euler(75f, 30f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.36f, 0.00f, 0.02f);
                    strikeRot = Quaternion.Euler(80f, -30f, 0f) * guardRot;
                    break;

                case 3:
                    // GOLPE 4: CORTE VERTICAL OVERHEAD (Slam)
                    // Elevación sobre el hombro derecho, slam vertical descendente al centro
                    windupEnd = 0.24f;
                    hitApex = 0.46f;
                    windupPos = guardPos + new Vector3(-0.08f, 0.28f, -0.08f);
                    windupRot = Quaternion.Euler(-60f, 15f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.16f, -0.16f, 0.04f);
                    strikeRot = Quaternion.Euler(85f, 0f, 0f) * guardRot;
                    break;

                default: // case 4
                    // GOLPE 5: ESTOCADA FRONTAL DIRECTA (Lunge Thrust)
                    // Retracción controlada a la altura del costado derecho, lunge lineal hacia el frente
                    windupEnd = 0.24f;
                    hitApex = 0.46f;
                    isThrust = true;
                    windupPos = guardPos + new Vector3(-0.06f, -0.02f, -0.12f);
                    windupRot = Quaternion.Euler(88f, 0f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.14f, 0.00f, 0.18f);
                    strikeRot = Quaternion.Euler(88f, 0f, 0f) * guardRot;
                    break;
            }

            InterpolateKeyframe(t, windupEnd, hitApex, isThrust, windupPos, windupRot,
                strikePos, strikeRot, guardPos, guardRot, out outPos, out outRot);
        }

        private void InterpolateKeyframe(float t, float windupEnd, float hitApex, bool isThrust,
            Vector3 windupPos, Quaternion windupRot, Vector3 strikePos, Quaternion strikeRot,
            Vector3 guardPos, Quaternion guardRot, out Vector3 outPos, out Quaternion outRot)
        {
            if (t <= windupEnd)
            {
                // FASE 1: ANTICIPACIÓN (WIND-UP)
                // Retracción armónica hacia la postura de carga opuesta
                float u = EaseInOutSine(t / windupEnd);
                outPos = Vector3.Lerp(_strikeStartPos, windupPos, u);
                outRot = Quaternion.Slerp(_strikeStartRot, windupRot, u);
            }
            else if (t <= hitApex)
            {
                // FASE 2: ACELERACIÓN TIPO LÁTIGO (DRIVE -> IMPACT)
                // Inicia con rezago mientras la cadera y columna inician la rotación (EaseInQuad)
                // y acelera explosivamente hacia el punto de impacto (hitApex)
                float tau = Mathf.Clamp01((t - windupEnd) / (hitApex - windupEnd));
                float u = EaseInQuad(tau);
                outPos = isThrust ? Vector3.Lerp(windupPos, strikePos, u) : ComputeOrbitalArc(windupPos, strikePos, u);
                outRot = Quaternion.Slerp(windupRot, strikeRot, u);
            }
            else
            {
                // FASE 4: FOLLOW-THROUGH (OVERSHOOT POR INERCIA) Y RECUPERACIÓN SUAVE
                // Tras el impacto, el arma sostiene el arco hacia el follow-through antes de retornar suavemente a la guardia.
                float recoveryStart = Mathf.Lerp(hitApex, 1.0f, 0.40f);
                Vector3 followDir = (strikePos - windupPos).sqrMagnitude > 0.001f ? (strikePos - windupPos).normalized : Vector3.forward;
                Vector3 followThroughPos = strikePos + followDir * 0.04f;
                Quaternion followThroughRot = strikeRot;

                if (t <= recoveryStart)
                {
                    float u = EaseOutQuad((t - hitApex) / (recoveryStart - hitApex));
                    outPos = Vector3.Lerp(strikePos, followThroughPos, u);
                    outRot = Quaternion.Slerp(strikeRot, followThroughRot, u);
                }
                else
                {
                    float u = EaseOutCubic((t - recoveryStart) / (1.0f - recoveryStart));
                    outPos = Vector3.Lerp(followThroughPos, guardPos, u);
                    outRot = Quaternion.Slerp(followThroughRot, guardRot, u);
                }
            }
        }
    }
}
