using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using ForjaGame.Audio;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo interactivo del yunque: barra de fuerza oscilante (timing), retícula 3D de impacto
    /// y silueta guía translúcida (blueprint) adaptada a Espada, Hacha, Martillo y Mango ([G]).
    /// </summary>
    public partial class AnvilHammerInput
    {
        private float _forceCursor, _lastTimingFeedbackUntil = -1f, _nextPolishScrapeTime;
        private string _lastTimingFeedback = "";
        private bool _showBlueprint = true, _builtBlueprintHandle;
        private GameObject _reticleRoot, _blueprintRoot;
        private LineRenderer _reticleOuterLR, _reticleInnerLR, _blueprintOutlineLR;
        private MeshFilter _blueprintMeshFilter;
        private MeshRenderer _blueprintMeshRenderer;
        private Material _guideOverlayMat;
        private WeaponType _builtBlueprintType = (WeaponType)(-1);
        private Vector3 _builtBlueprintSize;

        private void UpdateInteractiveGuides(bool stationActive, bool isBusy)
        {
            if (!stationActive || anvilController == null || !anvilController.IsForging)
            {
                SetGuidesVisible(false, false);
                return;
            }
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            {
                _showBlueprint = !_showBlueprint;
                ForgeAudioSystem.PlayUITab();
            }
            _forceCursor = Mathf.PingPong(Time.time * 1.55f, 1.0f);
            bool showReticle = !isBusy && (_toolMode == AnvilToolMode.Hammer || _toolMode == AnvilToolMode.Polish) && !IsMouseOverUI();
            UpdateImpactReticle3D(showReticle);
            UpdateBlueprintSilhouette3D(!isBusy && _showBlueprint);
        }

        private float EvaluateHammerTimingStrike(out float timingQuality)
        {
            if (_forceCursor >= 0.68f && _forceCursor <= 0.88f)
            {
                timingQuality = 1.0f;
                _lastTimingFeedback = "<color=#FFE066><b>[GOLPE MAESTRO x1.32]</b></color>";
                _lastTimingFeedbackUntil = Time.time + 1.2f;
                return 1.32f;
            }
            if (_forceCursor >= 0.38f && _forceCursor <= 0.94f)
            {
                timingQuality = 0.72f;
                _lastTimingFeedback = "<color=#66F09A><b>Golpe Firme (x1.00)</b></color>";
                _lastTimingFeedbackUntil = Time.time + 0.95f;
                return 1.00f;
            }
            timingQuality = 0.35f;
            _lastTimingFeedback = "<color=#F09B59>Golpe Débil (x0.64)</color>";
            _lastTimingFeedbackUntil = Time.time + 0.95f;
            return 0.64f;
        }

        private void DrawForceTimingBarHUD(float hudX, float hudY, float hudW)
        {
            EnsureUIStyles(); EnsureLineTexture();
            float barCardW = 540f, barCardH = 40f, cx = hudX + (hudW - barCardW) * 0.5f, cy = hudY - barCardH - 8f;
            GUI.Box(new Rect(cx, cy, barCardW, barCardH), GUIContent.none, _tabInactiveStyle);
            GUI.Label(new Rect(cx + 12f, cy + 8f, 118f, 24f), "<b><color=#F5E4C3>FUERZA [Click]</color></b>", _tabInactiveStyle);

            Rect meter = new Rect(cx + 132f, cy + 12f, 220f, 16f);
            GUI.color = new Color(0.12f, 0.08f, 0.06f, 0.95f); GUI.DrawTexture(meter, _lineTexture);
            GUI.color = new Color(0.20f, 0.58f, 0.34f, 0.88f); GUI.DrawTexture(new Rect(meter.x + meter.width * 0.38f, meter.y + 2f, meter.width * 0.56f, meter.height - 4f), _lineTexture);
            GUI.color = new Color(0.96f, 0.78f, 0.20f, 0.96f); GUI.DrawTexture(new Rect(meter.x + meter.width * 0.68f, meter.y + 1f, meter.width * 0.20f, meter.height - 2f), _lineTexture);
            GUI.color = Color.white; GUI.DrawTexture(new Rect(meter.x + _forceCursor * (meter.width - 4f), meter.y - 3f, 4f, meter.height + 6f), _lineTexture);

            string rightStatus = Time.time < _lastTimingFeedbackUntil ? _lastTimingFeedback : $"<color=#D8C8AE>[G] Guía: {(_showBlueprint ? "ON" : "OFF")}</color>";
            GUI.Label(new Rect(cx + 360f, cy + 8f, 172f, 24f), rightStatus, _tabInactiveStyle);
        }

        private void EnsureGuideObjects()
        {
            if (_guideOverlayMat == null)
                _guideOverlayMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            Transform ingotT = anvilController != null ? anvilController.IngotTransform : transform;
            if (_reticleRoot == null)
            {
                _reticleRoot = new GameObject("AnvilImpactReticle3D");
                _reticleRoot.transform.SetParent(ingotT, false);
                _reticleOuterLR = CreateRingRenderer(_reticleRoot.transform, "OuterRing", 0.0042f, 36);
                _reticleInnerLR = CreateRingRenderer(_reticleRoot.transform, "InnerRing", 0.0032f, 28);
            }
            else if (_reticleRoot.transform.parent != ingotT) _reticleRoot.transform.SetParent(ingotT, false);

            if (_blueprintRoot == null)
            {
                _blueprintRoot = new GameObject("AnvilBlueprintGuide3D");
                _blueprintRoot.transform.SetParent(ingotT, false);
                _blueprintMeshFilter = _blueprintRoot.AddComponent<MeshFilter>();
                _blueprintMeshRenderer = _blueprintRoot.AddComponent<MeshRenderer>();
                _blueprintMeshRenderer.sharedMaterial = new Material(_guideOverlayMat) { color = new Color(0.32f, 0.86f, 0.98f, 0.20f) };
                _blueprintMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _blueprintMeshRenderer.receiveShadows = false;
                _blueprintOutlineLR = CreateRingRenderer(_blueprintRoot.transform, "BlueprintOutline", 0.0038f, 4);
            }
            else if (_blueprintRoot.transform.parent != ingotT) _blueprintRoot.transform.SetParent(ingotT, false);
        }

        private LineRenderer CreateRingRenderer(Transform parent, string objName, float width, int count)
        {
            var go = new GameObject(objName); go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = count;
            lr.startWidth = width; lr.endWidth = width; lr.sharedMaterial = _guideOverlayMat;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            return lr;
        }

        private void UpdateImpactReticle3D(bool visible)
        {
            if (!visible || !RaycastIngotHorizontalPlane(out Vector3 localPt, out _))
            {
                if (_reticleRoot != null && _reticleRoot.activeSelf) _reticleRoot.SetActive(false);
                return;
            }
            EnsureGuideObjects();
            if (!_reticleRoot.activeSelf) _reticleRoot.SetActive(true);
            float surfY = anvilController.GetSurfaceTopYAt(localPt, out _) + 0.0030f;
            _reticleRoot.transform.localPosition = new Vector3(localPt.x, surfY, localPt.z);
            _reticleRoot.transform.localRotation = Quaternion.identity;

            if (_toolMode == AnvilToolMode.Polish)
            {
                SetCirclePositions(_reticleOuterLR, 0.035f);
                SetLineColor(_reticleOuterLR, new Color(0.45f, 0.92f, 1.0f, 0.85f));
                _reticleInnerLR.gameObject.SetActive(false);
                return;
            }
            _reticleInnerLR.gameObject.SetActive(true);
            anvilController.GetEffectiveStrikeRadii(out float innerR, out float outerR);
            SetCirclePositions(_reticleOuterLR, outerR);
            SetCirclePositions(_reticleInnerLR, innerR);

            float temp = anvilController.ThermalController != null ? anvilController.ThermalController.CurrentTemperature : 750f;
            bool overstruck = anvilController.IsPointOverstruck(localPt);
            bool sweet = _forceCursor >= 0.68f && _forceCursor <= 0.88f;
            Color col = temp < 500f ? new Color(0.32f, 0.72f, 1.0f, 0.90f)
                : (overstruck ? new Color(1.0f, 0.26f, 0.20f, 0.92f) : (sweet ? new Color(1.0f, 0.85f, 0.25f, 0.95f) : new Color(0.38f, 0.95f, 0.56f, 0.88f)));
            SetLineColor(_reticleInnerLR, col);
            SetLineColor(_reticleOuterLR, new Color(col.r, col.g, col.b, col.a * 0.50f));
        }

        private void UpdateBlueprintSilhouette3D(bool visible)
        {
            if (!visible || anvilController == null)
            {
                if (_blueprintRoot != null && _blueprintRoot.activeSelf) _blueprintRoot.SetActive(false);
                return;
            }
            EnsureGuideObjects();
            if (!_blueprintRoot.activeSelf) _blueprintRoot.SetActive(true);
            Vector3 baseSize = anvilController.IngotBaseSize;
            bool isHandle = anvilController.IsCarvingHandle;
            WeaponType wType = anvilController.SelectedWeaponType;
            if (_builtBlueprintType != wType || _builtBlueprintHandle != isHandle || (_builtBlueprintSize - baseSize).sqrMagnitude > 0.0001f)
            {
                RebuildBlueprintGeometry(wType, isHandle, baseSize);
                _builtBlueprintType = wType; _builtBlueprintHandle = isHandle; _builtBlueprintSize = baseSize;
            }
            anvilController.GetSurfaceTopYAt(Vector3.zero, out float maxTopY);
            _blueprintRoot.transform.localPosition = new Vector3(0f, maxTopY + 0.0022f, 0f);
            _blueprintRoot.transform.localRotation = Quaternion.identity;
        }

        private void RebuildBlueprintGeometry(WeaponType wType, bool isHandle, Vector3 baseSize)
        {
            float hx = Mathf.Max(0.18f, baseSize.x * 0.5f), hz = Mathf.Max(0.06f, baseSize.z * 0.5f);
            List<Vector2> pts = BuildSilhouettePolygon(wType, isHandle, hx, hz);
            Vector3[] verts = new Vector3[pts.Count + 1];
            verts[0] = Vector3.zero;
            _blueprintOutlineLR.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p = new Vector3(pts[i].x, 0f, pts[i].y);
                verts[i + 1] = p; _blueprintOutlineLR.SetPosition(i, p);
            }
            SetLineColor(_blueprintOutlineLR, isHandle ? new Color(0.98f, 0.82f, 0.36f, 0.82f) : new Color(0.42f, 0.92f, 1.0f, 0.82f));
            int[] tris = new int[pts.Count * 3];
            for (int i = 0; i < pts.Count; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = ((i + 1) % pts.Count) + 1; tris[i * 3 + 2] = i + 1; }
            Mesh m = new Mesh { name = "Mesh_AnvilBlueprint", vertices = verts, triangles = tris };
            m.RecalculateNormals(); m.RecalculateBounds();
            _blueprintMeshFilter.sharedMesh = m;
        }

        private static List<Vector2> BuildSilhouettePolygon(WeaponType wType, bool isHandle, float hx, float hz)
        {
            if (isHandle)
            {
                float gZ = Mathf.Min(hz * 0.46f, 0.030f), tZ = Mathf.Min(hz * 0.30f, 0.020f);
                return new List<Vector2> { new(-hx * 0.90f, -gZ), new(hx * 0.62f, -gZ), new(hx * 0.66f, -tZ), new(hx * 0.92f, -tZ), new(hx * 0.92f, tZ), new(hx * 0.66f, tZ), new(hx * 0.62f, gZ), new(-hx * 0.90f, gZ) };
            }
            if (wType == WeaponType.Sword)
            {
                float tZ = hz * 0.28f, bZ = hz * 0.88f;
                return new List<Vector2> { new(-hx * 0.96f, -tZ), new(-hx * 0.66f, -tZ), new(-hx * 0.60f, -bZ), new(hx * 0.68f, -bZ * 0.78f), new(hx * 1.02f, 0f), new(hx * 0.68f, bZ * 0.78f), new(-hx * 0.60f, bZ), new(-hx * 0.66f, tZ), new(-hx * 0.96f, tZ) };
            }
            if (wType == WeaponType.Axe)
                return new List<Vector2> { new(-hx * 0.82f, -hz * 0.46f), new(-hx * 0.15f, -hz * 0.42f), new(hx * 0.45f, -hz * 0.82f), new(hx * 0.98f, -hz * 1.08f), new(hx * 1.06f, 0f), new(hx * 0.98f, hz * 1.08f), new(hx * 0.45f, hz * 0.82f), new(-hx * 0.15f, hz * 0.42f), new(-hx * 0.82f, hz * 0.46f) };
            return new List<Vector2> { new(-hx * 0.88f, -hz * 0.62f), new(-hx * 0.18f, -hz * 0.48f), new(hx * 0.22f, -hz * 0.48f), new(hx * 0.88f, -hz * 0.82f), new(hx * 0.96f, 0f), new(hx * 0.88f, hz * 0.82f), new(hx * 0.22f, hz * 0.48f), new(-hx * 0.18f, hz * 0.48f), new(-hx * 0.88f, hz * 0.62f) };
        }

        private static void SetCirclePositions(LineRenderer lr, float radius)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++) { float a = (i / (float)n) * Mathf.PI * 2f; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius)); }
        }

        private static void SetLineColor(LineRenderer lr, Color c) { lr.startColor = c; lr.endColor = c; }
        private void SetGuidesVisible(bool reticle, bool blueprint)
        {
            if (_reticleRoot != null && _reticleRoot.activeSelf != reticle) _reticleRoot.SetActive(reticle);
            if (_blueprintRoot != null && _blueprintRoot.activeSelf != blueprint) _blueprintRoot.SetActive(blueprint);
        }
    }
}
