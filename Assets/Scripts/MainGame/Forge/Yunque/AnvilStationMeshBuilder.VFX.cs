using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Generador procedural de materiales y texturas VFX para la estación del yunque:
    /// Textura circular suave (falloff gaussiano anti-cuadrados), materiales de partículas URP y agua translúcida.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public static partial class AnvilStationMeshBuilder
    {
        private static Texture2D _softParticleTexture;

        public static Texture2D GetSoftParticleTexture()
        {
            if (_softParticleTexture != null) return _softParticleTexture;

            int size = 64;
            _softParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Tex_SoftParticle_Procedural",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / maxDist;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    alpha = alpha * alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            _softParticleTexture.SetPixels(pixels);
            _softParticleTexture.Apply();
            return _softParticleTexture;
        }

        public static Material CreateParticleMaterial(string name, bool isAdditive = false)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit")
                    ?? Shader.Find("Mobile/Particles/Alpha Blended")
                    ?? Shader.Find("Sprites/Default");

            Material m = new Material(s) { name = name };
            Texture2D tex = GetSoftParticleTexture();
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);

            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", isAdditive ? 1f : 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)(isAdditive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);

            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (isAdditive)
            {
                m.EnableKeyword("_BLENDMODE_ADD");
                m.DisableKeyword("_BLENDMODE_ALPHA");
            }
            else
            {
                m.EnableKeyword("_BLENDMODE_ALPHA");
                m.DisableKeyword("_BLENDMODE_ADD");
            }
            return m;
        }

        public static Material CreateQuenchWaterMaterial()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = "Mat_QuenchWater" };
            Color waterColor = new Color(0.12f, 0.48f, 0.72f, 1.0f);
            m.color = waterColor;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", waterColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.95f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);

            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.04f, 0.15f, 0.25f));
            return m;
        }

        public static Material CreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.35f, Color emission = default)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = name, color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);

            if (emission != default)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        public static ParticleSystem CreateBurstSystem(Transform parent, bool isSawdust)
        {
            var go = new GameObject(isSawdust ? "AnvilSawdustVFX" : "AnvilHitSparksVFX");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = isSawdust ? new ParticleSystem.MinMaxCurve(0.28f, 0.62f) : new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
            main.startSpeed = isSawdust ? new ParticleSystem.MinMaxCurve(0.85f, 2.3f) : new ParticleSystem.MinMaxCurve(2.2f, 5.8f);
            main.startSize = isSawdust ? new ParticleSystem.MinMaxCurve(0.016f, 0.036f) : new ParticleSystem.MinMaxCurve(0.014f, 0.032f);
            main.gravityModifier = isSawdust ? 0.95f : 1.65f;
            main.startColor = isSawdust
                ? new ParticleSystem.MinMaxGradient(new Color(0.84f, 0.66f, 0.38f, 0.96f), new Color(0.55f, 0.35f, 0.16f, 0.92f))
                : new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.38f, 1f), new Color(1f, 0.42f, 0.06f, 1f));
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.035f;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = isSawdust ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            if (!isSawdust) { psr.velocityScale = 0.06f; psr.lengthScale = 2.4f; }
            psr.sharedMaterial = CreateParticleMaterial(isSawdust ? "Mat_WoodSawdust" : "Mat_AnvilHitSparks", !isSawdust);
            return ps;
        }
    }
}

