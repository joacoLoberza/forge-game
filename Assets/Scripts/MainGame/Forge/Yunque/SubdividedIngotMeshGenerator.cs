using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Genera una malla prismática estanca (2.5D) continua para lingotes y hojas de forja.
    /// Garantiza simetría vertical en Y y elimina matemáticamente cualquier posibilidad de
    /// aberturas o grietas entre caras superiores, inferiores y perimetrales.
    /// </summary>
    public static class SubdividedIngotMeshGenerator
    {
        public static Mesh CreateSubdividedBillet(
            int cols, int rows, Vector3 size,
            out int[] perimIndices, out Vector3[] initialColumns)
        {
            if (size == Vector3.zero) size = new Vector3(0.52f, 0.14f, 0.24f);

            int M = cols * rows;
            Vector3[] columns = new Vector3[M];
            float halfX = size.x * 0.5f, halfY = size.y * 0.5f, halfZ = size.z * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                float tz = (float)r / (rows - 1);
                float z = Mathf.Lerp(-halfZ, halfZ, tz);
                for (int c = 0; c < cols; c++)
                {
                    float tx = (float)c / (cols - 1);
                    float x = Mathf.Lerp(-halfX, halfX, tx);
                    columns[r * cols + c] = new Vector3(x, halfY, z);
                }
            }

            // Bucle perimetral horario cerrado
            List<int> perim = new();
            for (int c = 0; c < cols - 1; c++) perim.Add(0 * cols + c);
            for (int r = 0; r < rows - 1; r++) perim.Add(r * cols + (cols - 1));
            for (int c = cols - 1; c > 0; c--) perim.Add((rows - 1) * cols + c);
            for (int r = rows - 1; r > 0; r--) perim.Add(r * cols + 0);

            int P = perim.Count;
            perimIndices = perim.ToArray();

            Vector3[] verts = new Vector3[2 * M + 2 * P];
            Vector3[] norms = new Vector3[2 * M + 2 * P];
            Vector2[] uvs = new Vector2[2 * M + 2 * P];
            List<int> tris = new();

            // Cara superior
            for (int r = 0; r < rows; r++)
            {
                float tr = (float)r / (rows - 1);
                for (int c = 0; c < cols; c++)
                {
                    int k = r * cols + c;
                    verts[k] = new Vector3(columns[k].x, columns[k].y, columns[k].z);
                    norms[k] = Vector3.up;
                    uvs[k] = new Vector2((float)c / (cols - 1), tr);
                }
            }

            // Cara inferior (simétrica en Y = 0)
            for (int r = 0; r < rows; r++)
            {
                float tr = (float)r / (rows - 1);
                for (int c = 0; c < cols; c++)
                {
                    int k = r * cols + c;
                    verts[M + k] = new Vector3(columns[k].x, -columns[k].y, columns[k].z);
                    norms[M + k] = Vector3.down;
                    uvs[M + k] = new Vector2((float)c / (cols - 1), tr);
                }
            }

            // Faldón perimetral cerrado
            for (int p = 0; p < P; p++)
            {
                int k = perimIndices[p];
                float tp = (float)p / P;
                Vector3 outDir = new Vector3(columns[k].x, 0f, columns[k].z).normalized;
                verts[2 * M + p] = new Vector3(columns[k].x, columns[k].y, columns[k].z);
                norms[2 * M + p] = outDir;
                uvs[2 * M + p] = new Vector2(tp, 1f);

                verts[2 * M + P + p] = new Vector3(columns[k].x, -columns[k].y, columns[k].z);
                norms[2 * M + P + p] = outDir;
                uvs[2 * M + P + p] = new Vector2(tp, 0f);
            }

            // Triángulos cara superior
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int v0 = r * cols + c, v1 = v0 + 1, v2 = (r + 1) * cols + c, v3 = v2 + 1;
                    tris.Add(v0); tris.Add(v2); tris.Add(v1);
                    tris.Add(v1); tris.Add(v2); tris.Add(v3);
                }
            }

            // Triángulos cara inferior
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int v0 = M + r * cols + c, v1 = v0 + 1, v2 = M + (r + 1) * cols + c, v3 = v2 + 1;
                    tris.Add(v0); tris.Add(v1); tris.Add(v2);
                    tris.Add(v1); tris.Add(v3); tris.Add(v2);
                }
            }

            // Triángulos faldón perimetral
            for (int p = 0; p < P; p++)
            {
                int pNext = (p + 1) % P;
                int t0 = 2 * M + p, t1 = 2 * M + pNext;
                int b0 = 2 * M + P + p, b1 = 2 * M + P + pNext;
                tris.Add(t0); tris.Add(t1); tris.Add(b1);
                tris.Add(t0); tris.Add(b1); tris.Add(b0);
            }

            Mesh mesh = new() { name = "WatertightBilletMesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            initialColumns = columns;
            return mesh;
        }

        public static Mesh CreateSubdividedIngot(int segX = 16, int segY = 4, int segZ = 10, Vector3 size = default)
        {
            return CreateSubdividedBillet(segX + 1, segZ + 1, size, out _, out _);
        }
    }
}
