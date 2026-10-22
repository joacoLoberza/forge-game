using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de detección y reconstrucción de lazos cerrados para AnvilMeshCutter:
    /// Aplica proximidad de extremos (Regla 1) e intersección de barreras infinitas (Regla 2).
    /// </summary>
    public static partial class AnvilMeshCutter
    {
        private const float MaxCutExtension = 0.10f;

        public static bool IsClosedAreaCut(
            List<Vector3> cutPointsLocal, List<Vector2> perimPoly, out List<Vector3> previewLoopLocal)
        {
            previewLoopLocal = null;
            if (cutPointsLocal == null || cutPointsLocal.Count < 3) return false;

            List<Vector2> pts2D = new List<Vector2>(cutPointsLocal.Count);
            float avgY = 0f;
            for (int i = 0; i < cutPointsLocal.Count; i++)
            {
                pts2D.Add(new Vector2(cutPointsLocal[i].x, cutPointsLocal[i].z));
                avgY += cutPointsLocal[i].y;
            }
            avgY /= cutPointsLocal.Count;

            if (!DetectAndBuildClosedLoop(pts2D, null, null, null, perimPoly, out List<Vector2> closed2D))
                return false;

            previewLoopLocal = new List<Vector3>(closed2D.Count);
            for (int i = 0; i < closed2D.Count; i++)
                previewLoopLocal.Add(new Vector3(closed2D[i].x, avgY, closed2D[i].y));

            return true;
        }

        public static bool IsClosedAreaCut(List<Vector3> cutPointsLocal, out List<Vector3> previewLoopLocal) =>
            IsClosedAreaCut(cutPointsLocal, null, out previewLoopLocal);

        public static bool DetectAndBuildClosedLoop(
            List<Vector2> pts, List<BoundaryEdge> bEdges, List<Vector3> verts, List<int> tris,
            List<Vector2> perimPoly, out List<Vector2> closedPoly)
        {
            closedPoly = null;
            if (pts == null || pts.Count < 3) return false;
            int K = pts.Count;

            Vector2 d0 = pts[0] - pts[1], d1 = pts[K - 1] - pts[K - 2];
            Vector2 r0Dir = d0.sqrMagnitude < 1e-6f ? Vector2.up : d0.normalized;
            Vector2 r1Dir = d1.sqrMagnitude < 1e-6f ? Vector2.down : d1.normalized;

            float distB0 = GetBoundaryExitDistance(pts[0], r0Dir, bEdges, perimPoly);
            float distB1 = GetBoundaryExitDistance(pts[K - 1], r1Dir, bEdges, perimPoly);

            // 1. Proximidad directa de extremos
            float endGap = Vector2.Distance(pts[K - 1], pts[0]);
            if (endGap <= 0.035f)
            {
                List<Vector2> loop = new List<Vector2>(pts) { pts[0] };
                if (Mathf.Abs(GetPolygonArea2D(loop)) > 1e-4f) { closedPoly = loop; return true; }
            }

            // 2. Segmento final cruza algún segmento previo definido
            for (int i = 0; i < K - 2; i++)
            {
                if (SegmentsIntersect2D(pts[K - 2], pts[K - 1], pts[i], pts[i + 1], out Vector2 xPt, out _, out _))
                {
                    List<Vector2> loop = new List<Vector2> { xPt };
                    for (int s = i + 1; s <= K - 2; s++) loop.Add(pts[s]);
                    loop.Add(xPt);
                    if (Mathf.Abs(GetPolygonArea2D(loop)) > 1e-4f) { closedPoly = loop; return true; }
                }
            }

            // 3. Proyección del último punto intersecta la polilínea definida antes de salir del lingote
            for (int i = 0; i < K - 2; i++)
            {
                if (RayIntersectsSegment(pts[K - 1], r1Dir, pts[i], pts[i + 1], out Vector2 hPt, out float tRay, out _) && tRay <= 0.25f && tRay < distB1)
                {
                    List<Vector2> loop = new List<Vector2> { hPt };
                    for (int s = i + 1; s < K; s++) loop.Add(pts[s]);
                    loop.Add(hPt);
                    if (Mathf.Abs(GetPolygonArea2D(loop)) > 1e-4f) { closedPoly = loop; return true; }
                }
            }

            // 4. Cierre guiado al extremo superior exclusivamente para mango de madera (perimPoly definido)
            if (K >= 3 && perimPoly != null && perimPoly.Count >= 3)
            {
                float maxX = float.MinValue;
                for (int i = 0; i < K; i++) if (pts[i].x > maxX) maxX = pts[i].x;
                if (pts[0].x > maxX - 0.25f && pts[K - 1].x > maxX - 0.25f)
                {
                    bool selfCross = false;
                    for (int s = 1; s < K - 2; s++)
                        if (SegmentsIntersect2D(pts[K - 1], pts[0], pts[s], pts[s + 1], out _, out _, out _)) { selfCross = true; break; }

                    List<Vector2> loop = new List<Vector2>(pts);
                    if (!selfCross) loop.Add(pts[0]);
                    else
                    {
                        float safeX = maxX + 0.025f;
                        loop.Add(new Vector2(safeX, pts[K - 1].y));
                        loop.Add(new Vector2(safeX, pts[0].y));
                        loop.Add(pts[0]);
                    }
                    if (Mathf.Abs(GetPolygonArea2D(loop)) > 0.003f) { closedPoly = loop; return true; }
                }
            }

            return false;
        }

        private static bool IsInsideIngot(
            Vector2 pt, List<Vector3> verts, List<int> tris, List<Vector2> perimPoly)
        {
            if (perimPoly != null && perimPoly.Count >= 3)
                return IsPointInPolygon2D(pt, perimPoly);
            return true;
        }

        private static float GetBoundaryExitDistance(
            Vector2 origin, Vector2 dir, List<BoundaryEdge> bEdges, List<Vector2> perimPoly)
        {
            float minT = float.MaxValue;
            if (bEdges != null && bEdges.Count > 0)
            {
                for (int i = 0; i < bEdges.Count; i++)
                {
                    if (RayIntersectsSegment(origin, dir, bEdges[i].a, bEdges[i].b, out _, out float t, out _))
                        if (t > 1e-4f && t < minT) minT = t;
                }
            }
            else if (perimPoly != null && perimPoly.Count >= 3)
            {
                for (int i = 0, j = perimPoly.Count - 1; i < perimPoly.Count; j = i++)
                {
                    if (RayIntersectsSegment(origin, dir, perimPoly[j], perimPoly[i], out _, out float t, out _))
                        if (t > 1e-4f && t < minT) minT = t;
                }
            }
            return minT;
        }

        public static bool RayIntersectsSegment(
            Vector2 origin, Vector2 dir, Vector2 a, Vector2 b,
            out Vector2 hitPt, out float tRay, out float uSeg)
        {
            hitPt = Vector2.zero; tRay = 0f; uSeg = 0f;
            Vector2 s = b - a;
            float cross = dir.x * s.y - dir.y * s.x;
            if (Mathf.Abs(cross) < 1e-6f) return false;
            Vector2 d = a - origin;
            float t = (d.x * s.y - d.y * s.x) / cross;
            float u = (d.x * dir.y - d.y * dir.x) / cross;
            if (t > 1e-3f && t < 10.0f && u >= -1e-4f && u <= 1f + 1e-4f)
            {
                tRay = t; uSeg = Mathf.Clamp01(u); hitPt = a + uSeg * s;
                return true;
            }
            return false;
        }

        public static bool SegmentsIntersect2D(
            Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2,
            out Vector2 xPt, out float tA, out float tB)
        {
            xPt = Vector2.zero; tA = 0f; tB = 0f;
            Vector2 r = a2 - a1, s = b2 - b1;
            float cross = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(cross) < 1e-6f) return false;
            Vector2 d = b1 - a1;
            tA = (d.x * s.y - d.y * s.x) / cross;
            tB = (d.x * r.y - d.y * r.x) / cross;
            if (tA >= 0f && tA <= 1f && tB >= 0f && tB <= 1f)
            {
                xPt = a1 + tA * r;
                return true;
            }
            return false;
        }

        public static bool IsPointInPolygon2D(Vector2 pt, List<Vector2> poly)
        {
            if (poly == null || poly.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 pi = poly[i], pj = poly[j];
                if (((pi.y > pt.y) != (pj.y > pt.y)) && (pt.x < (pj.x - pi.x) * (pt.y - pi.y) / (pj.y - pi.y) + pi.x))
                    inside = !inside;
            }
            return inside;
        }

        public static float GetPolygonArea2D(List<Vector2> poly)
        {
            if (poly == null || poly.Count < 3) return 0f;
            float area = 0f;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                area += (poly[j].x * poly[i].y) - (poly[i].x * poly[j].y);
            return area * 0.5f;
        }
    }
}
