using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Generador procedural de mallas poliédricas orgánicas para yacimientos minerales.
    /// Garantiza sólidos estancos sin caras faltantes ni normales invertidas.
    /// </summary>
    public static partial class OreMeshGenerator
    {
        private static void AddFaceOutward(List<Vector3> verts, List<int> tris, int i0, int i1, int i2, Vector3 center)
        {
            Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
            Vector3 faceCenter = (v0 + v1 + v2) / 3f;
            if (Vector3.Dot(normal, faceCenter - center) >= 0f)
            {
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
            }
            else
            {
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
            }
        }

        private static void AddQuadOutward(List<Vector3> verts, List<int> tris, int i0, int i1, int i2, int i3, Vector3 center)
        {
            AddFaceOutward(verts, tris, i0, i1, i2, center);
            AddFaceOutward(verts, tris, i0, i2, i3, center);
        }

        public static Mesh CreateRockBaseMesh(float radius, float height, int seed)
        {
            var rnd = new System.Random(seed);
            List<Vector3> verts = new();
            Vector3 center = new Vector3(0f, height * 0.40f, 0f);

            verts.Add(Vector3.zero); // 0: Centro de base inferior
            int sides = 8;

            // Anillo 1: Base en el suelo (1..8)
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float r = radius * (1.10f + (float)rnd.NextDouble() * 0.30f);
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }

            // Anillo 2: Flanco medio (9..16)
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.25f) * Mathf.PI * 2f / sides;
                float r = radius * (0.80f + (float)rnd.NextDouble() * 0.25f);
                float y = height * (0.35f + (float)rnd.NextDouble() * 0.18f);
                verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }

            // Anillo 3: Cresta superior facetada (17..24)
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float r = radius * (0.42f + (float)rnd.NextDouble() * 0.22f);
                float y = height * (0.75f + (float)rnd.NextDouble() * 0.20f);
                verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }

            // Vértice 25: Cúspide de la roca
            verts.Add(new Vector3(
                ((float)rnd.NextDouble() - 0.5f) * 0.25f * radius,
                height * (0.95f + (float)rnd.NextDouble() * 0.15f),
                ((float)rnd.NextDouble() - 0.5f) * 0.25f * radius));

            List<int> tris = new();
            int apexIdx = 25;

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int r1_a = 1 + i, r1_b = 1 + next;
                int r2_a = 9 + i, r2_b = 9 + next;
                int r3_a = 17 + i, r3_b = 17 + next;

                AddFaceOutward(verts, tris, 0, r1_b, r1_a, center);
                AddQuadOutward(verts, tris, r1_a, r2_a, r2_b, r1_b, center);
                AddQuadOutward(verts, tris, r2_a, r3_a, r3_b, r2_b, center);
                AddFaceOutward(verts, tris, r3_a, apexIdx, r3_b, center);
            }

            return BuildFlatShadedMesh(verts, tris, $"VeinStone_{seed}");
        }

        public static Mesh CreateCrystalSpireMesh(float width, float height, int seed)
        {
            var rnd = new System.Random(seed);
            List<Vector3> verts = new();
            Vector3 center = new Vector3(0f, height * 0.40f, 0f);

            verts.Add(new Vector3(0f, height, 0f)); // 0: Punta
            verts.Add(Vector3.zero); // 1: Fondo central

            int sides = 5;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float r = width * (0.80f + (float)rnd.NextDouble() * 0.35f);
                verts.Add(new Vector3(Mathf.Cos(a) * r, height * 0.45f, Mathf.Sin(a) * r)); // 2..6
            }
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float r = width * 0.70f;
                verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r)); // 7..11
            }

            List<int> tris = new();
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int mid_a = 2 + i, mid_b = 2 + next;
                int bot_a = 7 + i, bot_b = 7 + next;

                AddFaceOutward(verts, tris, mid_a, 0, mid_b, center);
                AddQuadOutward(verts, tris, bot_a, mid_a, mid_b, bot_b, center);
                AddFaceOutward(verts, tris, 1, bot_b, bot_a, center);
            }

            return BuildFlatShadedMesh(verts, tris, $"CrystalSpire_{seed}");
        }

        public static Mesh CreateFacetedChunkMesh(float scale, int seed)
        {
            var rnd = new System.Random(seed);
            List<Vector3> verts = new();
            Vector3 center = Vector3.zero;

            for (int i = 0; i < 8; i++)
            {
                float x = ((i & 1) == 0 ? -1f : 1f) * (0.75f + (float)rnd.NextDouble() * 0.5f) * scale;
                float y = ((i & 2) == 0 ? -1f : 1f) * (0.75f + (float)rnd.NextDouble() * 0.5f) * scale;
                float z = ((i & 4) == 0 ? -1f : 1f) * (0.75f + (float)rnd.NextDouble() * 0.5f) * scale;
                verts.Add(new Vector3(x, y, z));
            }

            List<int> tris = new();
            AddQuadOutward(verts, tris, 0, 2, 3, 1, center); // Front
            AddQuadOutward(verts, tris, 5, 7, 6, 4, center); // Back
            AddQuadOutward(verts, tris, 2, 6, 7, 3, center); // Top
            AddQuadOutward(verts, tris, 0, 1, 5, 4, center); // Bottom
            AddQuadOutward(verts, tris, 0, 4, 6, 2, center); // Left
            AddQuadOutward(verts, tris, 1, 3, 7, 5, center); // Right

            return BuildFlatShadedMesh(verts, tris, $"FacetedChunk_{seed}");
        }

        public static Mesh BuildFlatShadedMesh(List<Vector3> verts, List<int> tris, string name)
        {
            Vector3[] flatVerts = new Vector3[tris.Count];
            Vector2[] flatUvs = new Vector2[tris.Count];
            int[] flatTris = new int[tris.Count];

            for (int i = 0; i < tris.Count; i++)
            {
                flatVerts[i] = verts[tris[i]];
                flatUvs[i] = new Vector2(flatVerts[i].x, flatVerts[i].z) * 0.5f;
                flatTris[i] = i;
            }

            Mesh m = new Mesh { name = name };
            m.vertices = flatVerts;
            m.uv = flatUvs;
            m.triangles = flatTris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
