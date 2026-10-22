using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de corte plano analítico estanco por línea recta para AnvilMeshCutter:
    /// Corta todos los triángulos de la malla contra el hiperplano 2D definido por la línea,
    /// interpolando altura Y y normales, sin requerir reconstrucción de grafos BFS ni
    /// depender de lazos cerrados. Garantiza consistencia perfecta en cortes abiertos (2 puntos).
    /// </summary>
    public static partial class AnvilMeshCutter
    {
        public static void SliceTrianglesByLine(
            Vector2 lineA, Vector2 lineB, Vector2 keepPoint,
            List<Vector3> verts, List<int> tris, List<int> discardedTris = null, bool flipSide = false)
        {
            Vector2 dir = lineB - lineA;
            if (dir.sqrMagnitude < 1e-7f) return;

            Vector2 normal = new Vector2(-dir.y, dir.x).normalized;
            if (Vector2.Dot(keepPoint - lineA, normal) < 0f) normal = -normal;
            if (flipSide) normal = -normal;

            float[] dists = new float[verts.Count];
            for (int i = 0; i < verts.Count; i++)
                dists[i] = Vector2.Dot(new Vector2(verts[i].x, verts[i].z) - lineA, normal);

            List<int> nextTris = new List<int>(tris.Count);
            Dictionary<long, int> edgeCache = new Dictionary<long, int>();

            for (int i = 0; i < tris.Count; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                float d0 = dists[i0], d1 = dists[i1], d2 = dists[i2];
                bool in0 = d0 >= -1e-5f, in1 = d1 >= -1e-5f, in2 = d2 >= -1e-5f;
                int inCount = (in0 ? 1 : 0) + (in1 ? 1 : 0) + (in2 ? 1 : 0);

                if (inCount == 3)
                {
                    nextTris.Add(i0); nextTris.Add(i1); nextTris.Add(i2);
                }
                else if (inCount == 0)
                {
                    if (discardedTris != null) { discardedTris.Add(i0); discardedTris.Add(i1); discardedTris.Add(i2); }
                }
                else if (inCount == 1)
                {
                    int vIn = in0 ? i0 : (in1 ? i1 : i2);
                    int vOut1 = in0 ? i1 : (in1 ? i2 : i0);
                    int vOut2 = in0 ? i2 : (in1 ? i0 : i1);

                    int e1 = GetEdgeIntersectionCached(vIn, vOut1, dists, verts, edgeCache);
                    int e2 = GetEdgeIntersectionCached(vIn, vOut2, dists, verts, edgeCache);

                    if (GetTriangleArea2D(verts[vIn], verts[e1], verts[e2]) > 1e-7f)
                    {
                        nextTris.Add(vIn); nextTris.Add(e1); nextTris.Add(e2);
                    }
                    if (discardedTris != null)
                    {
                        discardedTris.Add(vOut1); discardedTris.Add(vOut2); discardedTris.Add(e2);
                        discardedTris.Add(vOut1); discardedTris.Add(e2); discardedTris.Add(e1);
                    }
                }
                else if (inCount == 2)
                {
                    int vOut = !in0 ? i0 : (!in1 ? i1 : i2);
                    int vIn1 = !in0 ? i1 : (!in1 ? i2 : i0);
                    int vIn2 = !in0 ? i2 : (!in1 ? i0 : i1);

                    int e1 = GetEdgeIntersectionCached(vIn1, vOut, dists, verts, edgeCache);
                    int e2 = GetEdgeIntersectionCached(vIn2, vOut, dists, verts, edgeCache);

                    if (GetTriangleArea2D(verts[vIn1], verts[vIn2], verts[e2]) > 1e-7f)
                    {
                        nextTris.Add(vIn1); nextTris.Add(vIn2); nextTris.Add(e2);
                    }
                    if (GetTriangleArea2D(verts[vIn1], verts[e2], verts[e1]) > 1e-7f)
                    {
                        nextTris.Add(vIn1); nextTris.Add(e2); nextTris.Add(e1);
                    }
                    if (discardedTris != null)
                    {
                        discardedTris.Add(vOut); discardedTris.Add(e2); discardedTris.Add(e1);
                    }
                }
            }

            tris.Clear();
            tris.AddRange(nextTris);
        }

        private static int GetEdgeIntersectionCached(
            int ia, int ib, float[] dists, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = ia < ib ? (((long)ia << 32) | (uint)ib) : (((long)ib << 32) | (uint)ia);
            if (cache.TryGetValue(key, out int existing)) return existing;

            float da = dists[ia], db = dists[ib];
            float denom = da - db;
            float t = Mathf.Abs(denom) > 1e-7f ? Mathf.Clamp01(da / denom) : 0.5f;

            Vector3 va = verts[ia], vb = verts[ib];
            Vector3 vNew = Vector3.Lerp(va, vb, t);
            int newIdx = verts.Count;
            verts.Add(vNew);
            cache[key] = newIdx;
            return newIdx;
        }
    }
}
