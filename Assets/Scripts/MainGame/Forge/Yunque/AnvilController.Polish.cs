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
        private float _nextTipPruneTime, _polishShineProgress;

        public bool PolishSurface(Vector3 worldPoint, float brushRadius = 0.042f, float strength = 1.55f)
        {
            if (!isForging || isQuenched || activeIngotMeshFilter == null || _columns == null) return false;
            Mesh mesh = _deformableMesh ?? activeIngotMeshFilter.sharedMesh;
            if (mesh == null) return false;

            int[] triangles = mesh.triangles;
            if (triangles == null || _columns.Length == 0) return false;

            if (_adjCachedMesh != mesh || _cachedVertexAdjacency == null || _cachedVertexAdjacency.Length != _columns.Length)
            {
                _cachedVertexAdjacency = BuildColumnAdjacency(_columns.Length, triangles);
                _adjCachedMesh = mesh;
            }

            Vector3 localHit = activeIngotMeshFilter.transform.InverseTransformPoint(worldPoint);
            float rSqr = brushRadius * brushRadius, dt = Time.deltaTime;
            bool modified = false;

            // 1. Redondear puntas / esquinas perimetrales y podar vertices "extra" excesivamente finos
            int pruneCandidate = -1;
            float bestPruneScore = 0f;
            if (_perimIndices != null && _perimIndices.Length >= 4)
            {
                int P = _perimIndices.Length;
                for (int p = 0; p < P; p++)
                {
                    int idx = _perimIndices[p];
                    if (idx < 0 || idx >= _columns.Length) continue;
                    Vector3 cur = _columns[idx];
                    float dx = cur.x - localHit.x, dz = cur.z - localHit.z;
                    float dSqr = dx * dx + dz * dz;
                    if (dSqr > rSqr) continue;

                    int prevIdx = _perimIndices[(p - 1 + P) % P], nextIdx = _perimIndices[(p + 1) % P];
                    Vector2 p2 = new Vector2(cur.x, cur.z);
                    Vector2 a2 = new Vector2(_columns[prevIdx].x, _columns[prevIdx].z);
                    Vector2 b2 = new Vector2(_columns[nextIdx].x, _columns[nextIdx].z);
                    Vector2 va = a2 - p2, vb = b2 - p2;
                    float la = va.magnitude, lb = vb.magnitude;
                    if (la < 1e-4f || lb < 1e-4f) continue;

                    float sharpDot = Vector2.Dot(va / la, vb / lb); // +1 = aguja extrema, -1 = borde recto
                    float baseWidth = Vector2.Distance(a2, b2);
                    if (sharpDot > 0.55f || (sharpDot > 0.25f && baseWidth < 0.026f))
                    {
                        float score = sharpDot + (baseWidth < 0.026f ? 0.5f : 0f);
                        if (score > bestPruneScore && _cachedVertexAdjacency[idx].Count <= 3) { bestPruneScore = score; pruneCandidate = idx; }
                    }
                    if (sharpDot > -0.35f)
                    {
                        float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dSqr) / brushRadius);
                        float roundAlpha = Mathf.Clamp(strength * 0.72f * falloff * dt, 0f, 0.16f);
                        Vector2 midXZ = (a2 + b2) * 0.5f;
                        float avgNbY = (_columns[prevIdx].y + _columns[nextIdx].y) * 0.5f;
                        // Redondear hacia adentro en XZ sin adelgazar la punta en Y
                        Vector2 rounded = Vector2.Lerp(p2, midXZ, roundAlpha);
                        float roundedY = Mathf.Lerp(cur.y, Mathf.Max(cur.y, avgNbY), roundAlpha * 0.5f);
                        _columns[idx] = new Vector3(rounded.x, Mathf.Clamp(roundedY, minLayerY, _initialLayerY), rounded.y);
                        modified = true;
                    }
                }
            }

            if (pruneCandidate >= 0 && Time.time >= _nextTipPruneTime
                && AnvilMeshCutter.TryPruneSpikyVertex(mesh, _columns, pruneCandidate, _currentBaseSize, out var newCols, out var newPerim, out Mesh prunedMesh))
            {
                _nextTipPruneTime = Time.time + 0.14f;
                _deformableMesh = prunedMesh; _columns = newCols; _perimIndices = newPerim; _meshVertices = _deformableMesh.vertices;
                _adjCachedMesh = null; _cachedVertexAdjacency = null;
                if (activeIngotMeshFilter != null) activeIngotMeshFilter.mesh = _deformableMesh;
                RebuildEdgeIndices(); RefreshCollider(); EvaluateEdgeCompletion();
                return true;
            }

            // 2. Suavizado suave y controlado de superficie interior (sin potencia excesiva)
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

                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(distSqr) / brushRadius);
                float alpha = Mathf.Clamp(strength * 0.48f * falloff * dt, 0f, 0.12f);
                float newY = Mathf.Lerp(col.y, avg.y, alpha);
                float newX = Mathf.Lerp(col.x, avg.x, alpha * 0.35f);
                float newZ = Mathf.Lerp(col.z, avg.z, alpha * 0.35f);
                _columns[i] = new Vector3(newX, Mathf.Clamp(newY, minLayerY, _initialLayerY), newZ);
                modified = true;
            }

            if (modified)
            {
                SyncDeformableMesh();
                ApplyPolishedSurfaceLuster(dt);
                if (forgingQuality < 1.0f) forgingQuality = Mathf.Min(1.0f, forgingQuality + 0.035f * dt);
                if (!IsCarvingHandle) _remainingMassRatio = Mathf.Max(0.20f, _remainingMassRatio - 0.00015f * dt * strength);
            }
            return modified;
        }

        private void ApplyPolishedSurfaceLuster(float dt)
        {
            _polishShineProgress = Mathf.Clamp01(_polishShineProgress + dt * 0.28f);
            if (activeIngotMeshRenderer == null || activeIngotMeshRenderer.material == null) return;
            Material mat = activeIngotMeshRenderer.material;
            if (IsCarvingHandle)
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Lerp(0.20f, 0.62f, _polishShineProgress));
                Color wood = Color.Lerp(new Color(0.60f, 0.40f, 0.24f), new Color(0.72f, 0.50f, 0.30f), _polishShineProgress);
                mat.color = wood; if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", wood);
                return;
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Lerp(0.65f, 0.96f, _polishShineProgress));
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Lerp(0.85f, 0.97f, _polishShineProgress));
            Color gleam = Color.Lerp(_currentMetalBaseColor, Color.white, _polishShineProgress * 0.16f);
            mat.color = gleam; if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gleam);
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
                    if (!adj[i0].Contains(i1)) adj[i0].Add(i1); if (!adj[i0].Contains(i2)) adj[i0].Add(i2);
                    if (!adj[i1].Contains(i0)) adj[i1].Add(i0); if (!adj[i1].Contains(i2)) adj[i1].Add(i2);
                    if (!adj[i2].Contains(i0)) adj[i2].Add(i0); if (!adj[i2].Contains(i1)) adj[i2].Add(i1);
                }
            }
            return adj;
        }

        public void SyncDeformableMesh()
        {
            if (_deformableMesh == null || _columns == null || _meshVertices == null) return;
            int M = _columns.Length, P = _perimIndices != null ? _perimIndices.Length : 0;
            for (int k = 0; k < M; k++) { Vector3 col = _columns[k]; _meshVertices[k] = col; _meshVertices[M + k] = new Vector3(col.x, -col.y, col.z); }
            for (int p = 0; p < P; p++)
            {
                int k = _perimIndices[p];
                if (k >= 0 && k < M) { Vector3 col = _columns[k]; _meshVertices[2 * M + p] = col; _meshVertices[2 * M + P + p] = new Vector3(col.x, -col.y, col.z); }
            }
            _deformableMesh.vertices = _meshVertices; _deformableMesh.RecalculateNormals(); _deformableMesh.RecalculateBounds(); RefreshCollider();
        }

        public Vector3 GetDominantSharpDirection()
        {
            int f = SharpFrontCount, b = SharpBackCount, r = SharpRightCount, l = SharpLeftCount;
            int max = Mathf.Max(f, Mathf.Max(b, Mathf.Max(r, l)));
            if (max >= 2) return f == max ? Vector3.forward : (b == max ? Vector3.back : (r == max ? Vector3.right : Vector3.left));
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
                if (t > 0.003f) { float w = t * t; sumX += ((_columns[i].x - midX) / spanX) * w; sumZ += ((_columns[i].z - midZ) / spanZ) * w; totalW += w; }
            }
            if (totalW < 1e-5f) return Vector3.forward;
            return Mathf.Abs(sumZ) >= Mathf.Abs(sumX) ? (sumZ > 0f ? Vector3.forward : Vector3.back) : (sumX > 0f ? Vector3.right : Vector3.left);
        }

        public void GetEffectiveStrikeRadii(out float innerR, out float outerR)
        {
            float scale = _selectedWeaponType == WeaponType.Sword ? 0.65f : ((_currentBaseSize.x > 0.05f ? _currentBaseSize.x : 0.85f) / 0.85f);
            innerR = hitInnerRadius * scale; outerR = hitOuterRadius * scale;
        }

        public bool IsPointOverstruck(Vector3 localPoint)
        {
            if (_columns == null || _columns.Length == 0) return false;
            float scale = _selectedWeaponType == WeaponType.Sword ? 0.65f : ((_currentBaseSize.x > 0.05f ? _currentBaseSize.x : 0.85f) / 0.85f);
            float rSqr = (0.07f * scale) * (0.07f * scale), minSharpThreshold = minLayerY + 0.0025f;
            for (int k = 0; k < _columns.Length; k++)
            {
                float dx = _columns[k].x - localPoint.x, dz = _columns[k].z - localPoint.z;
                if (dx * dx + dz * dz <= rSqr && _columns[k].y <= minSharpThreshold) return true;
            }
            return false;
        }

        public float GetSurfaceTopYAt(Vector3 localPoint, out float maxTopY)
        {
            float fallback = (_currentBaseSize.y > 0.005f ? _currentBaseSize.y : 0.10f) * 0.5f;
            maxTopY = fallback;
            if (_columns == null || _columns.Length == 0) return fallback;
            maxTopY = minLayerY;
            float bestDistSqr = float.MaxValue, localTopY = fallback;
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i].y > maxTopY) maxTopY = _columns[i].y;
                float dx = _columns[i].x - localPoint.x, dz = _columns[i].z - localPoint.z;
                float dSqr = dx * dx + dz * dz;
                if (dSqr < bestDistSqr) { bestDistSqr = dSqr; localTopY = _columns[i].y; }
            }
            return localTopY;
        }
    }
}
