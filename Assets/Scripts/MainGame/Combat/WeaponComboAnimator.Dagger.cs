using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class WeaponComboAnimator
    {
        private void GetDaggerKeyframe(int comboIndex, float t, Vector3 guardPos, Quaternion guardRot,
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
                    // Estocada corta desde la guardia frontal con holgura frontal (Z = 0.30m)
                    windupEnd = 0.15f;
                    hitApex = 0.38f;
                    isThrust = true;
                    windupPos = new Vector3(0.20f, -0.02f, 0.30f);
                    windupRot = Quaternion.Euler(88f, 0f, 0f) * guardRot;
                    strikePos = new Vector3(0.08f, 0.02f, 0.44f);
                    strikeRot = Quaternion.Euler(88f, 0f, 0f) * guardRot;
                    break;

                case 1:
                    // Estocada táctica de ángulo inverso manteniendo separación del pecho
                    windupEnd = 0.16f;
                    hitApex = 0.40f;
                    isThrust = true;
                    windupPos = new Vector3(-0.08f, 0.06f, 0.32f);
                    windupRot = Quaternion.Euler(82f, -20f, -5f) * guardRot;
                    strikePos = new Vector3(0.02f, 0.06f, 0.42f);
                    strikeRot = Quaternion.Euler(88f, 0f, 0f) * guardRot;
                    break;

                case 2:
                    // Corte corto horizontal para cambiar la linea de ataque con amplio paso
                    windupEnd = 0.17f;
                    hitApex = 0.42f;
                    windupPos = new Vector3(0.24f, 0.08f, 0.30f);
                    windupRot = Quaternion.Euler(78f, 32f, -6f) * guardRot;
                    strikePos = new Vector3(-0.10f, 0.04f, 0.42f);
                    strikeRot = Quaternion.Euler(82f, -28f, 6f) * guardRot;
                    break;

                case 3:
                    // Estocada ascendente, recogiendo sin invadir el tórax (Z = 0.28m)
                    windupEnd = 0.18f;
                    hitApex = 0.43f;
                    isThrust = true;
                    windupPos = new Vector3(0.12f, -0.08f, 0.28f);
                    windupRot = Quaternion.Euler(100f, -10f, 0f) * guardRot;
                    strikePos = new Vector3(0.04f, 0.14f, 0.42f);
                    strikeRot = Quaternion.Euler(78f, 0f, 0f) * guardRot;
                    break;

                default:
                    // Remate: estocada frontal profunda dentro del alcance anatómico limpio
                    windupEnd = 0.22f;
                    hitApex = 0.46f;
                    isThrust = true;
                    windupPos = new Vector3(0.20f, 0.04f, 0.28f);
                    windupRot = Quaternion.Euler(90f, 0f, 0f) * guardRot;
                    strikePos = new Vector3(0.06f, 0.04f, 0.46f);
                    strikeRot = Quaternion.Euler(90f, 0f, 0f) * guardRot;
                    break;
            }

            InterpolateKeyframe(t, windupEnd, hitApex, isThrust, windupPos, windupRot,
                strikePos, strikeRot, guardPos, guardRot, out outPos, out outRot);
        }
    }
}
