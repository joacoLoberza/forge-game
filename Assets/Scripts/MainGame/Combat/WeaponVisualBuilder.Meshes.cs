using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public static partial class WeaponVisualBuilder
    {
        public static Mesh CreateAxeHeadMesh(float headLength = 0.36f, float bladeHeight = 0.30f)
        {
            Mesh m = new Mesh { name = "ProceduralAxeHead_Mesh" };
            List<Vector3> verts = new();
            List<Vector2> uvs = new();
            List<int> tris = new();

            // 1. Ojo / Collar del hacha (abrazadera alrededor del mango)
            AddBoxCollar(verts, uvs, tris, new Vector3(0.12f, 0.075f, 0.08f), Vector3.zero);

            // 2. Cuello y filo creciente curvado frontal (+Z)
            int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                float z0 = Mathf.Lerp(0.04f, headLength, t0);
                float z1 = Mathf.Lerp(0.04f, headLength, t1);
                float h0 = Mathf.Lerp(0.06f, bladeHeight, t0 * t0);
                float h1 = Mathf.Lerp(0.06f, bladeHeight, t1 * t1);
                float w0 = Mathf.Lerp(0.035f, 0.004f, t0);
                float w1 = Mathf.Lerp(0.035f, 0.004f, t1);

                AddWedgeSection(verts, uvs, tris, z0, z1, h0, h1, w0, w1);
            }

            // 3. Cabeza trasera / Contrapeso plano (-Z)
            AddBoxCollar(verts, uvs, tris, new Vector3(0.09f, 0.065f, 0.10f), new Vector3(0f, 0f, -0.09f));

            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Mesh CreateHammerHeadMesh(float headLength = 0.28f, float faceRadius = 0.055f)
        {
            Mesh m = new Mesh { name = "ProceduralHammerHead_Mesh" };
            List<Vector3> verts = new();
            List<Vector2> uvs = new();
            List<int> tris = new();

            AddBoxCollar(verts, uvs, tris, new Vector3(0.13f, 0.085f, 0.09f), Vector3.zero);
            AddBoxCollar(verts, uvs, tris, new Vector3(0.10f, 0.075f, headLength * 0.5f), new Vector3(0f, 0f, headLength * 0.28f));
            AddBoxCollar(verts, uvs, tris, new Vector3(0.06f, 0.045f, headLength * 0.65f), new Vector3(0f, -0.01f, -headLength * 0.35f));

            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Mesh CreateSwordBladeMesh(float length = 0.88f, float width = 0.062f)
        {
            Mesh m = new Mesh { name = "ProceduralSwordBlade_Mesh" };
            List<Vector3> verts = new();
            List<Vector2> uvs = new();
            List<int> tris = new();

            // Gavilán / Guardia cruciforme
            AddBoxCollar(verts, uvs, tris, new Vector3(0.035f, width * 3.4f, 0.035f), Vector3.zero);

            // Hoja de doble filo a lo largo de +X
            const int segs = 7;
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
                float x0 = Mathf.Lerp(0.02f, length, t0), x1 = Mathf.Lerp(0.02f, length, t1);
                float w0 = Mathf.Lerp(width, i == segs - 1 ? 0.005f : width * 0.65f, t0);
                float w1 = Mathf.Lerp(width, i == segs - 1 ? 0.005f : width * 0.65f, t1);
                float th0 = Mathf.Lerp(0.018f, 0.004f, t0), th1 = Mathf.Lerp(0.018f, 0.004f, t1);

                Vector3 c0 = new Vector3(x0, 0f, 0f), c1 = new Vector3(x1, 0f, 0f);
                int baseIdx = verts.Count;
                verts.Add(c0 + new Vector3(0f, w0, 0f));
                verts.Add(c0 + new Vector3(0f, 0f, th0));
                verts.Add(c0 + new Vector3(0f, -w0, 0f));
                verts.Add(c0 + new Vector3(0f, 0f, -th0));

                verts.Add(c1 + new Vector3(0f, w1, 0f));
                verts.Add(c1 + new Vector3(0f, 0f, th1));
                verts.Add(c1 + new Vector3(0f, -w1, 0f));
                verts.Add(c1 + new Vector3(0f, 0f, -th1));

                for (int v = 0; v < 8; v++) uvs.Add(new Vector2(t0, v / 8f));

                for (int s = 0; s < 4; s++)
                {
                    int n = (s + 1) % 4;
                    tris.Add(baseIdx + s); tris.Add(baseIdx + 4 + s); tris.Add(baseIdx + 4 + n);
                    tris.Add(baseIdx + s); tris.Add(baseIdx + 4 + n); tris.Add(baseIdx + n);
                }
            }

            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Mesh CreateHandleMesh(HandleLength length)
        {
            float totalLength = length switch
            {
                HandleLength.Short => 0.38f,
                HandleLength.Long => 1.08f,
                _ => 0.68f
            };

            Mesh mesh = new Mesh { name = $"CarvedWoodHandle_{length}_Mesh" };
            List<Vector3> verts = new();
            List<Vector2> uvs = new();
            List<int> tris = new();

            int rings = 6, oct = 8;
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float x = t * totalLength;
                float rad = Mathf.Lerp(0.026f, 0.022f, Mathf.Sin(t * Mathf.PI)) * (r == 0 || r == rings ? 1.25f : 1.0f);

                for (int o = 0; o < oct; o++)
                {
                    float a = o * Mathf.PI * 2f / oct;
                    verts.Add(new Vector3(x, Mathf.Sin(a) * rad, Mathf.Cos(a) * rad));
                    uvs.Add(new Vector2(o / (float)oct, t));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                int r0 = r * oct, r1 = (r + 1) * oct;
                for (int o = 0; o < oct; o++)
                {
                    int next = (o + 1) % oct;
                    tris.Add(r0 + o); tris.Add(r1 + o); tris.Add(r1 + next);
                    tris.Add(r0 + o); tris.Add(r1 + next); tris.Add(r0 + next);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBoxCollar(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 size, Vector3 offset)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            Vector3[] p =
            {
                offset + new Vector3(-hx, -hy, -hz), offset + new Vector3(hx, -hy, -hz),
                offset + new Vector3(hx, hy, -hz), offset + new Vector3(-hx, hy, -hz),
                offset + new Vector3(-hx, -hy, hz), offset + new Vector3(hx, -hy, hz),
                offset + new Vector3(hx, hy, hz), offset + new Vector3(-hx, hy, hz)
            };

            int s = verts.Count;
            verts.AddRange(p);
            for (int i = 0; i < 8; i++) uvs.Add(new Vector2(p[i].x, p[i].z));

            int[] quads = { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 2,3,7, 2,7,6, 1,2,6, 1,6,5, 3,0,4, 3,4,7 };
            for (int i = 0; i < quads.Length; i++) tris.Add(s + quads[i]);
        }

        private static void AddWedgeSection(List<Vector3> verts, List<Vector2> uvs, List<int> tris, float z0, float z1, float h0, float h1, float w0, float w1)
        {
            int s = verts.Count;
            verts.Add(new Vector3(-w0, -h0 * 0.5f, z0)); verts.Add(new Vector3(w0, -h0 * 0.5f, z0));
            verts.Add(new Vector3(w0, h0 * 0.5f, z0));   verts.Add(new Vector3(-w0, h0 * 0.5f, z0));
            verts.Add(new Vector3(-w1, -h1 * 0.5f, z1)); verts.Add(new Vector3(w1, -h1 * 0.5f, z1));
            verts.Add(new Vector3(w1, h1 * 0.5f, z1));   verts.Add(new Vector3(-w1, h1 * 0.5f, z1));

            for (int i = 0; i < 8; i++) uvs.Add(new Vector2(verts[s + i].y, verts[s + i].z));
            int[] quads = { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 2,3,7, 2,7,6, 1,2,6, 1,6,5, 3,0,4, 3,4,7 };
            for (int i = 0; i < quads.Length; i++) tris.Add(s + quads[i]);
        }
    }
}
