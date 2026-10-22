using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Utilidad central de corte planar estanco en el yunque:
    /// Trata la pieza como una malla 2.5D prismática estanca, rebana analíticamente
    /// los triángulos de la cara superior preservando las columnas de deformación, normales y faldones,
    /// y orienta la normal de corte estrictamente hacia la zona a conservar (keepPoint).
    /// </summary>
    public static partial class AnvilMeshCutter
    {
        public static bool CutBilletMesh(
            Mesh currentMesh, Vector3[] columns, int[] perimIndices,
            List<Vector3> cutPointsLocal,
            out Vector3[] newColumns, out int[] newPerimIndices, out Mesh newMesh,
            Vector3 baseSize = default, bool? discardInside = null,
            Vector2 keepPoint = default, bool isHandle = false)
        {
            return CutBilletMesh(currentMesh, columns, perimIndices, cutPointsLocal,
                out newColumns, out newPerimIndices, out newMesh, out _, baseSize, discardInside, keepPoint, isHandle);
        }

        public static bool CutBilletMesh(
            Mesh currentMesh, Vector3[] columns, int[] perimIndices,
            List<Vector3> cutPointsLocal,
            out Vector3[] newColumns, out int[] newPerimIndices, out Mesh newMesh,
            out Mesh offcutMesh,
            Vector3 baseSize = default, bool? discardInside = null,
            Vector2 keepPoint = default, bool isHandle = false)
        {
            newColumns = columns;
            newPerimIndices = perimIndices;
            newMesh = null;
            offcutMesh = null;

            if (currentMesh == null || columns == null || perimIndices == null ||
                cutPointsLocal == null || cutPointsLocal.Count == 0)
                return false;

            int M = columns.Length;
            List<Vector3> verts = new List<Vector3>(columns);
            List<int> tris = new List<int>();

            int[] allTris = currentMesh.triangles;
            for (int i = 0; i < allTris.Length; i += 3)
            {
                int a = allTris[i], b = allTris[i + 1], c = allTris[i + 2];
                if (a < M && b < M && c < M) { tris.Add(a); tris.Add(b); tris.Add(c); }
            }
            if (tris.Count < 3) return false;

            List<Vector2> pts2D = new List<Vector2>(cutPointsLocal.Count);
            for (int i = 0; i < cutPointsLocal.Count; i++)
                pts2D.Add(new Vector2(cutPointsLocal[i].x, cutPointsLocal[i].z));

            List<int> discardedTris = new List<int>();

            if (pts2D.Count == 1)
            {
                int kClose = FindClosestVertexIn2D(pts2D[0], columns, perimIndices);
                Vector2 vB = new Vector2(columns[kClose].x, columns[kClose].z);
                CutTrianglesByPolyline(new List<Vector2> { vB, pts2D[0] }, keepPoint, verts, tris, discardedTris, discardInside);
            }
            else
            {
                CutTrianglesByPolyline(pts2D, keepPoint, verts, tris, discardedTris, discardInside);
            }

            if (tris.Count < 3) return false;

            List<int> perim = ExtractPerimeterLoop(verts, tris, out int[] loopLens);
            if (perim.Count < 3) return false;

            CompactVertices(verts, tris, perim, out Vector3[] finalCols, out int[] finalPerim);
            newMesh = AssemblePrismaticMesh(finalCols, finalPerim, loopLens, tris, baseSize);

            newColumns = finalCols;
            newPerimIndices = finalPerim;

            if (discardedTris.Count >= 3)
            {
                List<int> offPerim = ExtractPerimeterLoop(verts, discardedTris, out int[] offLoopLens);
                if (offPerim.Count >= 3)
                {
                    CompactVertices(verts, discardedTris, offPerim, out Vector3[] offCols, out int[] offFinalPerim);
                    offcutMesh = AssemblePrismaticMesh(offCols, offFinalPerim, offLoopLens, discardedTris, baseSize);
                }
            }

            return true;
        }

        public static bool GetCutPreviewTriangles(
            Mesh currentMesh, Vector3[] columns,
            List<Vector3> cutPointsLocal, Vector2 keepPoint, bool flipSide,
            out List<Vector3> discardedTrisLocal)
        {
            discardedTrisLocal = null;
            if (currentMesh == null || columns == null || cutPointsLocal == null || cutPointsLocal.Count == 0)
                return false;

            int M = columns.Length;
            List<Vector3> verts = new List<Vector3>(columns);
            List<int> tris = new List<int>();
            int[] allTris = currentMesh.triangles;
            for (int i = 0; i < allTris.Length; i += 3)
            {
                int a = allTris[i], b = allTris[i + 1], c = allTris[i + 2];
                if (a < M && b < M && c < M) { tris.Add(a); tris.Add(b); tris.Add(c); }
            }
            if (tris.Count < 3) return false;

            List<Vector2> pts2D = new List<Vector2>(cutPointsLocal.Count);
            for (int i = 0; i < cutPointsLocal.Count; i++)
                pts2D.Add(new Vector2(cutPointsLocal[i].x, cutPointsLocal[i].z));

            List<int> discarded = new List<int>();
            if (pts2D.Count == 1)
            {
                int kClose = FindClosestVertexIn2D(pts2D[0], columns, null);
                Vector2 vB = new Vector2(columns[kClose].x, columns[kClose].z);
                CutTrianglesByPolyline(new List<Vector2> { vB, pts2D[0] }, keepPoint, verts, tris, discarded, flipSide);
            }
            else
            {
                CutTrianglesByPolyline(pts2D, keepPoint, verts, tris, discarded, flipSide);
            }

            if (discarded.Count < 3) return false;
            discardedTrisLocal = new List<Vector3>(discarded.Count);
            for (int i = 0; i < discarded.Count; i++)
                discardedTrisLocal.Add(verts[discarded[i]]);
            return true;
        }

        private static void CompactVertices(
            List<Vector3> verts, List<int> tris, List<int> perim,
            out Vector3[] finalVerts, out int[] finalPerim)
        {
            int[] remap = new int[verts.Count];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            List<Vector3> compact = new List<Vector3>();
            for (int i = 0; i < tris.Count; i++)
            {
                int old = tris[i];
                if (remap[old] == -1) { remap[old] = compact.Count; compact.Add(verts[old]); }
                tris[i] = remap[old];
            }

            finalPerim = new int[perim.Count];
            for (int i = 0; i < perim.Count; i++)
            {
                int oldP = perim[i];
                if (remap[oldP] == -1) { remap[oldP] = compact.Count; compact.Add(verts[oldP]); }
                finalPerim[i] = remap[oldP];
            }
            finalVerts = compact.ToArray();
        }

        private static Mesh AssemblePrismaticMesh(
            Vector3[] cols, int[] perim, int[] loopLens, List<int> topTris, Vector3 baseSize = default)
        {
            int M = cols.Length, P = perim.Length;
            Vector3[] verts = new Vector3[2 * M + 2 * P];
            Vector3[] norms = new Vector3[2 * M + 2 * P];
            Vector2[] uvs = new Vector2[2 * M + 2 * P];
            List<int> tris = new List<int>(topTris.Count * 2 + P * 6);

            float invW = baseSize.x > 0.001f ? 1f / baseSize.x : 0f, invH = baseSize.z > 0.001f ? 1f / baseSize.z : 0f;
            float halfX = baseSize.x * 0.5f, halfZ = baseSize.z * 0.5f;

            for (int k = 0; k < M; k++)
            {
                Vector3 c = cols[k];
                Vector2 uv = new Vector2(invW > 0f ? (c.x + halfX) * invW : c.x, invH > 0f ? (c.z + halfZ) * invH : c.z);
                verts[k] = c; norms[k] = Vector3.up; uvs[k] = uv;
                verts[M + k] = new Vector3(c.x, -c.y, c.z); norms[M + k] = Vector3.down; uvs[M + k] = uv;
            }

            if (loopLens == null || loopLens.Length == 0) loopLens = new int[] { P };
            int offset = 0;
            for (int l = 0; l < loopLens.Length; l++)
            {
                int len = loopLens[l];
                for (int i = 0; i < len; i++)
                {
                    int curr = offset + i;
                    int k = perim[curr];
                    Vector3 c = cols[k];
                    Vector3 outDir = new Vector3(c.x, 0f, c.z).normalized;
                    float tp = (float)i / len;
                    verts[2 * M + curr] = c; norms[2 * M + curr] = outDir; uvs[2 * M + curr] = new Vector2(tp, 1f);
                    verts[2 * M + P + curr] = new Vector3(c.x, -c.y, c.z); norms[2 * M + P + curr] = outDir; uvs[2 * M + P + curr] = new Vector2(tp, 0f);

                    int pNext = offset + ((i + 1) % len);
                    int t0 = 2 * M + curr, t1 = 2 * M + pNext, b0 = 2 * M + P + curr, b1 = 2 * M + P + pNext;
                    tris.Add(t0); tris.Add(t1); tris.Add(b1);
                    tris.Add(t0); tris.Add(b1); tris.Add(b0);
                }
                offset += len;
            }

            tris.AddRange(topTris);
            for (int i = 0; i < topTris.Count; i += 3)
            {
                tris.Add(M + topTris[i]); tris.Add(M + topTris[i + 2]); tris.Add(M + topTris[i + 1]);
            }

            Mesh mesh = new Mesh { name = "CutBilletMesh" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0); mesh.RecalculateBounds(); mesh.RecalculateNormals();
            return mesh;
        }
    }
}
