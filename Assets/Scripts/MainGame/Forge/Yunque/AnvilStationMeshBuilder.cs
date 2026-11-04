using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Generador procedural de mallas y materiales para los elementos visuales de la estación del yunque:
    /// Barril de templado (duelas exteriores e interiores con caras completas, aros y agua) y fogón de recalentado.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public static partial class AnvilStationMeshBuilder
    {
        public static Mesh CreateBarrelMesh(float rBottom, float rBelly, float rTop, float height, int segments = 18)
        {
            Mesh m = new Mesh { name = "ProceduralQuenchBarrelMesh" };
            int rings = 9, ringVerts = segments + 1;
            int vertCount = (rings + 1) * ringVerts + 2;
            Vector3[] verts = new Vector3[vertCount]; Vector3[] norms = new Vector3[vertCount]; Vector2[] uvs = new Vector2[vertCount];

            float[] yFrac = { 0.00f, 0.25f, 0.50f, 0.75f, 1.00f, 1.00f, 0.75f, 0.50f, 0.25f, 0.04f };
            float th = 0.035f;
            float[] rFrac = {
                rBottom, Mathf.Lerp(rBottom, rBelly, 0.8f), rBelly, Mathf.Lerp(rTop, rBelly, 0.8f), rTop,
                rTop - th, Mathf.Lerp(rTop, rBelly, 0.75f) - th, rBelly - th, Mathf.Lerp(rBottom, rBelly, 0.75f) - th, rBottom - th
            };

            int idx = 0;
            for (int r = 0; r <= rings; r++)
            {
                float y = yFrac[r] * height, rad = rFrac[r];
                bool isInner = r >= 6, isLip = r == 5;
                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments, ang = u * Mathf.PI * 2f, cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                    verts[idx] = new Vector3(cos * rad, y, sin * rad);
                    if (isLip) norms[idx] = Vector3.up;
                    else if (isInner) norms[idx] = new Vector3(-cos, 0f, -sin);
                    else norms[idx] = new Vector3(cos, (r == 0 ? -0.2f : (r == 4 ? 0.2f : 0f)), sin).normalized;
                    uvs[idx] = new Vector2(u * 3f, yFrac[r]);
                    idx++;
                }
            }

            int botInnerCenterIdx = idx;
            verts[idx] = new Vector3(0f, 0.04f * height, 0f); norms[idx] = Vector3.up; uvs[idx] = new Vector2(0.5f, 0.5f); idx++;
            int botOuterCenterIdx = idx;
            verts[idx] = Vector3.zero; norms[idx] = Vector3.down; uvs[idx] = new Vector2(0.5f, 0.5f); idx++;

            int triCount = rings * segments * 6 + segments * 6;
            int[] tris = new int[triCount];
            int tIdx = 0;

            for (int r = 0; r < rings; r++)
            {
                int r0 = r * ringVerts, r1 = (r + 1) * ringVerts;
                for (int s = 0; s < segments; s++)
                {
                    tris[tIdx++] = r0 + s; tris[tIdx++] = r1 + s; tris[tIdx++] = r0 + s + 1;
                    tris[tIdx++] = r0 + s + 1; tris[tIdx++] = r1 + s; tris[tIdx++] = r1 + s + 1;
                }
            }

            int r9 = 9 * ringVerts;
            for (int s = 0; s < segments; s++) { tris[tIdx++] = botInnerCenterIdx; tris[tIdx++] = r9 + s + 1; tris[tIdx++] = r9 + s; }
            for (int s = 0; s < segments; s++) { tris[tIdx++] = botOuterCenterIdx; tris[tIdx++] = s; tris[tIdx++] = s + 1; }

            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        public static Mesh CreateHoopMesh(float radius, float height, float thickness, int segments = 18)
        {
            Mesh m = new Mesh { name = "ProceduralHoopMesh" };
            Vector3[] verts = new Vector3[(segments + 1) * 4];
            Vector3[] norms = new Vector3[verts.Length]; Vector2[] uvs = new Vector2[verts.Length];
            float rOut = radius + thickness;

            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments, ang = u * Mathf.PI * 2f, cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                int b = s * 4;
                verts[b + 0] = new Vector3(cos * radius, 0f, sin * radius); verts[b + 1] = new Vector3(cos * rOut, 0f, sin * rOut);
                verts[b + 2] = new Vector3(cos * rOut, height, sin * rOut); verts[b + 3] = new Vector3(cos * radius, height, sin * radius);
                Vector3 n = new Vector3(cos, 0f, sin);
                norms[b + 0] = -n; norms[b + 1] = n; norms[b + 2] = n; norms[b + 3] = -n;
                uvs[b + 0] = new Vector2(u * 2f, 0f); uvs[b + 1] = new Vector2(u * 2f, 0.5f);
                uvs[b + 2] = new Vector2(u * 2f, 1f); uvs[b + 3] = new Vector2(u * 2f, 0.5f);
            }

            int[] tris = new int[segments * 18]; int t = 0;
            for (int s = 0; s < segments; s++)
            {
                int c0 = s * 4, c1 = (s + 1) * 4;
                tris[t++] = c0 + 1; tris[t++] = c1 + 1; tris[t++] = c0 + 2;
                tris[t++] = c0 + 2; tris[t++] = c1 + 1; tris[t++] = c1 + 2;
                tris[t++] = c0 + 2; tris[t++] = c1 + 2; tris[t++] = c0 + 3;
                tris[t++] = c0 + 3; tris[t++] = c1 + 2; tris[t++] = c1 + 3;
                tris[t++] = c0 + 0; tris[t++] = c1 + 0; tris[t++] = c0 + 1;
                tris[t++] = c0 + 1; tris[t++] = c1 + 0; tris[t++] = c1 + 1;
            }

            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        public static Mesh CreateWaterDiscMesh(float radius, int segments = 18)
        {
            Mesh m = new Mesh { name = "ProceduralWaterDiscMesh" };
            Vector3[] verts = new Vector3[segments + 2]; Vector3[] norms = new Vector3[verts.Length]; Vector2[] uvs = new Vector2[verts.Length];
            verts[0] = Vector3.zero; norms[0] = Vector3.up; uvs[0] = new Vector2(0.5f, 0.5f);

            for (int s = 0; s <= segments; s++)
            {
                float ang = (float)s / segments * Mathf.PI * 2f, cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                verts[s + 1] = new Vector3(cos * radius, 0f, sin * radius); norms[s + 1] = Vector3.up;
                uvs[s + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
            }

            int[] tris = new int[segments * 3];
            for (int s = 0; s < segments; s++) { tris[s * 3 + 0] = 0; tris[s * 3 + 1] = s + 2; tris[s * 3 + 2] = s + 1; }
            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        public static Mesh CreateBrazierBowlMesh(float rBase, float rRim, float height, int segments = 16)
        {
            Mesh m = new Mesh { name = "ProceduralBrazierBowlMesh" };
            int rings = 3;
            Vector3[] verts = new Vector3[(rings + 1) * (segments + 1)]; Vector3[] norms = new Vector3[verts.Length]; Vector2[] uvs = new Vector2[verts.Length];
            float[] ySteps = { 0f, height * 0.45f, height, height * 0.88f };
            float[] rSteps = { rBase, Mathf.Lerp(rBase, rRim, 0.5f), rRim, rRim - 0.045f };

            int idx = 0;
            for (int r = 0; r <= rings; r++)
            {
                float y = ySteps[r], rad = rSteps[r];
                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments, ang = u * Mathf.PI * 2f;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
                    norms[idx] = r < rings ? new Vector3(verts[idx].x, 0.3f, verts[idx].z).normalized : Vector3.up;
                    uvs[idx] = new Vector2(u * 2f, ySteps[r] / height);
                    idx++;
                }
            }

            int[] tris = new int[rings * segments * 6]; int t = 0;
            for (int r = 0; r < rings; r++)
            {
                int r0 = r * (segments + 1), r1 = (r + 1) * (segments + 1);
                for (int s = 0; s < segments; s++)
                {
                    tris[t++] = r0 + s; tris[t++] = r1 + s; tris[t++] = r0 + s + 1;
                    tris[t++] = r0 + s + 1; tris[t++] = r1 + s; tris[t++] = r1 + s + 1;
                }
            }

            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        public static Mesh CreatePedestalMesh(float rBase, float rTop, float height, int segments = 12)
        {
            Mesh m = new Mesh { name = "ProceduralPedestalMesh" };
            int rings = 3;
            Vector3[] verts = new Vector3[(rings + 1) * (segments + 1)]; Vector3[] norms = new Vector3[verts.Length]; Vector2[] uvs = new Vector2[verts.Length];
            float[] ySteps = { 0f, 0.10f, height - 0.08f, height };
            float[] rSteps = { rBase * 1.25f, rBase, rTop, rTop * 1.15f };

            int idx = 0;
            for (int r = 0; r <= rings; r++)
            {
                float y = ySteps[r], rad = rSteps[r];
                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments, ang = u * Mathf.PI * 2f;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
                    norms[idx] = new Vector3(verts[idx].x, 0f, verts[idx].z).normalized;
                    uvs[idx] = new Vector2(u * 2f, ySteps[r] / height);
                    idx++;
                }
            }

            int[] tris = new int[rings * segments * 6]; int t = 0;
            for (int r = 0; r < rings; r++)
            {
                int r0 = r * (segments + 1), r1 = (r + 1) * (segments + 1);
                for (int s = 0; s < segments; s++)
                {
                    tris[t++] = r0 + s; tris[t++] = r1 + s; tris[t++] = r0 + s + 1;
                    tris[t++] = r0 + s + 1; tris[t++] = r1 + s; tris[t++] = r1 + s + 1;
                }
            }

            m.vertices = verts; m.normals = norms; m.uv = uvs; m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
