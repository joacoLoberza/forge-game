using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public static partial class WeaponVisualBuilder
    {
        public static Mesh GenerateArchetypeHead(WeaponType type) => type switch
        {
            WeaponType.Axe => GenerateAxeHeadMesh(),
            WeaponType.Sword => GenerateSwordBladeMesh(),
            WeaponType.Hammer => GenerateHammerHeadMesh(),
            _ => PickaxeModelGenerator.GenerateHeadMesh()
        };

        public static Vector3 GetArchetypeGripOffset(HandleLength length, WeaponType type)
        {
            if (type == WeaponType.Pickaxe) return new Vector3(0.35f, 0f, 0f);
            return length switch
            {
                HandleLength.Short => new Vector3(0.12f, 0f, 0f),
                HandleLength.Medium => new Vector3(0.18f, 0f, 0f),
                _ => new Vector3(0.32f, 0f, 0f)
            };
        }

        public static Mesh GenerateAxeHeadMesh()
        {
            Mesh mesh = new Mesh { name = "ArchetypeAxeHead_Mesh" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // 1. Ojo central del hacha
            AddBox(verts, uvs, tris, Vector3.zero, new Vector3(0.09f, 0.08f, 0.08f));

            // 2. Cotana / Martillo trasero (en -Z)
            AddBox(verts, uvs, tris, new Vector3(0f, 0f, -0.065f), new Vector3(0.06f, 0.06f, 0.05f));

            // 3. Hoja del hacha: altura del filo vertical a lo largo del mango (X), grosor fino en Y y alcance hacia adelante (+Z)
            Vector3[] c0 = { new(0.036f, 0.026f, 0.04f), new(-0.036f, 0.026f, 0.04f), new(-0.036f, -0.026f, 0.04f), new(0.036f, -0.026f, 0.04f) };
            Vector3[] c1 = { new(0.088f, 0.014f, 0.12f), new(-0.088f, 0.014f, 0.12f), new(-0.088f, -0.014f, 0.12f), new(0.088f, -0.014f, 0.12f) };
            Vector3[] c2 = { new(0.145f, 0.003f, 0.22f), new(-0.145f, 0.003f, 0.22f), new(-0.145f, -0.003f, 0.22f), new(0.145f, -0.003f, 0.22f) };

            ConnectRings(verts, uvs, tris, c0, c1);
            ConnectRings(verts, uvs, tris, c1, c2);
            AddQuad(verts, uvs, tris, c2[0], c2[3], c2[2], c2[1]);

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh GenerateSwordBladeMesh(float bladeLength = 0.82f)
        {
            Mesh mesh = new Mesh { name = "ArchetypeSwordBlade_Mesh" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // 1. Gavilanes / Cruz (en Z)
            AddBox(verts, uvs, tris, Vector3.zero, new Vector3(0.035f, 0.03f, 0.22f));

            // 2. Hoja recta con sección de diamante a lo largo de +X
            float xMid = bladeLength * 0.88f;
            Vector3[] r0 = { new(0.018f, 0.008f, 0f), new(0.018f, 0f, 0.036f), new(0.018f, -0.008f, 0f), new(0.018f, 0f, -0.036f) };
            Vector3[] r1 = { new(xMid, 0.006f, 0f), new(xMid, 0f, 0.028f), new(xMid, -0.006f, 0f), new(xMid, 0f, -0.028f) };
            ConnectRings(verts, uvs, tris, r0, r1);

            // 3. Punta afilada
            Vector3 tip = new Vector3(bladeLength, 0f, 0f);
            AddTriangle(verts, uvs, tris, r1[0], r1[1], tip);
            AddTriangle(verts, uvs, tris, r1[1], r1[2], tip);
            AddTriangle(verts, uvs, tris, r1[2], r1[3], tip);
            AddTriangle(verts, uvs, tris, r1[3], r1[0], tip);

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh GenerateHammerHeadMesh()
        {
            Mesh mesh = new Mesh { name = "ArchetypeHammerHead_Mesh" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // 1. Collar central (Z: -0.04 .. +0.04)
            AddBox(verts, uvs, tris, Vector3.zero, new Vector3(0.09f, 0.08f, 0.08f));

            // 2. Maza de golpeo frontal (+Z) con maza ligeramente encastrada y placa frontal limpia sin Z-fighting
            AddBox(verts, uvs, tris, new Vector3(0f, 0f, 0.095f), new Vector3(0.082f, 0.074f, 0.118f));
            AddBox(verts, uvs, tris, new Vector3(0f, 0f, 0.162f), new Vector3(0.092f, 0.092f, 0.018f));

            // 3. Espolón perforador trasero (-Z) ligeramente encastrado (-0.036) para evitar coplanaridad/clipping en Z=-0.04
            Vector3[] b0 = { new(0.028f, 0.028f, -0.036f), new(-0.028f, 0.028f, -0.036f), new(-0.028f, -0.028f, -0.036f), new(0.028f, -0.028f, -0.036f) };
            Vector3 peak = new Vector3(0f, -0.010f, -0.155f);
            AddTriangle(verts, uvs, tris, b0[0], peak, b0[1]);
            AddTriangle(verts, uvs, tris, b0[1], peak, b0[2]);
            AddTriangle(verts, uvs, tris, b0[2], peak, b0[3]);
            AddTriangle(verts, uvs, tris, b0[3], peak, b0[0]);
            AddQuad(verts, uvs, tris, b0[0], b0[1], b0[2], b0[3]);

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh GenerateArchetypeHandle(HandleLength length, WeaponType type)
        {
            if (type == WeaponType.Pickaxe) return PickaxeModelGenerator.GenerateHandleMesh();

            float totalLen = length switch
            {
                HandleLength.Short => 0.28f,
                HandleLength.Medium => 0.65f,
                _ => 1.05f
            };
            float radius = (length == HandleLength.Short) ? 0.018f : (length == HandleLength.Medium ? 0.022f : 0.025f);

            Mesh mesh = new Mesh { name = $"ArchetypeHandle_{length}_Mesh" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            const int segs = 8;
            int rings = length == HandleLength.Short ? 4 : (length == HandleLength.Medium ? 6 : 8);

            for (int r = 0; r < rings; r++)
            {
                float t = (float)r / (rings - 1);
                float x = t * totalLen;
                float rCurrent = (r == 0 && length == HandleLength.Short) ? radius * 1.55f : radius;

                for (int s = 0; s < segs; s++)
                {
                    float angle = s * Mathf.PI * 2f / segs;
                    verts.Add(new Vector3(x, Mathf.Sin(angle) * rCurrent, Mathf.Cos(angle) * rCurrent));
                    uvs.Add(new Vector2((float)s / segs, t));
                }
            }

            for (int r = 0; r < rings - 1; r++)
            {
                int r0 = r * segs, r1 = (r + 1) * segs;
                for (int s = 0; s < segs; s++)
                {
                    int next = (s + 1) % segs;
                    tris.Add(r0 + s); tris.Add(r1 + s); tris.Add(r1 + next);
                    tris.Add(r0 + s); tris.Add(r1 + next); tris.Add(r0 + next);
                }
            }

            int cap0 = verts.Count;
            verts.Add(Vector3.zero); uvs.Add(new Vector2(0.5f, 0f));
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(cap0); tris.Add(s); tris.Add(next);
            }

            int cap1 = verts.Count;
            verts.Add(new Vector3(totalLen, 0f, 0f)); uvs.Add(new Vector2(0.5f, 1f));
            int lastRing = (rings - 1) * segs;
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(cap1); tris.Add(lastRing + next); tris.Add(lastRing + s);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 center, Vector3 size)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            Vector3 c = center;
            AddQuad(verts, uvs, tris, c + new Vector3(hx, hy, hz), c + new Vector3(-hx, hy, hz), c + new Vector3(-hx, -hy, hz), c + new Vector3(hx, -hy, hz));
            AddQuad(verts, uvs, tris, c + new Vector3(-hx, hy, -hz), c + new Vector3(hx, hy, -hz), c + new Vector3(hx, -hy, -hz), c + new Vector3(-hx, -hy, -hz));
            AddQuad(verts, uvs, tris, c + new Vector3(hx, hy, -hz), c + new Vector3(-hx, hy, -hz), c + new Vector3(-hx, hy, hz), c + new Vector3(hx, hy, hz));
            AddQuad(verts, uvs, tris, c + new Vector3(-hx, -hy, -hz), c + new Vector3(hx, -hy, -hz), c + new Vector3(hx, -hy, hz), c + new Vector3(-hx, -hy, hz));
            AddQuad(verts, uvs, tris, c + new Vector3(hx, hy, -hz), c + new Vector3(hx, hy, hz), c + new Vector3(hx, -hy, hz), c + new Vector3(hx, -hy, -hz));
            AddQuad(verts, uvs, tris, c + new Vector3(-hx, hy, hz), c + new Vector3(-hx, hy, -hz), c + new Vector3(-hx, -hy, -hz), c + new Vector3(-hx, -hy, hz));
        }

        private static void ConnectRings(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3[] r0, Vector3[] r1)
        {
            int n = r0.Length;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                AddQuad(verts, uvs, tris, r0[i], r0[next], r1[next], r1[i]);
            }
        }

        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }

        private static void AddTriangle(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0.5f, 1f));
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
        }
    }
}

