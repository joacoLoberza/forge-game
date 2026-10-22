using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de pulido, lijado y suavizado laplaciano para AnvilController:
    /// Modifica la malla en tiempo de ejecución promediando vértices vecinos conectados por triángulos
    /// (v_nuevo = v + alpha * (v_vecinos - v)), con restricción estrictamente reductiva/erosiva
    /// que desgasta aristas y redondea esquinas sin inflar ni expandir mineral.
    /// </summary>
    public partial class AnvilController
    {
        private Mesh _adjCachedMesh;
        private List<int>[] _cachedVertexAdjacency;

        public bool PolishSurface(Vector3 worldPoint, float brushRadius = 0.055f, float strength = 5.0f)
        {
            if (!isForging || isQuenched || activeIngotMeshFilter == null || _columns == null) return false;
            Mesh mesh = _deformableMesh ?? activeIngotMeshFilter.sharedMesh;
            if (mesh == null) return false;

            int[] triangles = mesh.triangles;
            if (triangles == null || _columns.Length == 0) return false;

            // Matriz de conectividad topológica sobre columnas (en caché)
            if (_adjCachedMesh != mesh || _cachedVertexAdjacency == null || _cachedVertexAdjacency.Length != _columns.Length)
            {
                _cachedVertexAdjacency = BuildColumnAdjacency(_columns.Length, triangles);
                _adjCachedMesh = mesh;
            }

            Vector3 localHit = activeIngotMeshFilter.transform.InverseTransformPoint(worldPoint);
            float rSqr = brushRadius * brushRadius;
            float dt = Time.deltaTime;
            bool modified = false;

            for (int i = 0; i < _columns.Length; i++)
            {
                Vector3 col = _columns[i];
                float dx = col.x - localHit.x, dz = col.z - localHit.z;
                float distSqr = dx * dx + dz * dz;
                if (distSqr > rSqr) continue;

                List<int> neighbors = _cachedVertexAdjacency[i];
                if (neighbors.Count == 0) continue;

                Vector3 avg = Vector3.zero;
                for (int n = 0; n < neighbors.Count; n++) avg += _columns[neighbors[n]];
                avg /= neighbors.Count;

                float dist = Mathf.Sqrt(distSqr);
                float falloff = Mathf.Clamp01(1f - (dist / brushRadius));
                float alpha = Mathf.Clamp01(strength * falloff * dt);

                // Suavizado en altura y redondeo de aristas y esquinas agudas
                float newY = Mathf.Lerp(col.y, avg.y, alpha);
                float newX = Mathf.Lerp(col.x, avg.x, alpha * 0.45f);
                float newZ = Mathf.Lerp(col.z, avg.z, alpha * 0.45f);

                _columns[i] = new Vector3(newX, Mathf.Clamp(newY, minLayerY, _initialLayerY), newZ);
                modified = true;
            }

            if (modified)
            {
                SyncDeformableMesh();
                if (forgingQuality < 1.0f)
                    forgingQuality = Mathf.Min(1.0f, forgingQuality + 0.025f * dt);
                if (!IsCarvingHandle)
                    _remainingMassRatio = Mathf.Max(0.20f, _remainingMassRatio - 0.0004f * dt * strength);
            }

            return modified;
        }

        private static List<int>[] BuildColumnAdjacency(int colCount, int[] triangles)
        {
            List<int>[] adj = new List<int>[colCount];
            for (int i = 0; i < colCount; i++) adj[i] = new List<int>(6);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int i0 = triangles[t], i1 = triangles[t + 1], i2 = triangles[t + 2];
                if (i0 < colCount && i1 < colCount && i2 < colCount)
                {
                    if (!adj[i0].Contains(i1)) adj[i0].Add(i1);
                    if (!adj[i0].Contains(i2)) adj[i0].Add(i2);
                    if (!adj[i1].Contains(i0)) adj[i1].Add(i0);
                    if (!adj[i1].Contains(i2)) adj[i1].Add(i2);
                    if (!adj[i2].Contains(i0)) adj[i2].Add(i0);
                    if (!adj[i2].Contains(i1)) adj[i2].Add(i1);
                }
            }
            return adj;
        }

        public void SyncDeformableMesh()
        {
            if (_deformableMesh == null || _columns == null || _meshVertices == null) return;
            int M = _columns.Length, P = _perimIndices != null ? _perimIndices.Length : 0;
            for (int k = 0; k < M; k++)
            {
                Vector3 col = _columns[k];
                _meshVertices[k] = col;
                _meshVertices[M + k] = new Vector3(col.x, -col.y, col.z);
            }
            for (int p = 0; p < P; p++)
            {
                int k = _perimIndices[p];
                if (k >= 0 && k < M)
                {
                    Vector3 col = _columns[k];
                    _meshVertices[2 * M + p] = col;
                    _meshVertices[2 * M + P + p] = new Vector3(col.x, -col.y, col.z);
                }
            }
            _deformableMesh.vertices = _meshVertices;
            _deformableMesh.RecalculateNormals();
            _deformableMesh.RecalculateBounds();
            RefreshCollider();
        }

        public Vector3 GetDominantSharpDirection()
        {
            int f = SharpFrontCount, b = SharpBackCount, r = SharpRightCount, l = SharpLeftCount;
            int max = Mathf.Max(f, Mathf.Max(b, Mathf.Max(r, l)));
            if (max >= 2)
            {
                if (f == max) return Vector3.forward;
                if (b == max) return Vector3.back;
                if (r == max) return Vector3.right;
                return Vector3.left;
            }
            if (_columns == null || _columns.Length == 0) return Vector3.forward;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i].x < minX) minX = _columns[i].x; if (_columns[i].x > maxX) maxX = _columns[i].x;
                if (_columns[i].z < minZ) minZ = _columns[i].z; if (_columns[i].z > maxZ) maxZ = _columns[i].z;
            }
            float spanX = Mathf.Max(0.04f, maxX - minX), spanZ = Mathf.Max(0.04f, maxZ - minZ);
            float midX = (minX + maxX) * 0.5f, midZ = (minZ + maxZ) * 0.5f, sumX = 0f, sumZ = 0f, totalW = 0f;
            for (int i = 0; i < _columns.Length; i++)
            {
                float t = Mathf.Max(0f, _initialLayerY - _columns[i].y);
                if (t > 0.003f)
                {
                    float w = t * t;
                    sumX += ((_columns[i].x - midX) / spanX) * w;
                    sumZ += ((_columns[i].z - midZ) / spanZ) * w;
                    totalW += w;
                }
            }
            if (totalW < 1e-5f) return Vector3.forward;
            return Mathf.Abs(sumZ) >= Mathf.Abs(sumX) ? (sumZ > 0f ? Vector3.forward : Vector3.back) : (sumX > 0f ? Vector3.right : Vector3.left);
        }
    }
}
