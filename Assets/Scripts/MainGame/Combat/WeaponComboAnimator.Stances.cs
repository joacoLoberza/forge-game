using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class WeaponComboAnimator
    {
        /// <summary>
        /// Secuencia cinemática de 4 golpes contundentes para Martillo de Guerra (WeaponAnimationStyle.Hammer1H).
        /// Posiciones relativas a guardPos a la altura del pecho/hombros.
        /// La cara plana de impacto del martillo lidera estrictamente cada golpe hacia la dirección de ataque.
        /// </summary>
        private void GetHammerKeyframe(int comboIndex, float t, Vector3 guardPos, Quaternion guardRot,
            out Vector3 outPos, out Quaternion outRot, out float hitApex)
        {
            Vector3 windupPos, strikePos;
            Quaternion windupRot, strikeRot;
            float windupEnd;
            int step = Mathf.Clamp(comboIndex, 0, 4);

            switch (step)
            {
                case 0:
                    // GOLPE 1: APLASTAMIENTO OVERHEAD
                    // Cabeza de martillo elevada sobre el hombro, slam descendente con cara frontal plana hacia el impacto
                    windupEnd = 0.26f;
                    hitApex = 0.50f;
                    windupPos = guardPos + new Vector3(-0.06f, 0.26f, -0.10f);
                    windupRot = Quaternion.Euler(-60f, 10f, -5f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.18f, -0.18f, 0.04f);
                    strikeRot = Quaternion.Euler(85f, 0f, 0f) * guardRot;
                    break;

                case 1:
                    // GOLPE 2: TRAYECTORIA HORIZONTAL (Der -> Izq)
                    // La cara plana de impacto lidera el avance del barrido horizontal
                    windupEnd = 0.24f;
                    hitApex = 0.48f;
                    windupPos = guardPos + new Vector3(0.06f, 0.02f, -0.08f);
                    windupRot = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(80f, 35f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.38f, -0.04f, 0.02f);
                    strikeRot = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(85f, -30f, 0f) * guardRot;
                    break;

                case 2:
                    // GOLPE 3: RETORNO HORIZONTAL (Izq -> Der)
                    // La cara plana de impacto lidera el contragolpe de regreso
                    windupEnd = 0.24f;
                    hitApex = 0.48f;
                    windupPos = guardPos + new Vector3(-0.36f, -0.06f, -0.06f);
                    windupRot = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(60f, -30f, -10f) * guardRot;
                    strikePos = guardPos + new Vector3(0.06f, 0.04f, 0.02f);
                    strikeRot = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(65f, 35f, 5f) * guardRot;
                    break;

                case 3:
                    // GOLPE 4: GRAN APLASTAMIENTO SÍSMICO
                    // Elevación central máxima y descarga demoledora vertical
                    windupEnd = 0.30f;
                    hitApex = 0.54f;
                    windupPos = guardPos + new Vector3(-0.10f, 0.30f, -0.10f);
                    windupRot = Quaternion.Euler(-70f, 0f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.20f, -0.20f, 0.04f);
                    strikeRot = Quaternion.Euler(95f, 0f, 0f) * guardRot;
                    break;

                default: // case 4
                    // GOLPE 5: REMATE HORIZONTAL PESADO
                    windupEnd = 0.30f;
                    hitApex = 0.54f;
                    windupPos = guardPos + new Vector3(0.06f, 0.24f, -0.08f);
                    windupRot = Quaternion.Euler(70f, -25f, 90f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.22f, -0.14f, 0.04f);
                    strikeRot = Quaternion.Euler(95f, 25f, 90f) * guardRot;
                    break;
            }

            InterpolateKeyframe(t, windupEnd, hitApex, false, windupPos, windupRot,
                strikePos, strikeRot, guardPos, guardRot, out outPos, out outRot);
        }

        /// <summary>
        /// Secuencia cinemática de 4 tajos con el filo cortante para Hacha de Combate (WeaponAnimationStyle.Axe1H).
        /// Posiciones relativas a guardPos a la altura del pecho.
        /// </summary>
        private void GetAxeKeyframe(int comboIndex, float t, Vector3 guardPos, Quaternion guardRot,
            out Vector3 outPos, out Quaternion outRot, out float hitApex)
        {
            Vector3 windupPos, strikePos;
            Quaternion windupRot, strikeRot;
            float windupEnd;
            int step = Mathf.Clamp(comboIndex, 0, 4);

            switch (step)
            {
                case 0:
                    // GOLPE 1: TAJO LATERAL DE DERECHA A IZQUIERDA
                    windupEnd = 0.21f;
                    hitApex = 0.45f;
                    windupPos = guardPos + new Vector3(0.06f, 0.04f, -0.08f);
                    windupRot = Quaternion.Euler(4f, 70f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.36f, -0.04f, 0.02f);
                    strikeRot = Quaternion.Euler(4f, -50f, 0f) * guardRot;
                    break;

                case 1:
                    // GOLPE 2: RETORNO CRUZADO DE IZQUIERDA A DERECHA
                    windupEnd = 0.20f;
                    hitApex = 0.44f;
                    windupPos = guardPos + new Vector3(-0.36f, -0.04f, -0.06f);
                    windupRot = Quaternion.Euler(-8f, -50f, 2f) * guardRot;
                    strikePos = guardPos + new Vector3(0.04f, 0.04f, 0.02f);
                    strikeRot = Quaternion.Euler(-8f, 55f, -2f) * guardRot;
                    break;

                case 2:
                    // GOLPE 3: TAJO DE CADERA CON CAMBIO DE NIVEL
                    windupEnd = 0.23f;
                    hitApex = 0.46f;
                    windupPos = guardPos + new Vector3(0.04f, -0.08f, -0.08f);
                    windupRot = Quaternion.Euler(22f, 48f, -8f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.36f, 0.02f, 0.02f);
                    strikeRot = Quaternion.Euler(-18f, -40f, 8f) * guardRot;
                    break;

                case 3:
                    // GOLPE 4: HACHAZO VERTICAL DIVISORIO
                    windupEnd = 0.28f;
                    hitApex = 0.50f;
                    windupPos = guardPos + new Vector3(-0.08f, 0.26f, -0.08f);
                    windupRot = Quaternion.Euler(-55f, 0f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.18f, -0.18f, 0.04f);
                    strikeRot = Quaternion.Euler(90f, 0f, 0f) * guardRot;
                    break;

                default: // case 4
                    // GOLPE 5: REMATE DIAGONAL DE HACHAZO
                    windupEnd = 0.30f;
                    hitApex = 0.52f;
                    windupPos = guardPos + new Vector3(-0.06f, 0.28f, -0.08f);
                    windupRot = Quaternion.Euler(-48f, 20f, -12f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.32f, -0.16f, 0.04f);
                    strikeRot = Quaternion.Euler(88f, -12f, 8f) * guardRot;
                    break;
            }

            InterpolateKeyframe(t, windupEnd, hitApex, false, windupPos, windupRot,
                strikePos, strikeRot, guardPos, guardRot, out outPos, out outRot);
        }

        /// <summary>
        /// Secuencia de barridos para Mandoble Pesado 2H (WeaponAnimationStyle.Heavy2H).
        /// Con holgura frontal generosa para permitir que ambos brazos crucen sin chocar con el pecho.
        /// </summary>
        private void GetHeavy2HKeyframe(int comboIndex, float t, Vector3 guardPos, Quaternion guardRot,
            out Vector3 outPos, out Quaternion outRot, out float hitApex)
        {
            Vector3 windupPos, strikePos;
            Quaternion windupRot, strikeRot;
            float windupEnd;
            int step = Mathf.Clamp(comboIndex, 0, 4);

            switch (step)
            {
                case 0:
                    // GOLPE 1: BARRIDO HORIZONTAL INICIAL (Arco de derecha a izquierda)
                    windupEnd = 0.28f;
                    hitApex = 0.50f;
                    windupPos = guardPos + new Vector3(0.08f, 0.06f, -0.08f);
                    windupRot = Quaternion.Euler(50f, 40f, -10f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.32f, -0.08f, 0.02f);
                    strikeRot = Quaternion.Euler(70f, -40f, 10f) * guardRot;
                    break;

                case 1:
                    // GOLPE 2: CORTE GIRATORIO CONTINUO
                    windupEnd = 0.24f;
                    hitApex = 0.48f;
                    windupPos = guardPos + new Vector3(-0.30f, -0.04f, -0.06f);
                    windupRot = Quaternion.Euler(60f, -50f, 10f) * guardRot;
                    strikePos = guardPos + new Vector3(0.06f, 0.04f, 0.02f);
                    strikeRot = Quaternion.Euler(65f, 40f, 10f) * guardRot;
                    break;

                case 2:
                    // GOLPE 3: DEMOLEDOR TAJO VERTICAL A 2 MANOS
                    windupEnd = 0.32f;
                    hitApex = 0.52f;
                    windupPos = guardPos + new Vector3(-0.08f, 0.28f, -0.08f);
                    windupRot = Quaternion.Euler(-60f, 10f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.18f, -0.18f, 0.04f);
                    strikeRot = Quaternion.Euler(85f, 0f, 0f) * guardRot;
                    break;

                case 3:
                    // GOLPE 4: BARRIDO LATERAL DE RECUPERACION
                    windupEnd = 0.28f;
                    hitApex = 0.50f;
                    windupPos = guardPos + new Vector3(-0.30f, -0.04f, -0.06f);
                    windupRot = Quaternion.Euler(60f, -40f, 10f) * guardRot;
                    strikePos = guardPos + new Vector3(0.06f, -0.02f, 0.02f);
                    strikeRot = Quaternion.Euler(70f, 40f, -10f) * guardRot;
                    break;

                default: // case 4
                    // GOLPE 5: REMATE VERTICAL A DOS MANOS
                    windupEnd = 0.34f;
                    hitApex = 0.56f;
                    windupPos = guardPos + new Vector3(-0.10f, 0.30f, -0.08f);
                    windupRot = Quaternion.Euler(-65f, 0f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.18f, -0.20f, 0.04f);
                    strikeRot = Quaternion.Euler(90f, 0f, 0f) * guardRot;
                    break;
            }

            InterpolateKeyframe(t, windupEnd, hitApex, false, windupPos, windupRot,
                strikePos, strikeRot, guardPos, guardRot, out outPos, out outRot);
        }

        /// <summary>
        /// Secuencia táctica de golpes para Lanza / Polearm (WeaponAnimationStyle.Polearm).
        /// </summary>
        private void GetPolearmKeyframe(int comboIndex, float t, Vector3 guardPos, Quaternion guardRot,
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
                    // GOLPE 1: TAJO DIAGONAL DE SONDEO
                    windupEnd = 0.22f;
                    hitApex = 0.44f;
                    windupPos = guardPos + new Vector3(0.06f, 0.08f, -0.08f);
                    windupRot = Quaternion.Euler(50f, 25f, -10f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.32f, -0.06f, 0.02f);
                    strikeRot = Quaternion.Euler(70f, -35f, 10f) * guardRot;
                    break;

                case 1:
                    // GOLPE 2: BARRIDO BAJO CIRCULAR
                    windupEnd = 0.20f;
                    hitApex = 0.44f;
                    windupPos = guardPos + new Vector3(-0.30f, -0.08f, -0.06f);
                    windupRot = Quaternion.Euler(65f, -35f, -10f) * guardRot;
                    strikePos = guardPos + new Vector3(0.04f, -0.04f, 0.02f);
                    strikeRot = Quaternion.Euler(75f, 35f, 10f) * guardRot;
                    break;

                case 2:
                    // GOLPE 3: PUNZADA ALTA AL PECHO
                    windupEnd = 0.20f;
                    hitApex = 0.42f;
                    isThrust = true;
                    windupPos = guardPos + new Vector3(-0.06f, 0.00f, -0.12f);
                    windupRot = Quaternion.Euler(85f, 0f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.14f, 0.02f, 0.16f);
                    strikeRot = Quaternion.Euler(85f, 0f, 0f) * guardRot;
                    break;

                case 3:
                    // GOLPE 4: BARRIDO LATERAL DE CONTROL
                    windupEnd = 0.24f;
                    hitApex = 0.47f;
                    windupPos = guardPos + new Vector3(-0.30f, 0.02f, -0.06f);
                    windupRot = Quaternion.Euler(68f, -35f, -10f) * guardRot;
                    strikePos = guardPos + new Vector3(0.04f, 0.02f, 0.02f);
                    strikeRot = Quaternion.Euler(72f, 35f, 10f) * guardRot;
                    break;

                default: // case 4
                    // GOLPE 5: ESTOCADA LARGA FINAL DE LA LANZA
                    windupEnd = 0.30f;
                    hitApex = 0.50f;
                    isThrust = true;
                    windupPos = guardPos + new Vector3(-0.06f, 0.00f, -0.12f);
                    windupRot = Quaternion.Euler(88f, 0f, 0f) * guardRot;
                    strikePos = guardPos + new Vector3(-0.16f, 0.02f, 0.18f);
                    strikeRot = Quaternion.Euler(88f, 0f, 0f) * guardRot;
                    break;
            }

            InterpolateKeyframe(t, windupEnd, hitApex, isThrust, windupPos, windupRot,
                strikePos, strikeRot, guardPos, guardRot, out outPos, out outRot);
        }
    }
}
