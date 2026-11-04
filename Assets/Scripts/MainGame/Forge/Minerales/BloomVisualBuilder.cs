using UnityEngine;
using ForjaGame.Resources;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Constructor procedural 3D del modelo de la Lupa (color real de la aleación y negro
    /// proporcional únicamente a las impurezas del horno) y del Lingote prensado.
    /// </summary>
    public static class BloomVisualBuilder
    {
        public static Color ResolveCompositionColor(AlloyComposition comp)
        {
            if (comp == null || comp.ingredients == null || comp.ingredients.Count == 0)
                return MetalProperties.GetMetalColor(MetalType.Iron);

            float r = 0f, g = 0f, b = 0f, weightSum = 0f;
            for (int i = 0; i < comp.ingredients.Count; i++)
            {
                var ing = comp.ingredients[i];
                float w = ing.count > 0 ? ing.count : Mathf.Max(1f, ing.percentage);
                Color c = MetalProperties.GetMetalColor(ing.metal);
                r += c.r * w;
                g += c.g * w;
                b += c.b * w;
                weightSum += w;
            }
            return weightSum > 0f ? new Color(r / weightSum, g / weightSum, b / weightSum, 1f) : Color.gray;
        }

        public static Color ResolveBloomDisplayColor(BloomData bloom)
        {
            Color alloy = ResolveCompositionColor(bloom != null ? bloom.composition : null);
            float imp = bloom != null ? Mathf.Clamp01(bloom.ImpurityPercentage / 100f) : 0f;
            return Color.Lerp(alloy, new Color(0.05f, 0.05f, 0.05f, 1f), imp * 0.65f);
        }

        public static float GetSizeScale(ItemSize size) => size switch
        {
            ItemSize.Small => 0.80f,
            ItemSize.Large => 1.20f,
            _ => 1.00f
        };

        /// <summary>
        /// Crea el modelo 3D de la Lupa: su color base es el color exacto de la aleación;
        /// el color negro solo aparece en proporción a las impurezas acumuladas en el horno.
        /// </summary>
        public static GameObject CreateBloomVisual(BloomData bloom, Transform parent = null)
        {
            GameObject root = new GameObject("Bloom3D_Visual");
            if (parent != null) root.transform.SetParent(parent, false);

            ItemSize size = bloom != null ? bloom.size : ItemSize.Medium;
            float sizeMult = GetSizeScale(size);
            Color alloyColor = ResolveCompositionColor(bloom != null ? bloom.composition : null);
            float impurityRatio = bloom != null ? Mathf.Clamp01(bloom.ImpurityPercentage / 100f) : 0f;
            int seed = bloom != null && !string.IsNullOrEmpty(bloom.Id) ? bloom.Id.GetHashCode() : 1337;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            Material coreMat = new Material(lit) { name = "Mat_BloomCore" };
            if (impurityRatio <= 0.001f)
            {
                SetSurface(coreMat, alloyColor, metallic: 0.86f, smoothness: 0.56f, emission: alloyColor * 0.16f);
            }
            else
            {
                Texture2D marbledTex = CreateImpurityMarbledTexture(alloyColor, impurityRatio, seed);
                SetSurface(coreMat, Color.white,
                    metallic: Mathf.Lerp(0.86f, 0.22f, impurityRatio),
                    smoothness: Mathf.Lerp(0.56f, 0.16f, impurityRatio),
                    emission: alloyColor * (0.14f * (1f - impurityRatio)));
                coreMat.mainTexture = marbledTex;
                if (coreMat.HasProperty("_BaseMap")) coreMat.SetTexture("_BaseMap", marbledTex);
                if (coreMat.HasProperty("_MainTex")) coreMat.SetTexture("_MainTex", marbledTex);
            }

            GameObject core = new GameObject("BloomCore", typeof(MeshFilter), typeof(MeshRenderer));
            core.transform.SetParent(root.transform, false);
            Mesh coreMesh = OreMeshGenerator.CreatePolyhedralRockMesh(
                0.28f * sizeMult, sides: 9, new Vector3(1.22f, 0.68f, 0.96f), jitter: 0.22f, seed, "BloomCoreMesh");
            core.GetComponent<MeshFilter>().sharedMesh = coreMesh;
            core.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            // Eleva el núcleo para que la base del Bloom descanse sobre el ancla (Y >= 0) sin incrustarse en la mesada
            core.transform.localPosition = new Vector3(0f, 0.235f * sizeMult, 0f);

            // Relieves metálicos del propio color de la aleación para dar volumen esponjoso
            for (int i = 0; i < 3; i++)
            {
                GameObject facet = new GameObject($"AlloyLobe_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                facet.transform.SetParent(core.transform, false);
                float ang = i * Mathf.PI * 2f / 3f + 0.4f;
                facet.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.08f * sizeMult, 0.018f * sizeMult, Mathf.Sin(ang) * 0.068f * sizeMult);
                facet.transform.localRotation = Quaternion.Euler(20f * i, ang * Mathf.Rad2Deg, 14f);
                facet.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.056f * sizeMult, seed + i * 19);
                facet.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            }

            // Costras negras de escoria: SOLO existen si hubo impurezas en el horno
            if (impurityRatio > 0.001f)
            {
                Material slagMat = new Material(lit) { name = "Mat_BloomImpurityBlack" };
                SetSurface(slagMat, new Color(0.03f, 0.03f, 0.03f, 1f), metallic: 0.08f, smoothness: 0.12f, emission: Color.black);

                int slagCount = Mathf.Clamp(Mathf.CeilToInt(impurityRatio * 12f), 1, 12);
                float crustSize = Mathf.Lerp(0.028f, 0.060f, impurityRatio) * sizeMult;
                for (int i = 0; i < slagCount; i++)
                {
                    GameObject slag = new GameObject($"SlagCrust_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                    slag.transform.SetParent(core.transform, false);
                    float ang = i * Mathf.PI * 2f / slagCount + 0.2f;
                    float rad = 0.105f * sizeMult;
                    float yOff = ((i % 2 == 0) ? 0.032f : -0.024f) * sizeMult;
                    slag.transform.localPosition = new Vector3(Mathf.Cos(ang) * rad, yOff, Mathf.Sin(ang) * rad * 0.82f);
                    slag.transform.localRotation = Quaternion.Euler(18f * i, ang * Mathf.Rad2Deg + 30f, 12f * i);
                    slag.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(crustSize, seed + 101 + i * 23);
                    slag.GetComponent<MeshRenderer>().sharedMaterial = slagMat;
                }
            }

            return root;
        }

        private static Texture2D CreateImpurityMarbledTexture(Color alloyColor, float impurityRatio, int seed)
        {
            const int res = 64;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "Tex_BloomImpurityMarble",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color charcoalBlack = new Color(0.025f, 0.025f, 0.028f, 1f);
            float ox = (Mathf.Abs(seed % 997) * 0.137f) + 3.1f;
            float oy = (Mathf.Abs((seed / 13) % 991) * 0.173f) + 7.4f;
            Color[] pixels = new Color[res * res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / res;
                    float v = (float)y / res;
                    float n1 = Mathf.PerlinNoise(ox + u * 5.5f, oy + v * 5.5f);
                    float n2 = Mathf.PerlinNoise(ox + 19.3f + u * 13f, oy + 11.7f + v * 13f);
                    float n = Mathf.Clamp01(n1 * 0.72f + n2 * 0.28f);
                    float edge = impurityRatio;
                    float mask = Mathf.Clamp01((edge - n + 0.04f) / 0.08f);
                    pixels[y * res + x] = Color.Lerp(alloyColor, charcoalBlack, mask);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        public static GameObject CreateIngotVisual(IngotData ingot, Transform parent = null)
        {
            GameObject root = new GameObject("Ingot3D_Visual", typeof(MeshFilter), typeof(MeshRenderer));
            if (parent != null) root.transform.SetParent(parent, false);

            ItemSize size = ingot != null ? ingot.size : ItemSize.Medium;
            float s = GetSizeScale(size);
            Vector3 dims = new Vector3(0.34f * s, 0.085f * s, 0.16f * s);

            Mesh ingotMesh = SubdividedIngotMeshGenerator.CreateSubdividedIngot(16, 4, 10, dims);
            root.GetComponent<MeshFilter>().sharedMesh = ingotMesh;

            Color metalCol = ResolveCompositionColor(ingot != null ? ingot.composition : null);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            Material ingotMat = new Material(lit) { name = "Mat_PressedIngot" };
            SetSurface(ingotMat, metalCol, metallic: 0.90f, smoothness: 0.74f, emission: metalCol * 0.14f);
            root.GetComponent<MeshRenderer>().sharedMaterial = ingotMat;

            root.transform.localPosition = new Vector3(0f, dims.y * 0.5f, 0f);
            return root;
        }

        public static GameObject CreateRawOreProp(MetalType metal, int seed, Transform parent = null, float scale = 0.24f)
        {
            string itemId = metal.ToString();
            GameObject oreRoot = new GameObject($"RawOreProp_{itemId}", typeof(MeshFilter), typeof(MeshRenderer));
            if (parent != null) oreRoot.transform.SetParent(parent, false);

            oreRoot.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateOreDropRockMesh(itemId, scale, seed);
            oreRoot.GetComponent<MeshRenderer>().sharedMaterial = ResourceVisualBuilder.CreateRockMaterial();

            Material oreMat = ResourceVisualBuilder.CreateResourceMaterial(itemId);
            int incrustCount = metal == MetalType.Gold ? 2 : 3;
            for (int i = 0; i < incrustCount; i++)
            {
                GameObject inc = new GameObject($"OreVein_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                inc.transform.SetParent(oreRoot.transform, false);
                float ang = i * Mathf.PI * 2f / incrustCount + 0.35f;
                float r = scale * 0.29f;
                inc.transform.localPosition = new Vector3(Mathf.Cos(ang) * r, (i - 1) * scale * 0.12f, Mathf.Sin(ang) * r);
                inc.transform.localRotation = Quaternion.Euler(22f * (i + 1), ang * Mathf.Rad2Deg + 15f, 14f * i);
                inc.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateOreInclusionMesh(itemId, scale * 0.46f, seed + i * 31);
                inc.GetComponent<MeshRenderer>().sharedMaterial = oreMat;
            }
            return oreRoot;
        }

        private static void SetSurface(Material mat, Color baseCol, float metallic, float smoothness, Color emission)
        {
            mat.color = baseCol;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0.001f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
            }
        }
    }
}
