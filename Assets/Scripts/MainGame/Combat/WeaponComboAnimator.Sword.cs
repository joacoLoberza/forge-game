using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class WeaponComboAnimator
    {
        /// <summary>
        /// Secuencia cinemática armónica de 5 golpes para espada de 1 mano y dagas.
        /// Flujo continuo de inercia entre golpes, filo vertical garantizado y gran estocada final.
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
                    // Carga a la derecha con holgura frontal respecto al pecho (Z = 0.34m)
                    windupEnd = 0.20f;
                    hitApex = 0.44f;
                    windupPos = new Vector3(0.32f, 0.18f, 0.34f);
                    windupRot = Quaternion.Euler(55f, 25f, -10f);
                    strikePos = new Vector3(-0.12f, -0.02f, 0.44f);
                    strikeRot = Quaternion.Euler(70f, -30f, 10f);
                    break;

                case 1:
                    // GOLPE 2: TAJO ASCENDENTE DE RETORNO (Izq -> Der)
                    // Inicia desde el follow-through en la izquierda manteniendo 36cm de separación
                    windupEnd = 0.18f;
                    hitApex = 0.42f;
                    windupPos = new Vector3(-0.12f, -0.06f, 0.36f);
                    windupRot = Quaternion.Euler(60f, -25f, -10f);
                    strikePos = new Vector3(0.28f, 0.14f, 0.42f);
                    strikeRot = Quaternion.Euler(65f, 30f, 5f);
                    break;

                case 2:
                    // GOLPE 3: TAJO HORIZONTAL A LA CINTURA (Cleave Der -> Izq)
                    // Carga abierta a la derecha y corte horizontal con amplio radio de paso
                    windupEnd = 0.20f;
                    hitApex = 0.44f;
                    windupPos = new Vector3(0.34f, 0.06f, 0.34f);
                    windupRot = Quaternion.Euler(75f, 30f, 0f);
                    strikePos = new Vector3(-0.12f, 0.04f, 0.44f);
                    strikeRot = Quaternion.Euler(80f, -30f, 0f);
                    break;

                case 3:
                    // GOLPE 4: CORTE VERTICAL OVERHEAD (Slam)
                    // Elevación sobre el hombro derecho con suficiente separación hacia adelante (Z = 0.32m)
                    windupEnd = 0.24f;
                    hitApex = 0.46f;
                    windupPos = new Vector3(0.18f, 0.34f, 0.32f);
                    windupRot = Quaternion.Euler(180f, 90f, 135f);
                    strikePos = new Vector3(0.08f, -0.08f, 0.46f);
                    strikeRot = Quaternion.Euler(180f, 90f, 75f);
                    break;

                default: // case 4
                    // GOLPE 5: ESTOCADA FRONTAL DIRECTA (Lunge Thrust)
                    // Retracción controlada a la altura del costado con 32cm de separación frontal
                    windupEnd = 0.26f;
                    hitApex = 0.48f;
                    isThrust = true;
                    windupPos = new Vector3(0.22f, 0.05f, 0.32f);
                    windupRot = Quaternion.Euler(90f, 0f, 0f);
                    strikePos = new Vector3(0.10f, 0.03f, 0.50f);
                    strikeRot = Quaternion.Euler(90f, 0f, 0f);
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
                float u = EaseInQuad(t / windupEnd);
                outPos = Vector3.Lerp(_strikeStartPos, windupPos, u);
                outRot = Quaternion.Slerp(_strikeStartRot, windupRot, u);
            }
            else if (t <= hitApex)
            {
                float u = EaseOutExpo((t - windupEnd) / (hitApex - windupEnd));
                outPos = isThrust ? Vector3.Lerp(windupPos, strikePos, u) : ComputeOrbitalArc(windupPos, strikePos, u);
                outRot = Quaternion.Slerp(windupRot, strikeRot, u);
            }
            else
            {
                // Follow-through y espaciado marcial:
                // Tras el impacto (hitApex), el arma mantiene inercia hacia el final del arco (follow-through)
                // durante la ventana de combo (hasta ~0.65-0.70), en lugar de volver bruscamente a la guardia.
                // Si no se encadena otro golpe, retorna suavemente a guardPos.
                float recoveryStart = Mathf.Lerp(hitApex, 1.0f, 0.45f);
                Vector3 followDir = (strikePos - windupPos).sqrMagnitude > 0.001f ? (strikePos - windupPos).normalized : Vector3.forward;
                Vector3 followThroughPos = strikePos + followDir * 0.035f;
                Quaternion followThroughRot = strikeRot;

                if (t <= recoveryStart)
                {
                    float u = Mathf.SmoothStep(0f, 1f, (t - hitApex) / (recoveryStart - hitApex));
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
