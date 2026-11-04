using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Generador procedural del modelo 3D del Gran Pico de Minería (2H).
    /// Garantiza caras sólidas estancas con normales exteriores en todos sus polígonos.
    /// </summary>
    public static class PickaxeModelGenerator
    {
        public static WeaponStats CreateDefaultIronPickaxe()
        {
            var ironComp = AlloyComposition.FromOres(new Dictionary<MetalType, int>
            {
                { MetalType.Iron, 9 }
            });

            Mesh handleMesh = GenerateHandleMesh();
            Mesh headMesh = GenerateHeadMesh();

            return new WeaponStats
            {
                weaponName = "Gran Pico de Hierro (2H)",
                weaponType = WeaponType.Pickaxe,
                ingotSize = ItemSize.Large,
                handleLength = HandleLength.Long,
                composition = ironComp,
                baseDamage = 48f,
                attackSpeed = 1.05f,
                attackRange = 1.35f,
                maxSharpness = 100f,
                currentSharpness = 100f,
                quality = 0.98f,
                qualityPrefix = "Minero Pesado",
                customMesh = headMesh,
                customHandleMesh = handleMesh,
                bladeMountPosition = new Vector3(0.94f, 0f, 0f),
                bladeMountRotation = Quaternion.identity,
                gripCenterOffset = new Vector3(0.35f, 0f, 0f),
                isFrontFaceDominant = true
            };
        }

        public static Mesh GenerateHandleMesh()
        {
            Mesh mesh = new Mesh { name = "ForgedPickaxeHandle2H_Mesh" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float[] xRings = { 0.00f, 0.04f, 0.10f, 0.25f, 0.50f, 0.75f, 0.88f, 0.94f, 1.00f, 1.05f };
            float[] rRings = { 0.030f, 0.034f, 0.024f, 0.023f, 0.024f, 0.026f, 0.030f, 0.032f, 0.030f, 0.026f };
            const int segs = 8;

            for (int r = 0; r < xRings.Length; r++)
            {
                float x = xRings[r];
                float radius = rRings[r];
                for (int s = 0; s < segs; s++)
                {
                    float angle = s * Mathf.PI * 2f / segs;
                    verts.Add(new Vector3(x, Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius));
                    uvs.Add(new Vector2((float)s / segs, x / 1.05f));
                }
            }

            for (int r = 0; r < xRings.Length - 1; r++)
            {
                int r0 = r * segs;
                int r1 = (r + 1) * segs;
                for (int s = 0; s < segs; s++)
                {
                    int next = (s + 1) % segs;
                    tris.Add(r0 + s); tris.Add(r1 + s); tris.Add(r1 + next);
                    tris.Add(r0 + s); tris.Add(r1 + next); tris.Add(r0 + next);
                }
            }

            int capStartIdx = verts.Count;
            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0f));
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(capStartIdx); tris.Add(s); tris.Add(next);
            }

            int capEndIdx = verts.Count;
            verts.Add(new Vector3(1.05f, 0f, 0f));
            uvs.Add(new Vector2(0.5f, 1f));
            int lastRing = (xRings.Length - 1) * segs;
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(capEndIdx); tris.Add(lastRing + next); tris.Add(lastRing + s);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh GenerateHeadMesh()
        {
            Mesh mesh = new Mesh { name = "ForgedIronPickaxeHead2H_Mesh" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            AddCollarBox(verts, uvs, tris, new Vector3(0.14f, 0.085f, 0.09f));
            AddCurvedPickArm(verts, uvs, tris, isFront: true, length: 0.46f, steps: 7, dropX: 0.15f, baseW: 0.042f, baseH: 0.038f);
            AddCurvedPickArm(verts, uvs, tris, isFront: false, length: 0.34f, steps: 6, dropX: 0.11f, baseW: 0.040f, baseH: 0.035f);

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCollarBox(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 size)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            Vector3 center = Vector3.zero;

            AddQuadOutward(verts, uvs, tris, new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz), center);
            AddQuadOutward(verts, uvs, tris, new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, -hy, -hz), center);
            AddQuadOutward(verts, uvs, tris, new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz), center);
            AddQuadOutward(verts, uvs, tris, new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), center);
            AddQuadOutward(verts, uvs, tris, new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz), new Vector3(-hx, -hy, hz), center);
            AddQuadOutward(verts, uvs, tris, new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, -hy, -hz), center);
        }

        private static void AddCurvedPickArm(List<Vector3> verts, List<Vector2> uvs, List<int> tris, bool isFront, float length, int steps, float dropX, float baseW, float baseH)
        {
            float zDir = isFront ? 1f : -1f;
            float zStart = zDir * 0.045f;
            float zTotal = zDir * length;

            for (int i = 0; i < steps; i++)
            {
                float t0 = (float)i / steps;
                float t1 = (float)(i + 1) / steps;

                float z0 = Mathf.Lerp(zStart, zTotal, t0);
                float z1 = Mathf.Lerp(zStart, zTotal, t1);

                float x0 = -dropX * Mathf.Pow(t0, 1.55f);
                float x1 = -dropX * Mathf.Pow(t1, 1.55f);

                float w0 = Mathf.Lerp(baseW, 0.005f, t0);
                float h0 = Mathf.Lerp(baseH, 0.005f, t0);
                float w1 = Mathf.Lerp(baseW, 0.005f, t1);
                float h1 = Mathf.Lerp(baseH, 0.005f, t1);

                Vector3 top0 = new Vector3(x0 + w0, 0f, z0);
                Vector3 bot0 = new Vector3(x0 - w0, 0f, z0);
                Vector3 right0 = new Vector3(x0, h0, z0);
                Vector3 left0 = new Vector3(x0, -h0, z0);

                Vector3 top1 = new Vector3(x1 + w1, 0f, z1);
                Vector3 bot1 = new Vector3(x1 - w1, 0f, z1);
                Vector3 right1 = new Vector3(x1, h1, z1);
                Vector3 left1 = new Vector3(x1, -h1, z1);

                Vector3 segCenter = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);

                AddQuadOutward(verts, uvs, tris, top0, right0, right1, top1, segCenter);
                AddQuadOutward(verts, uvs, tris, right0, bot0, bot1, right1, segCenter);
                AddQuadOutward(verts, uvs, tris, bot0, left0, left1, bot1, segCenter);
                AddQuadOutward(verts, uvs, tris, left0, top0, top1, left1, segCenter);

                if (i == steps - 1)
                {
                    Vector3 apex = new Vector3(x1 - (dropX * 0.12f), 0f, z1 + (zDir * 0.025f));
                    Vector3 apexCenter = new Vector3(x1, 0f, z1);
                    AddFaceOutward(verts, uvs, tris, top1, right1, apex, apexCenter);
                    AddFaceOutward(verts, uvs, tris, right1, bot1, apex, apexCenter);
                    AddFaceOutward(verts, uvs, tris, bot1, left1, apex, apexCenter);
                    AddFaceOutward(verts, uvs, tris, left1, top1, apex, apexCenter);
                }
            }
        }

        private static void AddFaceOutward(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 center)
        {
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
            Vector3 outward = ((v0 + v1 + v2) / 3f) - center;
            int s = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2);
            uvs.Add(new Vector2(v0.y + 0.5f, v0.z));
            uvs.Add(new Vector2(v1.y + 0.5f, v1.z));
            uvs.Add(new Vector2(v2.y + 0.5f, v2.z));

            if (Vector3.Dot(normal, outward) >= 0f)
            {
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            }
            else
            {
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
            }
        }

        private static void AddQuadOutward(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center)
        {
            AddFaceOutward(verts, uvs, tris, a, b, c, center);
            AddFaceOutward(verts, uvs, tris, a, c, d, center);
        }
    }
}
