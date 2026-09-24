using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class WeaponComboAnimator
    {
        /// <summary>
        /// Secuencia cinemática de 5 tajos con el filo cortante para Hacha de Combate (WeaponAnimationStyle.Axe1H).
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
    }
}
