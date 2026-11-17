using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Marco 3D volumétrico esculpido de la boca de la caverna (CaveEntranceCollar3D).
    /// Rodea todo el borde de la entrada con geometría 3D cerrada, normales matemáticamente orientadas
    /// hacia el observador y dimensiones que encajan perfectamente bajo el arco de la montaña sin traspasarlo.
    /// </summary>
    public static partial class CaveDungeonBuilder
    {
        public static void Build3DCaveEntranceCollar(Transform portalRoot)
        {
            var collarGo = new GameObject("CaveEntranceCollar3D", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            collarGo.transform.SetParent(portalRoot, false);
            collarGo.transform.localPosition = Vector3.zero;
            collarGo.transform.localRotation = Quaternion.identity;

            Mesh archMesh = GenerateSculptedEntranceArchMesh();
            collarGo.GetComponent<MeshFilter>().sharedMesh = archMesh;

            var mr = collarGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = GetCaveRockMaterial();
            mr.shadowCastingMode = ShadowCastingMode.TwoSided;
            mr.receiveShadows = true;

            var mc = collarGo.GetComponent<MeshCollider>();
            mc.sharedMesh = archMesh;
        }

        private static Mesh GenerateSculptedEntranceArchMesh()
        {
            const int segs = 28;
            const int rings = 3;
            // Profundidad en Z: desde el exterior en el pasto (-1.2m) hasta el interior de la montaña (+1.4m)
            float[] sliceZ = { -1.25f, -0.60f, 0.0f, 0.70f, 1.45f };
            int slices = sliceZ.Length;

            Vector3[,,] grid = new Vector3[slices, rings, segs + 1];

            for (int s = 0; s < slices; s++)
            {
                float z = sliceZ[s];
                float zTaper = 1.0f - Mathf.Abs(z) * 0.05f;

                for (int r = 0; r < rings; r++)
                {
                    // Dimensiones calibradas: el radio exterior máximo (7.0m) encaja bajo la montaña (6.85m - 7.5m)
                    float rxBase = (r == 0) ? 4.85f : ((r == 1) ? 5.85f : 6.80f);
                    float ryBase = (r == 0) ? 5.65f : ((r == 1) ? 6.45f : 7.15f);
                    float rx = rxBase * zTaper;
                    float ry = ryBase * zTaper;

                    for (int a = 0; a <= segs; a++)
                    {
                        float t = a / (float)segs;
                        float ang = Mathf.PI * t;
                        float cosA = Mathf.Cos(ang);
                        float sinA = Mathf.Sin(ang);

                        float crag = (r == 0)
                            ? 0f
                            : (Mathf.PerlinNoise(a * 0.35f + 4.1f, s * 0.30f + r * 2.1f) - 0.5f) * (r == 1 ? 0.22f : 0.38f);

                        float x = cosA * (rx + crag);
                        float y;

                        if (a == 0 || a == segs)
                        {
                            y = (r == 0) ? -2.40f : ((r == 1) ? -2.90f : -3.40f);
                        }
                        else
                        {
                            float footBlend = Mathf.Clamp01(Mathf.Sin(ang) / 0.16f);
                            float archY = 0.05f + sinA * (ry + crag);
                            float floorY = (r == 0) ? -2.40f : ((r == 1) ? -2.90f : -3.40f);
                            y = Mathf.Lerp(floorY, archY, footBlend);
                        }

                        grid[s, r, a] = new Vector3(x, y, z);
                    }
                }
            }

            List<Vector3> verts = new();
            List<Vector2> uvs = new();
            List<int> tris = new();

            // 1. Cara frontal (Slice 0) mirando hacia el pasto (-Z)
            for (int a = 0; a < segs; a++)
            {
                AddOrientedQuad(verts, uvs, tris, grid[0, 0, a], grid[0, 1, a], grid[0, 1, a + 1], grid[0, 0, a + 1], -Vector3.forward);
                AddOrientedQuad(verts, uvs, tris, grid[0, 1, a], grid[0, 2, a], grid[0, 2, a + 1], grid[0, 1, a + 1], -Vector3.forward);
            }

            // 2. Cara posterior (Slice slices - 1) mirando hacia el túnel (+Z)
            int lastS = slices - 1;
            for (int a = 0; a < segs; a++)
            {
                AddOrientedQuad(verts, uvs, tris, grid[lastS, 0, a], grid[lastS, 1, a], grid[lastS, 1, a + 1], grid[lastS, 0, a + 1], Vector3.forward);
                AddOrientedQuad(verts, uvs, tris, grid[lastS, 1, a], grid[lastS, 2, a], grid[lastS, 2, a + 1], grid[lastS, 1, a + 1], Vector3.forward);
            }

            // 3. Superficie exterior del arco (Ring 2 a través de todos los Z, normal radial hacia afuera)
            for (int s = 0; s < slices - 1; s++)
            {
                for (int a = 0; a < segs; a++)
                {
                    Vector3 midP = (grid[s, 2, a] + grid[s + 1, 2, a + 1]) * 0.5f;
                    Vector3 radialOut = new Vector3(midP.x, Mathf.Max(0.1f, midP.y), 0f).normalized;
                    AddOrientedQuad(verts, uvs, tris, grid[s, 2, a], grid[s + 1, 2, a], grid[s + 1, 2, a + 1], grid[s, 2, a + 1], radialOut);
                }
            }

            // 4. Bóveda interior del arco (Ring 0 a través de todos los Z, normal hacia adentro del túnel)
            for (int s = 0; s < slices - 1; s++)
            {
                for (int a = 0; a < segs; a++)
                {
                    Vector3 midP = (grid[s, 0, a] + grid[s + 1, 0, a + 1]) * 0.5f;
                    Vector3 radialIn = -new Vector3(midP.x, Mathf.Max(0.1f, midP.y), 0f).normalized;
                    AddOrientedQuad(verts, uvs, tris, grid[s, 0, a], grid[s + 1, 0, a], grid[s + 1, 0, a + 1], grid[s, 0, a + 1], radialIn);
                }
            }

            // 5. Cierre inferior subterráneo en la base izquierda y derecha (normal hacia abajo -Y)
            for (int s = 0; s < slices - 1; s++)
            {
                for (int r = 0; r < rings - 1; r++)
                {
                    // Lado derecho (a = 0)
                    AddOrientedQuad(verts, uvs, tris, grid[s, r, 0], grid[s, r + 1, 0], grid[s + 1, r + 1, 0], grid[s + 1, r, 0], -Vector3.up);
                    // Lado izquierdo (a = segs)
                    AddOrientedQuad(verts, uvs, tris, grid[s, r, segs], grid[s, r + 1, segs], grid[s + 1, r + 1, segs], grid[s + 1, r, segs], -Vector3.up);
                }
            }

            Mesh mesh = new Mesh { name = "ProceduralCaveEntranceCollar3D_V2" };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddOrientedQuad(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outwardHint)
        {
            Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
            if (Vector3.Dot(faceNormal, outwardHint) < 0f)
            {
                AddQuadRaw(v, uv, t, p0, p3, p2, p1);
            }
            else
            {
                AddQuadRaw(v, uv, t, p0, p1, p2, p3);
            }
        }

        private static void AddQuadRaw(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int idx = v.Count;
            v.Add(p0); uv.Add(new Vector2(p0.x * 0.16f, (p0.y + p0.z) * 0.16f));
            v.Add(p1); uv.Add(new Vector2(p1.x * 0.16f, (p1.y + p1.z) * 0.16f));
            v.Add(p2); uv.Add(new Vector2(p2.x * 0.16f, (p2.y + p2.z) * 0.16f));
            v.Add(p3); uv.Add(new Vector2(p3.x * 0.16f, (p3.y + p3.z) * 0.16f));

            t.Add(idx); t.Add(idx + 1); t.Add(idx + 2);
            t.Add(idx); t.Add(idx + 2); t.Add(idx + 3);
        }
    }
}
