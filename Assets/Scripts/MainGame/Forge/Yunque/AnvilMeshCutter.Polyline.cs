using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Módulo de corte topológico por polilínea finita para AnvilMeshCutter:
    /// Subdivide exclusivamente a lo largo del trayecto trazado por el jugador sin
    /// atajos internos ni proyecciones infinitas arbitrarias, y aísla la porción deseada.
    /// </summary>
    public static partial class AnvilMeshCutter
    {
        public static void CutTrianglesByPolyline(
            List<Vector2> pts, Vector2 keepPoint, List<Vector3> verts, List<int> tris,
            List<int> discardedTris = null, bool? discardInside = null)
        {
            if (pts == null || pts.Count < 2) return;
            if (pts.Count == 2)
            {
                SliceTrianglesByLine(pts[0], pts[1], keepPoint, verts, tris, discardedTris, discardInside == true);
                return;
            }
            int K = pts.Count;
            List<BoundaryEdge> bEdges = GetBoundaryEdges(verts, tris);

            bool isClosed = DetectAndBuildClosedLoop(pts, bEdges, verts, tris, null, out List<Vector2> closedLoop);
            if (isClosed && closedLoop != null && closedLoop.Count >= 3)
            {
                int L = closedLoop.Count - 1; int[] loopVerts = new int[L];
                for (int i = 0; i < L; i++) loopVerts[i] = IsPointInsideMesh(closedLoop[i], verts, tris) ? InsertOrFindVertexInMesh(closedLoop[i], verts, tris) : -1;
                HashSet<long> cutEdges = new HashSet<long>();
                for (int s = 0; s < L; s++) SubdivideAlongSegment(closedLoop[s], closedLoop[s + 1], loopVerts[s], loopVerts[(s + 1) % L], verts, tris, cutEdges);

                bool discardIn = discardInside == true;
                List<int> nextTris = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 v0 = verts[tris[i]], v1 = verts[tris[i + 1]], v2 = verts[tris[i + 2]];
                    Vector2 c = new Vector2((v0.x + v1.x + v2.x) / 3f, (v0.z + v1.z + v2.z) / 3f);
                    bool keep = discardIn ? !IsPointInPolygon2D(c, closedLoop) : IsPointInPolygon2D(c, closedLoop);
                    if (keep) { nextTris.Add(tris[i]); nextTris.Add(tris[i + 1]); nextTris.Add(tris[i + 2]); }
                    else if (discardedTris != null) { discardedTris.Add(tris[i]); discardedTris.Add(tris[i + 1]); discardedTris.Add(tris[i + 2]); }
                }
                tris.Clear(); tris.AddRange(nextTris);
                return;
            }

            // Corte abierto: extender ambos extremos fuera del perímetro para garantizar partición de borde a borde
            Vector2 d0 = pts[0] - pts[1], r0 = d0.sqrMagnitude < 1e-6f ? Vector2.up : d0.normalized;
            Vector2 d1 = pts[K - 1] - pts[K - 2], r1 = d1.sqrMagnitude < 1e-6f ? Vector2.down : d1.normalized;

            Vector2 orig0 = IsPointInsideMesh(pts[0], verts, tris) ? pts[0] : pts[1];
            float tE0 = 0f, tE1 = 0f;
            if (!FindFirstBoundaryHit(orig0, r0, bEdges, out BoundaryEdge e0, out Vector2 b0, out tE0))
                EnsureBoundaryHit(pts[0], bEdges, ref e0, ref b0, ref tE0);

            Vector2 orig1 = IsPointInsideMesh(pts[K - 1], verts, tris) ? pts[K - 1] : pts[K - 2];
            if (!FindFirstBoundaryHit(orig1, r1, bEdges, out BoundaryEdge e1, out Vector2 b1, out tE1))
                EnsureBoundaryHit(pts[K - 1], bEdges, ref e1, ref b1, ref tE1);

            List<Vector2> fullPts = new List<Vector2> { b0 };
            List<int> fullVerts = new List<int> { InsertBoundaryVertex(e0, b0, tE0, verts, tris) };

            void TryAddPt(Vector2 p, Vector2 edgePt) {
                if (Vector2.Distance(p, edgePt) > 0.015f && IsPointInsideMesh(p, verts, tris)) {
                    fullPts.Add(p); fullVerts.Add(InsertOrFindVertexInMesh(p, verts, tris));
                }
            }
            TryAddPt(pts[0], b0);
            for (int i = 1; i < K - 1; i++) {
                if (IsPointInsideMesh(pts[i], verts, tris)) {
                    fullPts.Add(pts[i]); fullVerts.Add(InsertOrFindVertexInMesh(pts[i], verts, tris));
                }
            }
            TryAddPt(pts[K - 1], b1);
            fullPts.Add(b1); fullVerts.Add(InsertBoundaryVertex(e1, b1, tE1, verts, tris));

            HashSet<long> openCutEdges = new HashSet<long>();
            for (int s = 0; s < fullPts.Count - 1; s++)
                SubdivideAlongSegment(fullPts[s], fullPts[s + 1], fullVerts[s], fullVerts[s + 1], verts, tris, openCutEdges);

            bool flipOpen = discardInside == true;
            BfsKeepComponent(keepPoint, false, false, fullPts, verts, tris, openCutEdges, discardedTris, flipOpen);
        }

        private static int InsertBoundaryVertex(BoundaryEdge e, Vector2 pt, float t, List<Vector3> verts, List<int> tris)
        {
            if (t < 1e-3f) return e.u;
            if (t > 1f - 1e-3f) return e.v;
            int n = verts.Count;
            verts.Add(new Vector3(pt.x, Mathf.Lerp(verts[e.u].y, verts[e.v].y, t), pt.y));
            SplitEdgeInTris(e.u, e.v, n, tris);
            return n;
        }

        private static void EnsureBoundaryHit(
            Vector2 pt, List<BoundaryEdge> bEdges, ref BoundaryEdge e, ref Vector2 b, ref float tE)
        {
            if (bEdges == null || bEdges.Count == 0) return;
            float minD = float.MaxValue;
            for (int i = 0; i < bEdges.Count; i++)
            {
                Vector2 pC = ClosestPointOnSegment(pt, bEdges[i].a, bEdges[i].b, out float t);
                float d = (pt - pC).sqrMagnitude;
                if (d < minD) { minD = d; e = bEdges[i]; b = pC; tE = t; }
            }
        }

        private static int InsertOrFindVertexInMesh(Vector2 pt, List<Vector3> verts, List<int> tris)
        {
            for (int i = 0; i < verts.Count; i++)
                if ((verts[i].x - pt.x) * (verts[i].x - pt.x) + (verts[i].z - pt.y) * (verts[i].z - pt.y) < 1e-6f) return i;

            for (int i = 0; i < tris.Count; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                if (PointInTriangle2D(pt, verts[i0], verts[i1], verts[i2], out float w0, out float w1, out float w2))
                {
                    int n = verts.Count;
                    verts.Add(new Vector3(pt.x, w0 * verts[i0].y + w1 * verts[i1].y + w2 * verts[i2].y, pt.y));
                    tris[i] = i0; tris[i + 1] = i1; tris[i + 2] = n;
                    tris.Add(i1); tris.Add(i2); tris.Add(n); tris.Add(i2); tris.Add(i0); tris.Add(n);
                    return n;
                }
            }
            verts.Add(new Vector3(pt.x, 0.12f, pt.y));
            return verts.Count - 1;
        }

        private static void SubdivideAlongSegment(
            Vector2 a, Vector2 b, int startV, int endV,
            List<Vector3> verts, List<int> tris, HashSet<long> cutEdges)
        {
            HashSet<long> tested = new HashSet<long>();
            List<EdgeHit> hits = new List<EdgeHit>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                TestEdgeIntersection(a, b, tris[i], tris[i + 1], verts, tested, hits);
                TestEdgeIntersection(a, b, tris[i + 1], tris[i + 2], verts, tested, hits);
                TestEdgeIntersection(a, b, tris[i + 2], tris[i], verts, tested, hits);
            }
            hits.Sort((h1, h2) => h1.tSeg.CompareTo(h2.tSeg));
            for (int h = hits.Count - 1; h > 0; h--)
                if (Mathf.Abs(hits[h].tSeg - hits[h - 1].tSeg) < 1e-4f) hits.RemoveAt(h);

            List<int> chain = new List<int>();
            if (startV >= 0) chain.Add(startV);
            for (int h = 0; h < hits.Count; h++)
            {
                int newIdx = verts.Count;
                verts.Add(Vector3.Lerp(verts[hits[h].u], verts[hits[h].v], hits[h].tEdge));
                SplitEdgeInTris(hits[h].u, hits[h].v, newIdx, tris);
                chain.Add(newIdx);
            }
            if (endV >= 0) chain.Add(endV);
            for (int k = 0; k < chain.Count - 1; k++)
            {
                if (chain[k] != chain[k + 1])
                    cutEdges.Add(GetEdgeKey(chain[k], chain[k + 1]));
            }
        }

        private static void BfsKeepComponent(
            Vector2 keepPoint, bool isClosed, bool centerInside, List<Vector2> loopPts,
            List<Vector3> verts, List<int> tris, HashSet<long> cutEdges, List<int> discardedTris = null, bool flipKeep = false)
        {
            if (tris.Count < 3) return;
            int bestTri = -1; float minDst = float.MaxValue;
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 v0 = verts[tris[i]], v1 = verts[tris[i + 1]], v2 = verts[tris[i + 2]];
                Vector2 c = new Vector2((v0.x + v1.x + v2.x) / 3f, (v0.z + v1.z + v2.z) / 3f);
                if (isClosed && loopPts != null && IsPointInPolygon2D(c, loopPts) != centerInside) continue;
                float d2 = (c - keepPoint).sqrMagnitude;
                if (d2 < minDst) { minDst = d2; bestTri = i; }
            }
            if (bestTri == -1) bestTri = 0;

            Dictionary<long, List<int>> edgeToTris = new Dictionary<long, List<int>>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                AddEdgeMap(GetEdgeKey(tris[i], tris[i + 1]), i, edgeToTris, cutEdges);
                AddEdgeMap(GetEdgeKey(tris[i + 1], tris[i + 2]), i, edgeToTris, cutEdges);
                AddEdgeMap(GetEdgeKey(tris[i + 2], tris[i]), i, edgeToTris, cutEdges);
            }

            Queue<int> queue = new Queue<int>();
            HashSet<int> visited = new HashSet<int> { bestTri };
            queue.Enqueue(bestTri);

            while (queue.Count > 0)
            {
                int tri = queue.Dequeue();
                long[] keys = { GetEdgeKey(tris[tri], tris[tri + 1]), GetEdgeKey(tris[tri + 1], tris[tri + 2]), GetEdgeKey(tris[tri + 2], tris[tri]) };
                for (int e = 0; e < 3; e++)
                {
                    if (cutEdges.Contains(keys[e])) continue;
                    if (edgeToTris.TryGetValue(keys[e], out var nbs))
                        for (int n = 0; n < nbs.Count; n++) if (visited.Add(nbs[n])) queue.Enqueue(nbs[n]);
                }
            }

            // Fallback robusto: si el BFS inundó toda la malla por micro-fugas topológicas, clasificar por lado signado
            if (visited.Count * 3 >= tris.Count && loopPts != null && loopPts.Count >= 2)
            {
                visited.Clear();
                float refSide = GetPolylineSide(keepPoint, loopPts);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 v0 = verts[tris[i]], v1 = verts[tris[i + 1]], v2 = verts[tris[i + 2]];
                    Vector2 c = new Vector2((v0.x + v1.x + v2.x) / 3f, (v0.z + v1.z + v2.z) / 3f);
                    float triSide = GetPolylineSide(c, loopPts);
                    if ((triSide >= 0f) == (refSide >= 0f)) visited.Add(i);
                }
            }

            bool keepVisited = !flipKeep;
            List<int> nextTris = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                if (visited.Contains(i) == keepVisited) { nextTris.Add(tris[i]); nextTris.Add(tris[i + 1]); nextTris.Add(tris[i + 2]); }
                else if (discardedTris != null) { discardedTris.Add(tris[i]); discardedTris.Add(tris[i + 1]); discardedTris.Add(tris[i + 2]); }
            }
            tris.Clear(); tris.AddRange(nextTris);
        }

        private static float GetPolylineSide(Vector2 p, List<Vector2> poly)
        {
            float minD = float.MaxValue, bestSide = 0f;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                Vector2 a = poly[i], b = poly[i + 1], seg = b - a;
                float sq = seg.sqrMagnitude;
                if (sq < 1e-7f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, seg) / sq);
                Vector2 diff = p - (a + t * seg);
                float d = diff.sqrMagnitude;
                if (d < minD) { minD = d; bestSide = seg.x * diff.y - seg.y * diff.x; }
            }
            return bestSide;
        }

        private static void AddEdgeMap(long k, int t, Dictionary<long, List<int>> map, HashSet<long> cuts)
        {
            if (!cuts.Contains(k)) { if (!map.TryGetValue(k, out var list)) { list = new List<int>(); map[k] = list; } list.Add(t); }
        }
    }
}
