using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo geométrico auxiliar para AnvilMeshCutter:
    /// Detección de bordes perimetrales, intersección de rayos y segmentos 2D,
    /// división de aristas en triángulos y extracción del lazo perimetral estanco.
    /// </summary>
    public static partial class AnvilMeshCutter
    {
        public struct BoundaryEdge { public int u, v; public Vector2 a, b; }
        public struct EdgeHit { public int u, v; public float tSeg, tEdge; }

        public static List<BoundaryEdge> GetBoundaryEdges(List<Vector3> verts, List<int> tris)
        {
            Dictionary<long, int> directed = new Dictionary<long, int>(tris.Count);
            for (int i = 0; i < tris.Count; i += 3)
            {
                directed[((long)tris[i] << 32) | (uint)tris[i + 1]] = 1;
                directed[((long)tris[i + 1] << 32) | (uint)tris[i + 2]] = 1;
                directed[((long)tris[i + 2] << 32) | (uint)tris[i]] = 1;
            }

            List<BoundaryEdge> list = new List<BoundaryEdge>();
            foreach (var kv in directed)
            {
                int u = (int)(kv.Key >> 32), v = (int)(kv.Key & 0xFFFFFFFF);
                if (!directed.ContainsKey(((long)v << 32) | (uint)u))
                {
                    list.Add(new BoundaryEdge
                    {
                        u = u, v = v,
                        a = new Vector2(verts[u].x, verts[u].z),
                        b = new Vector2(verts[v].x, verts[v].z)
                    });
                }
            }
            return list;
        }

        public static bool FindFirstBoundaryHit(
            Vector2 origin, Vector2 dir, List<BoundaryEdge> bEdges,
            out BoundaryEdge hitEdge, out Vector2 hitPt, out float hitTEdge)
        {
            hitEdge = default; hitPt = Vector2.zero; hitTEdge = 0f;
            float minT = float.MaxValue;
            for (int i = 0; i < bEdges.Count; i++)
            {
                BoundaryEdge be = bEdges[i];
                Vector2 s = be.b - be.a;
                float cross = dir.x * s.y - dir.y * s.x;
                if (Mathf.Abs(cross) < 1e-6f) continue;
                Vector2 delta = be.a - origin;
                float tRay = (delta.x * s.y - delta.y * s.x) / cross;
                float tEdge = (delta.x * dir.y - delta.y * dir.x) / cross;
                if (tRay > 1e-4f && tRay < minT && tEdge >= -1e-4f && tEdge <= 1f + 1e-4f)
                {
                    minT = tRay; hitEdge = be; hitTEdge = Mathf.Clamp01(tEdge); hitPt = origin + tRay * dir;
                }
            }
            return minT < float.MaxValue;
        }

        public static bool IsPointInsideMesh(Vector2 pt, List<Vector3> verts, List<int> tris)
        {
            for (int i = 0; i < tris.Count; i += 3)
                if (PointInTriangle2D(pt, verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]], out _, out _, out _))
                    return true;
            return false;
        }

        public static bool PointInTriangle2D(
            Vector2 p, Vector3 a, Vector3 b, Vector3 c, out float w0, out float w1, out float w2)
        {
            w0 = w1 = w2 = 0f;
            float denom = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(denom) < 1e-7f) return false;
            w0 = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / denom;
            w1 = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / denom;
            w2 = 1f - w0 - w1;
            return w0 >= -1e-4f && w1 >= -1e-4f && w2 >= -1e-4f;
        }

        public static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 a, Vector2 b)
        {
            Vector2 r = p2 - p1, s = b - a; float cross = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(cross) < 1e-6f) return false;
            Vector2 d = a - p1;
            float t = (d.x * s.y - d.y * s.x) / cross, u = (d.x * r.y - d.y * r.x) / cross;
            return t > 1e-4f && t < 1f - 1e-4f && u > 1e-4f && u < 1f - 1e-4f;
        }

        public static void TestEdgeIntersection(
            Vector2 a, Vector2 b, int u, int v, List<Vector3> verts, HashSet<long> tested, List<EdgeHit> hits)
        {
            long key = GetEdgeKey(u, v);
            if (!tested.Add(key)) return;
            Vector2 c = new Vector2(verts[u].x, verts[u].z), d = new Vector2(verts[v].x, verts[v].z);
            Vector2 r = b - a, s = d - c;
            float cross = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(cross) < 1e-7f) return;
            Vector2 ca = c - a;
            float t = (ca.x * s.y - ca.y * s.x) / cross, uP = (ca.x * r.y - ca.y * r.x) / cross;
            if (t > 1e-3f && t < 1f - 1e-3f && uP > 1e-4f && uP < 1f - 1e-4f)
                hits.Add(new EdgeHit { u = u, v = v, tSeg = Mathf.Clamp01(t), tEdge = Mathf.Clamp01(uP) });
        }

        public static void SplitEdgeInTris(int u, int v, int newIdx, List<int> tris)
        {
            int count = tris.Count;
            for (int i = 0; i < count; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                if ((i0 == u && i1 == v) || (i0 == v && i1 == u)) { tris[i] = i0; tris[i + 1] = newIdx; tris[i + 2] = i2; tris.Add(newIdx); tris.Add(i1); tris.Add(i2); }
                else if ((i1 == u && i2 == v) || (i1 == v && i2 == u)) { tris[i] = i0; tris[i + 1] = i1; tris[i + 2] = newIdx; tris.Add(i0); tris.Add(newIdx); tris.Add(i2); }
                else if ((i2 == u && i0 == v) || (i2 == v && i0 == u)) { tris[i] = i0; tris[i + 1] = i1; tris[i + 2] = newIdx; tris.Add(i1); tris.Add(i2); tris.Add(newIdx); }
            }
        }

        public static long GetEdgeKey(int a, int b) => a < b ? (((long)a << 32) | (uint)b) : (((long)b << 32) | (uint)a);

        public static List<int> ExtractPerimeterLoop(List<Vector3> verts, List<int> tris, out int[] loopLengths)
        {
            Dictionary<long, int> directed = new Dictionary<long, int>(tris.Count);
            for (int i = 0; i < tris.Count; i += 3)
            {
                directed[((long)tris[i] << 32) | (uint)tris[i + 1]] = 1;
                directed[((long)tris[i + 1] << 32) | (uint)tris[i + 2]] = 1;
                directed[((long)tris[i + 2] << 32) | (uint)tris[i]] = 1;
            }

            Dictionary<int, int> nextVertex = new Dictionary<int, int>();
            foreach (var kv in directed)
            {
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xFFFFFFFF);
                if (!directed.ContainsKey(((long)b << 32) | (uint)a)) nextVertex[a] = b;
            }

            List<List<int>> loops = new List<List<int>>();
            HashSet<int> visited = new HashSet<int>();

            foreach (var startKey in nextVertex.Keys)
            {
                if (visited.Contains(startKey)) continue;
                List<int> loop = new List<int>();
                int curr = startKey;
                while (!visited.Contains(curr) && nextVertex.TryGetValue(curr, out int next))
                {
                    visited.Add(curr);
                    loop.Add(curr);
                    curr = next;
                    if (curr == startKey) break;
                }
                if (loop.Count >= 3) loops.Add(loop);
            }

            if (loops.Count == 0) { loopLengths = new int[0]; return new List<int>(); }

            // Sort loops: largest area first is outer boundary
            loops.Sort((l1, l2) => Mathf.Abs(GetSignedArea2D(verts, l2)).CompareTo(Mathf.Abs(GetSignedArea2D(verts, l1))));

            // Outer loop has positive signed area (counter-clockwise)
            if (GetSignedArea2D(verts, loops[0]) < 0f) loops[0].Reverse();

            // Inner loops (holes) have negative signed area (clockwise, walls face into hole)
            for (int l = 1; l < loops.Count; l++)
            {
                if (GetSignedArea2D(verts, loops[l]) > 0f) loops[l].Reverse();
            }

            List<int> flat = new List<int>();
            loopLengths = new int[loops.Count];
            for (int l = 0; l < loops.Count; l++)
            {
                loopLengths[l] = loops[l].Count;
                flat.AddRange(loops[l]);
            }
            return flat;
        }

        public static List<int> ExtractPerimeterLoop(List<Vector3> verts, List<int> tris, Vector2 keepPoint = default) =>
            ExtractPerimeterLoop(verts, tris, out _);

        public static float GetSignedArea2D(List<Vector3> verts, List<int> perim)
        {
            float area = 0f;
            for (int i = 0, j = perim.Count - 1; i < perim.Count; j = i++)
            {
                Vector3 pi = verts[perim[i]], pj = verts[perim[j]];
                area += (pj.x * pi.z) - (pi.x * pj.z);
            }
            return area * 0.5f;
        }

        public static float GetTriangleArea2D(Vector3 a, Vector3 b, Vector3 c) =>
            Mathf.Abs((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x)) * 0.5f;

        public static int FindClosestVertexIn2D(Vector2 pt, Vector3[] columns, int[] indices)
        {
            float minDstSqr = float.MaxValue;
            int bestIdx = indices != null && indices.Length > 0 ? indices[0] : 0;
            int count = indices != null ? indices.Length : columns.Length;

            for (int i = 0; i < count; i++)
            {
                int k = indices != null ? indices[i] : i;
                float dx = columns[k].x - pt.x, dz = columns[k].z - pt.y;
                float d2 = dx * dx + dz * dz;
                if (d2 < minDstSqr) { minDstSqr = d2; bestIdx = k; }
            }
            return bestIdx;
        }

        public static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a; float len2 = ab.sqrMagnitude;
            if (len2 < 1e-7f) { t = 0f; return a; }
            t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return a + t * ab;
        }
    }
}
