using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Constructor visual procedural para vetas minerales con geometrías orgánicas diferenciadas por recurso.
    /// Soporta pistas visuales de daño progresivo (desprendimiento de fragmentos y fracturas visibles).
    /// </summary>
    public static partial class ResourceVisualBuilder
    {
        public static Color GetColorForResource(string itemId) => itemId?.ToLowerInvariant() switch
        {
            "copper" or "cobre" => new Color(0.92f, 0.46f, 0.24f),  // Naranja cobrizo brillante
            "iron" or "hierro"   => new Color(0.72f, 0.76f, 0.82f),  // Gris metálico pizarra
            "gold" or "oro"      => new Color(1.00f, 0.82f, 0.14f),  // Oro cálido resplandeciente
            "coal" or "carbon"   => new Color(0.12f, 0.13f, 0.15f),  // Negro antracita profundo
            "wood" or "madera"   => new Color(0.50f, 0.33f, 0.18f),  // Madera roble
            _ => new Color(0.40f, 0.70f, 0.95f)
        };

        public static (float metallic, float smoothness) GetMaterialProperties(string itemId) => itemId?.ToLowerInvariant() switch
        {
            "copper" or "cobre" => (0.88f, 0.65f),
            "iron" or "hierro"   => (0.92f, 0.70f),
            "gold" or "oro"      => (0.98f, 0.92f),
            "coal" or "carbon"   => (0.15f, 0.40f),
            "wood" or "madera"   => (0.02f, 0.15f),
            _ => (0.50f, 0.50f)
        };

        public static Material CreateResourceMaterial(string itemId)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { name = $"Mat_{itemId}" };
            Color col = GetColorForResource(itemId);
            var (met, smooth) = GetMaterialProperties(itemId);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            mat.color = col;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", met);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                float emissionIntensity = (itemId != null && (itemId.ToLowerInvariant().Contains("gold") || itemId.ToLowerInvariant().Contains("oro"))) ? 0.48f : 0.30f;
                mat.SetColor("_EmissionColor", col * emissionIntensity);
            }
            return mat;
        }

        public static Material CreateRockMaterial()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material rockMat = new Material(s) { name = "Mat_VeinBedrock" };
            Color stoneCol = new Color(0.25f, 0.26f, 0.28f);
            if (rockMat.HasProperty("_BaseColor")) rockMat.SetColor("_BaseColor", stoneCol);
            rockMat.color = stoneCol;
            if (rockMat.HasProperty("_Smoothness")) rockMat.SetFloat("_Smoothness", 0.20f);
            return rockMat;
        }

        public static void CreateVeinVisual(GameObject root, string itemId, out List<GameObject> breakableClusters)
        {
            breakableClusters = new List<GameObject>();
            if (root == null) return;

            int seed = root.GetInstanceID() ^ (itemId != null ? itemId.GetHashCode() : 0);
            string idLower = itemId != null ? itemId.ToLowerInvariant() : "iron";

            // 1. Roca base facetada y orgánica
            GameObject rockBase = new GameObject("RockBase", typeof(MeshFilter), typeof(MeshRenderer));
            rockBase.transform.SetParent(root.transform, false);
            rockBase.transform.localPosition = Vector3.zero;
            rockBase.transform.localRotation = Quaternion.Euler(0f, (seed % 360), 0f);

            Mesh rockMesh = OreMeshGenerator.CreateRockBaseMesh(1.10f, 0.65f, seed);
            rockBase.GetComponent<MeshFilter>().sharedMesh = rockMesh;
            rockBase.GetComponent<MeshRenderer>().sharedMaterial = CreateRockMaterial();

            // 2. Geometría distintiva y única por mineral
            Material oreMat = CreateResourceMaterial(itemId);

            if (idLower.Contains("copper") || idLower.Contains("cobre"))
                BuildCopperSpires(root.transform, oreMat, seed, breakableClusters);
            else if (idLower.Contains("iron") || idLower.Contains("hierro"))
                BuildIronNodules(root.transform, oreMat, seed, breakableClusters);
            else if (idLower.Contains("coal") || idLower.Contains("carbon"))
                BuildCoalStrata(root.transform, oreMat, seed, breakableClusters);
            else if (idLower.Contains("gold") || idLower.Contains("oro"))
                BuildGoldVein(root.transform, oreMat, seed, breakableClusters);
            else
                BuildDefaultVein(root.transform, oreMat, seed, breakableClusters);

            // 3. Luz ambiental suave característica
            GameObject lightObj = new GameObject("VeinLight", typeof(Light));
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            Light l = lightObj.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = GetColorForResource(itemId);
            l.range = 3.5f;
            l.intensity = 0.85f;
        }

        private static void BuildCopperSpires(Transform root, Material mat, int seed, List<GameObject> clusters)
        {
            int spireCount = 6;
            for (int i = 0; i < spireCount; i++)
            {
                GameObject spire = new GameObject($"CopperSpire_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                spire.transform.SetParent(root, false);

                float angle = i * Mathf.PI * 2f / spireCount + (i * 0.2f);
                float r = 0.42f + (i % 2) * 0.12f;
                spire.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.28f + (i % 3) * 0.10f, Mathf.Sin(angle) * r);
                spire.transform.localRotation = Quaternion.Euler(25f * (i % 2 == 0 ? 1 : -1), angle * Mathf.Rad2Deg, 25f);

                spire.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(0.18f, 0.42f, seed + i * 17);
                spire.GetComponent<MeshRenderer>().sharedMaterial = mat;
                clusters.Add(spire);
            }
        }

        private static void BuildIronNodules(Transform root, Material mat, int seed, List<GameObject> clusters)
        {
            int noduleCount = 5;
            for (int i = 0; i < noduleCount; i++)
            {
                GameObject nodule = new GameObject($"IronNodule_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                nodule.transform.SetParent(root, false);

                float angle = i * Mathf.PI * 2f / noduleCount;
                float r = 0.48f;
                nodule.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.22f + (i % 2) * 0.18f, Mathf.Sin(angle) * r);
                nodule.transform.localRotation = Quaternion.Euler(15f * i, angle * Mathf.Rad2Deg + 45f, 0f);

                nodule.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.20f, seed + i * 29);
                nodule.GetComponent<MeshRenderer>().sharedMaterial = mat;
                clusters.Add(nodule);
            }
        }

        private static void BuildCoalStrata(Transform root, Material mat, int seed, List<GameObject> clusters)
        {
            int blockCount = 6;
            for (int i = 0; i < blockCount; i++)
            {
                GameObject coal = new GameObject($"CoalStrata_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                coal.transform.SetParent(root, false);

                float angle = i * Mathf.PI * 2f / blockCount;
                float r = 0.38f + (i % 3) * 0.10f;
                coal.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.18f + i * 0.08f, Mathf.Sin(angle) * r);
                coal.transform.localRotation = Quaternion.Euler(10f, angle * Mathf.Rad2Deg, 12f * (i - 2));

                coal.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.18f, seed + i * 37);
                coal.GetComponent<MeshRenderer>().sharedMaterial = mat;
                clusters.Add(coal);
            }
        }

        private static void BuildGoldVein(Transform root, Material mat, int seed, List<GameObject> clusters)
        {
            int blockCount = 6;
            for (int i = 0; i < blockCount; i++)
            {
                GameObject gold = new GameObject($"GoldVein_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                gold.transform.SetParent(root, false);

                float angle = i * Mathf.PI * 2f / blockCount;
                float r = 0.38f + (i % 3) * 0.10f;
                gold.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.18f + i * 0.08f, Mathf.Sin(angle) * r);
                // Basado exactamente en carbón pero con rotación ligera distintiva
                gold.transform.localRotation = Quaternion.Euler(18f * (i % 2 == 0 ? 1f : -1f), angle * Mathf.Rad2Deg + 25f, 15f * (i - 2));

                gold.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.18f, seed + i * 43);
                gold.GetComponent<MeshRenderer>().sharedMaterial = mat;
                clusters.Add(gold);
            }
        }

        private static void BuildDefaultVein(Transform root, Material mat, int seed, List<GameObject> clusters)
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject chunk = new GameObject($"OreChunk_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                chunk.transform.SetParent(root, false);
                float a = i * Mathf.PI * 0.5f;
                chunk.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.35f, 0.6f, Mathf.Sin(a) * 0.35f);
                chunk.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.20f, seed + i);
                chunk.GetComponent<MeshRenderer>().sharedMaterial = mat;
                clusters.Add(chunk);
            }
        }
    }
}
