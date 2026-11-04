using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Resources
{
    public static partial class OreMeshGenerator
    {
        public static Mesh CreatePolyhedralRockMesh(float scale, int sides, Vector3 axisScale, float jitter, int seed, string name)
        {
            var rnd = new System.Random(seed);
            List<Vector3> verts = new();
            Vector3 center = Vector3.zero;

            // 0: Polo superior, 1: Polo inferior
            float topY = 0.45f * axisScale.y * (1.0f + (float)rnd.NextDouble() * jitter);
            float botY = -0.45f * axisScale.y * (1.0f + (float)rnd.NextDouble() * jitter);
            verts.Add(new Vector3(0f, topY, 0f) * scale);
            verts.Add(new Vector3(0f, botY, 0f) * scale);

            // Anillo 1: Banda superior
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float r = (0.40f + ((float)rnd.NextDouble() - 0.5f) * jitter) * scale;
                float y = (0.16f + ((float)rnd.NextDouble() - 0.5f) * jitter) * axisScale.y * scale;
                verts.Add(new Vector3(Mathf.Cos(a) * r * axisScale.x, y, Mathf.Sin(a) * r * axisScale.z));
            }

            // Anillo 2: Banda inferior (desfasada medio paso para triangulación óptima)
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / sides;
                float r = (0.42f + ((float)rnd.NextDouble() - 0.5f) * jitter) * scale;
                float y = (-0.16f + ((float)rnd.NextDouble() - 0.5f) * jitter) * axisScale.y * scale;
                verts.Add(new Vector3(Mathf.Cos(a) * r * axisScale.x, y, Mathf.Sin(a) * r * axisScale.z));
            }

            List<int> tris = new();
            // Casquete superior
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                AddFaceOutward(verts, tris, 0, 2 + i, 2 + next, center);
            }

            // Cinturón intermedio (tiras triangulares alternadas)
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int u1 = 2 + i;
                int u2 = 2 + next;
                int d1 = 2 + sides + i;
                int d2 = 2 + sides + next;
                AddFaceOutward(verts, tris, u1, d1, u2, center);
                AddFaceOutward(verts, tris, u2, d1, d2, center);
            }

            // Casquete inferior
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                AddFaceOutward(verts, tris, 1, 2 + sides + next, 2 + sides + i, center);
            }

            return BuildFlatShadedMesh(verts, tris, $"{name}_{seed}");
        }

        public static Mesh CreateOreDropRockMesh(string itemId, float scale, int seed)
        {
            string id = itemId != null ? itemId.ToLowerInvariant() : "iron";
            if (id.Contains("copper") || id.Contains("cobre"))
            {
                // Cobre: Roca alargada y asimétrica con caras piramidales
                return CreatePolyhedralRockMesh(scale, sides: 6, new Vector3(0.70f, 0.85f, 1.40f), jitter: 0.20f, seed, "RockCopperDrop");
            }
            if (id.Contains("gold") || id.Contains("oro"))
            {
                // Oro: Canto rodado de cuarzo multifacetado orgánico
                return CreatePolyhedralRockMesh(scale, sides: 8, new Vector3(1.15f, 0.80f, 0.95f), jitter: 0.12f, seed, "RockGoldDrop");
            }
            if (id.Contains("coal") || id.Contains("carbon"))
            {
                // Carbón: Bloque sedimentario tabular con lajas escalonadas
                return CreatePolyhedralRockMesh(scale, sides: 6, new Vector3(1.35f, 0.50f, 1.05f), jitter: 0.16f, seed, "RockCoalDrop");
            }
            // Hierro: Nódulo denso poliédrico de 7 caras angulares
            return CreatePolyhedralRockMesh(scale, sides: 7, new Vector3(0.95f, 0.95f, 0.95f), jitter: 0.24f, seed, "RockIronDrop");
        }

        public static Mesh CreateOreInclusionMesh(string itemId, float scale, int seed)
        {
            string id = itemId != null ? itemId.ToLowerInvariant() : "iron";
            if (id.Contains("copper") || id.Contains("cobre"))
            {
                // Cobre: Cristales de aguja y espículas prismáticas alargadas
                return CreateCrystalSpireMesh(scale * 0.45f, scale * 1.05f, seed);
            }
            if (id.Contains("gold") || id.Contains("oro"))
            {
                // Oro: Pepitas / vetas aplanadas de oro nativo incrustadas en las hendiduras
                return CreatePolyhedralRockMesh(scale, sides: 5, new Vector3(1.20f, 0.32f, 0.80f), jitter: 0.10f, seed, "OreInclusionGold");
            }
            if (id.Contains("coal") || id.Contains("carbon"))
            {
                // Carbón: Laja sedimentaria escalonada y tabular
                return CreatePolyhedralRockMesh(scale, sides: 5, new Vector3(1.35f, 0.25f, 1.10f), jitter: 0.12f, seed, "OreInclusionCoal");
            }
            // Hierro: Cristal romboédrico / octaédrico metálico denso
            return CreatePolyhedralRockMesh(scale, sides: 4, new Vector3(1.0f, 1.0f, 1.0f), jitter: 0.08f, seed, "OreInclusionIron");
        }
    }
}
