using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de deformación física para AnvilController:
    /// Ejecuta el aplanado de capas por martillo, expansión radial controlada,
    /// garantía de monotonicidad de grilla, suavizado de pendientes y sincronización de malla.
    /// </summary>
    public partial class AnvilController
    {
        private void RefreshCollider()
        {
            if (activeIngotMeshCollider != null && _deformableMesh != null)
            { activeIngotMeshCollider.sharedMesh = null; activeIngotMeshCollider.sharedMesh = _deformableMesh; }
        }

        public bool IsWorldPointOnIngot(Vector3 worldPoint)
        {
            if (activeIngotMeshFilter == null || _columns == null || _columns.Length == 0) return false;
            Vector3 lp = activeIngotMeshFilter.transform.InverseTransformPoint(worldPoint);
            float maxR = (hitInnerRadius * 1.15f) * (hitInnerRadius * 1.15f);
            for (int k = 0; k < _columns.Length; k++)
            { float dx = _columns[k].x - lp.x, dz = _columns[k].z - lp.z; if (dx * dx + dz * dz <= maxR) return true; }
            return false;
        }

        public bool StrikeWithHammer(RaycastHit hit, float forceMultiplier = 1f) => StrikeWithHammer(hit.point, forceMultiplier);
        public bool StrikeWithHammer(Vector3 worldPoint, float forceMultiplier = 1f)
        {
            if (!isForging || isQuenched || IsIngotRuinedTooSmall || !IsWorldPointOnIngot(worldPoint)) return false;
            currentHits++; DeformMeshAndFlattenLayer(worldPoint, Mathf.Clamp(forceMultiplier, 0.45f, 1.45f));
            EvaluateEdgeCompletion(); return true;
        }

        private void DeformMeshAndFlattenLayer(Vector3 worldPoint, float forceMultiplier = 1f)
        {
            if (activeIngotMeshFilter == null || _deformableMesh == null || _columns == null) return;
            Vector3 localPoint = activeIngotMeshFilter.transform.InverseTransformPoint(worldPoint);

            bool isSword = _selectedWeaponType == WeaponType.Sword;
            float scale = isSword ? 0.65f : ((_currentBaseSize.x > 0.05f ? _currentBaseSize.x : 0.85f) / 0.85f);
            float effSpreadForce = hitSpreadForce * (isSword ? 0.55f : scale) * forceMultiplier;
            float effInnerRadius = hitInnerRadius * scale;
            float effOuterRadius = hitOuterRadius * scale;
            float minMono = 0.012f * scale;

            bool isOverstruck = false;
            float overstrikeRadiusSqr = (0.07f * scale) * (0.07f * scale);
            float minSharpThreshold = minLayerY + 0.0025f;
            for (int k = 0; k < _columns.Length; k++)
            {
                float dx = _columns[k].x - localPoint.x, dz = _columns[k].z - localPoint.z;
                if (dx * dx + dz * dz <= overstrikeRadiusSqr && _columns[k].y <= minSharpThreshold) { isOverstruck = true; break; }
            }
            WasLastHitDulling = isOverstruck;

            float thermalMalleability = (ThermalController != null ? ThermalController.GetMalleabilityMultiplier() : 1.0f) * forceMultiplier;
            if (!isOverstruck && forceMultiplier > 1.15f && thermalMalleability >= 0.7f)
                forgingQuality = Mathf.Min(1.0f, forgingQuality + 0.012f);

            float hitOuterRadiusSqr = effOuterRadius * effOuterRadius;
            float radiusDiffInv = 1.0f / (effOuterRadius - effInnerRadius);
            for (int k = 0; k < _columns.Length; k++)
            {
                float dx = _columns[k].x - localPoint.x, dz = _columns[k].z - localPoint.z;
                float distSqr = dx * dx + dz * dz;
                if (distSqr > hitOuterRadiusSqr) continue;

                float dist = Mathf.Sqrt(distSqr);
                float falloff = dist <= effInnerRadius ? 1.0f : Mathf.SmoothStep(1.0f, 0.0f, (dist - effInnerRadius) * radiusDiffInv);

                // Solo modificar si la columna aún puede adelgazar
                if (_columns[k].y > minLayerY)
                {
                    _columns[k].y = Mathf.Max(minLayerY, _columns[k].y - (_layerStep * falloff * thermalMalleability));

                    // Solo expandir si el golpe no cayó en capa mínima
                    if (!isOverstruck)
                    {
                        Vector2 outward = new Vector2(_columns[k].x, _columns[k].z);
                        Vector2 pushDir = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector2.up;
                        _columns[k].x += pushDir.x * (effSpreadForce * falloff * thermalMalleability);
                        _columns[k].z += pushDir.y * (effSpreadForce * falloff * thermalMalleability);
                    }
                }
            }

            // Monotonicidad y suavizado de pendiente si la malla conserva la cuadrícula original
            bool isRegularGrid = (_columns.Length == _rows * _cols);
            if (isRegularGrid)
            {
                for (int r = 0; r < _rows; r++)
                    for (int c = 0; c < _cols - 1; c++)
                    {
                        int idx = r * _cols + c;
                        if (_columns[idx + 1].x < _columns[idx].x + minMono) _columns[idx + 1].x = _columns[idx].x + minMono;
                    }

                for (int c = 0; c < _cols; c++)
                    for (int r = 0; r < _rows - 1; r++)
                    {
                        int idx = r * _cols + c, nextRowIdx = idx + _cols;
                        if (_columns[nextRowIdx].z < _columns[idx].z + minMono) _columns[nextRowIdx].z = _columns[idx].z + minMono;
                    }

                float maxSlope = _layerStep * 1.5f;
                for (int r = 0; r < _rows; r++)
                    for (int c = 0; c < _cols; c++)
                    {
                        int idx = r * _cols + c;
                        if (c < _cols - 1)
                        {
                            int right = idx + 1;
                            if (_columns[right].y > _columns[idx].y + maxSlope) _columns[right].y = _columns[idx].y + maxSlope;
                            else if (_columns[idx].y > _columns[right].y + maxSlope) _columns[idx].y = _columns[right].y + maxSlope;
                        }
                        if (r < _rows - 1)
                        {
                            int up = idx + _cols;
                            if (_columns[up].y > _columns[idx].y + maxSlope) _columns[up].y = _columns[idx].y + maxSlope;
                            else if (_columns[idx].y > _columns[up].y + maxSlope) _columns[idx].y = _columns[up].y + maxSlope;
                        }
                        _columns[idx].y = Mathf.Clamp(_columns[idx].y, minLayerY, _initialLayerY);
                    }
            }
            else
            {
                for (int k = 0; k < _columns.Length; k++) _columns[k].y = Mathf.Clamp(_columns[k].y, minLayerY, _initialLayerY);
            }

            // Sincronizar vértices de la malla en tiempo real (superior, inferior y faldón perimetral)
            int M = _columns.Length, P = _perimIndices.Length;
            for (int k = 0; k < M; k++) { Vector3 col = _columns[k]; _meshVertices[k] = col; _meshVertices[M + k] = new Vector3(col.x, -col.y, col.z); }
            for (int p = 0; p < P; p++) { int k = _perimIndices[p]; Vector3 col = _columns[k]; _meshVertices[2 * M + p] = col; _meshVertices[2 * M + P + p] = new Vector3(col.x, -col.y, col.z); }
            _deformableMesh.vertices = _meshVertices; _deformableMesh.RecalculateNormals(); _deformableMesh.RecalculateBounds();
            RefreshCollider();
        }

        private void EvaluateEdgeCompletion()
        {
            _sharpEdgeCount = Mathf.Max(SharpFrontCount, Mathf.Max(SharpBackCount, Mathf.Max(SharpRightCount, SharpLeftCount)));
            _isEdgeComplete = _selectedWeaponType == WeaponType.Hammer ? (_sharpEdgeCount >= 2 || currentHits >= 3) : (_sharpEdgeCount >= _requiredEdgeCount);
            EdgeProgressChanged?.Invoke(EdgeProgress);
            if (_isEdgeComplete)
                ReadyForQuenching?.Invoke();
        }

        public int CountSharpInDirection(Vector3 dir)
        {
            if (_columns == null || _columns.Length == 0) return 0;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i].x < minX) minX = _columns[i].x; if (_columns[i].x > maxX) maxX = _columns[i].x;
                if (_columns[i].z < minZ) minZ = _columns[i].z; if (_columns[i].z > maxZ) maxZ = _columns[i].z;
            }
            float spanX = Mathf.Max(0.04f, maxX - minX), spanZ = Mathf.Max(0.04f, maxZ - minZ), th = minLayerY + 0.005f;
            int count = 0;
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i].y <= th)
                {
                    if (dir == Vector3.forward && _columns[i].z >= maxZ - spanZ * 0.35f) count++;
                    else if (dir == Vector3.back && _columns[i].z <= minZ + spanZ * 0.35f) count++;
                    else if (dir == Vector3.right && _columns[i].x >= maxX - spanX * 0.35f) count++;
                    else if (dir == Vector3.left && _columns[i].x <= minX + spanX * 0.35f) count++;
                }
            }
            return count;
        }

        public int SharpFrontCount => CountSharpInDirection(Vector3.forward);
        public int SharpBackCount => CountSharpInDirection(Vector3.back);
        public int SharpRightCount => CountSharpInDirection(Vector3.right);
        public int SharpLeftCount => CountSharpInDirection(Vector3.left);
        public void ReduceQualityFromOverheat(float amount) => forgingQuality = Mathf.Max(0.15f, forgingQuality - amount);

        public float GetMinimumAllowedArea2D() =>
            (_selectedWeaponType == WeaponType.Sword ? (0.72f * 0.175f) : (0.42f * 0.20f)) * 0.10f;

        public float ComputeCurrentTopArea2D()
        {
            if (_deformableMesh == null || _columns == null) return 0f;
            int M = _columns.Length; int[] tris = _deformableMesh.triangles; float area = 0f;
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                if (a < M && b < M && c < M)
                {
                    Vector3 va = _columns[a], vb = _columns[b], vc = _columns[c];
                    area += 0.5f * Mathf.Abs((vb.x - va.x) * (vc.z - va.z) - (vc.x - va.x) * (vb.z - va.z));
                }
            }
            return area;
        }

        public List<Vector2> GetPerimeter2D()
        {
            if (_columns == null || _perimIndices == null) return null;
            var poly = new List<Vector2>(_perimIndices.Length);
            for (int i = 0; i < _perimIndices.Length; i++) { int k = _perimIndices[i]; if (k >= 0 && k < _columns.Length) poly.Add(new Vector2(_columns[k].x, _columns[k].z)); }
            return poly;
        }

        public void ApplyIngotMetalColor(IngotData ingot)
        {
            if (activeIngotMeshRenderer == null) return;
            Color metalColor = new Color(0.82f, 0.86f, 0.92f);
            if (ingot?.composition != null)
            {
                if (ingot.composition.ingredients != null && ingot.composition.ingredients.Count > 0)
                {
                    float r = 0f, g = 0f, b = 0f, total = 0f;
                    for (int i = 0; i < ingot.composition.ingredients.Count; i++)
                    {
                        var ing = ingot.composition.ingredients[i]; Color c = MetalProperties.GetMetalColor(ing.metal);
                        r += c.r * ing.percentage; g += c.g * ing.percentage; b += c.b * ing.percentage; total += ing.percentage;
                    }
                    if (total > 0f) metalColor = new Color(r / total, g / total, b / total, 1f);
                }
                else
                {
                    metalColor = MetalProperties.GetMetalColor(ingot.composition.CalculateWeightedProperties().metalType); metalColor.a = 1f;
                }
            }
            _currentMetalBaseColor = metalColor;
            Material mat = activeIngotMeshRenderer.material;
            mat.DisableKeyword("_NORMALMAP"); mat.EnableKeyword("_EMISSION");
            mat.mainTexture = null; mat.color = metalColor;
            foreach (var p in new[] { "_MainTex", "_BumpMap" }) if (mat.HasProperty(p)) mat.SetTexture(p, null);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", metalColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.85f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
            _thermalController?.ResetThermalTextureCache();
            _thermalController?.UpdateVisualColors();
        }
    }
}
