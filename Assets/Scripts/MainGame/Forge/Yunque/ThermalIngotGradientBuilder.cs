using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Generador limpio y simple de textura metálica biselada y curva térmica continua para lingotes 3D.
    /// Elimina ruidos de impurezas y transiciones bruscas: las aristas conservan un sombreado biselado sutil
    /// mientras el color y la incandescencia progresan de forma continua e intuitiva con la temperatura.
    /// </summary>
    public static class ThermalIngotGradientBuilder
    {
        public const int TexSize = 64;

        public static void EnsureTextures(ref Texture2D baseTex, ref Texture2D emitTex)
        {
            if (baseTex == null)
                baseTex = CreateCleanBevelTexture("IngotThermalBase", Color.white, 0.80f, 1.00f);

            if (emitTex == null)
                emitTex = CreateCleanBevelTexture("IngotThermalEmit", Color.white, 0.36f, 1.00f);
        }

        public static void EvaluateContinuousThermalState(
            Color coldMetal, float temp,
            out Color albedoTint, out Color emissionColor, out float metallic, out float smoothness)
        {
            // Curva continua monotónica por tramos suaves:
            // Frío (<=180°C) -> Templado (380°C) -> Cereza (650°C) -> Naranja (920°C) -> Ámbar Dorado (1180°C) -> Sobrecalentado (1400°C)
            Color temperedTint = Color.Lerp(coldMetal, new Color(0.68f, 0.54f, 0.38f, 1f), 0.38f);
            Color cCherry = new Color(0.76f, 0.14f, 0.06f, 1f);
            Color cOrange = new Color(0.95f, 0.42f, 0.08f, 1f);
            Color cAmber = new Color(1.00f, 0.74f, 0.22f, 1f);
            Color cOverheat = new Color(1.00f, 0.90f, 0.54f, 1f);

            Color eCherry = new Color(0.32f, 0.035f, 0.01f, 1f);
            Color eOrange = new Color(0.56f, 0.14f, 0.02f, 1f);
            Color eAmber = new Color(0.72f, 0.32f, 0.06f, 1f);
            Color eOverheat = new Color(0.80f, 0.48f, 0.14f, 1f);

            if (temp <= 180f)
            {
                albedoTint = coldMetal;
                emissionColor = Color.black;
            }
            else if (temp <= 380f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (temp - 180f) / 200f);
                albedoTint = Color.Lerp(coldMetal, temperedTint, k);
                emissionColor = Color.Lerp(Color.black, eCherry * 0.32f, Mathf.SmoothStep(0f, 1f, (temp - 240f) / 140f));
            }
            else if (temp <= 650f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (temp - 380f) / 270f);
                albedoTint = Color.Lerp(temperedTint, cCherry, k);
                emissionColor = Color.Lerp(eCherry * 0.32f, eCherry, k);
            }
            else if (temp <= 920f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (temp - 650f) / 270f);
                albedoTint = Color.Lerp(cCherry, cOrange, k);
                emissionColor = Color.Lerp(eCherry, eOrange, k);
            }
            else if (temp <= 1180f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (temp - 920f) / 260f);
                albedoTint = Color.Lerp(cOrange, cAmber, k);
                emissionColor = Color.Lerp(eOrange, eAmber, k);
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (temp - 1180f) / 220f);
                albedoTint = Color.Lerp(cAmber, cOverheat, k);
                emissionColor = Color.Lerp(eAmber, eOverheat, k);
            }

            float heatNorm = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((temp - 260f) / 860f));
            metallic = Mathf.Lerp(0.88f, 0.34f, heatNorm);
            smoothness = Mathf.Lerp(0.66f, 0.44f, heatNorm);
        }

        public static Texture2D CreateBrushedIngotTexture(Color metalColor) =>
            CreateCleanBevelTexture("IngotColdBrushed", metalColor, 0.80f, 1.02f);

        private static Texture2D CreateCleanBevelTexture(string texName, Color tint, float edgeShade, float faceShade)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                name = texName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] px = new Color[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
            {
                float v = y / (float)(TexSize - 1);
                float dv = Mathf.Min(v, 1f - v) * 2f;

                for (int x = 0; x < TexSize; x++)
                {
                    float u = x / (float)(TexSize - 1);
                    float du = Mathf.Min(u, 1f - u) * 2f;
                    float bevel = Mathf.SmoothStep(0f, 0.36f, Mathf.Min(du, dv));
                    float shade = Mathf.Lerp(edgeShade, faceShade, bevel);
                    Color c = tint * shade;
                    c.a = 1f;
                    px[y * TexSize + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }
    }
}
