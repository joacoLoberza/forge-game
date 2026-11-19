using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Controlador principal del yunque: gestiona el lingote en mesa, estado de forja,
    /// propiedades metalúrgicas y templado.
    /// La lógica de deformación de malla por martillo reside en AnvilController.Deformation.cs.
    /// </summary>
    public partial class AnvilController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float minLayerY = 0.003f;
        [SerializeField] private float hitSpreadForce = 0.040f;
        [SerializeField] private float hitInnerRadius = 0.10f;
        [SerializeField] private float hitOuterRadius = 0.22f;

        [SerializeField] private MeshFilter activeIngotMeshFilter;
        [SerializeField] private MeshCollider activeIngotMeshCollider;
        [SerializeField] private MeshRenderer activeIngotMeshRenderer;
        [SerializeField] private int currentHits;
        [SerializeField] private bool isForging, isQuenched;
        [SerializeField] private float forgingQuality = 1f;

        private IngotData _currentIngot;
        private WeaponType _selectedWeaponType;
        private Mesh _deformableMesh, _forgedBladeMesh;
        private Material _forgedBladeMaterial;
        private Vector3[] _columns, _meshVertices;
        private int[] _perimIndices;
        private int _cols = 17, _rows = 11;
        private float _initialLayerY, _layerStep;
        private readonly List<int> _frontEdge = new(), _backEdge = new(), _rightEdge = new(), _leftEdge = new();
        private int _sharpEdgeCount, _requiredEdgeCount = 1;
        private bool _isEdgeComplete;
        private Vector3 _currentBaseSize;
        private AnvilThermalController _thermalController;
        private AnvilQuenchDifferential _quenchDifferential;
        private Color _currentMetalBaseColor = new Color(0.48f, 0.52f, 0.58f);
        private float _remainingMassRatio = 1.0f;
        private int _cutsPerformed = 0;
        public static AnvilController Instance { get; private set; }

        public bool IsForging => isForging; public bool HasIngotOnAnvil => _currentIngot != null;
        public bool HasMeshBeenModified => currentHits > 0 || _cutsPerformed > 0;
        public bool CanRetrieveIngot => _currentIngot != null && !isQuenched && !HasMeshBeenModified; public int CurrentHits => currentHits;
        public float ForgingQuality => forgingQuality; public bool IsQuenched => isQuenched;
        public bool IsEdgeComplete => _isEdgeComplete; public int SharpEdgeCount => _sharpEdgeCount;
        public float FinalCombinedQuality => _currentIngot != null ? _currentIngot.quality * forgingQuality : forgingQuality;
        public int RequiredEdgeCount => _requiredEdgeCount;
        public float EdgeProgress => _selectedWeaponType == WeaponType.Hammer ? (_isEdgeComplete ? 1f : Mathf.Clamp01(currentHits / 3f)) : (_requiredEdgeCount > 0 ? Mathf.Clamp01((float)_sharpEdgeCount / _requiredEdgeCount) : 0f);
        public IngotData CurrentIngot => _currentIngot; public Mesh ForgedBladeMesh => _forgedBladeMesh;
        public Material ForgedBladeMaterial => _forgedBladeMaterial;
        public Transform IngotTransform => activeIngotMeshFilter != null ? activeIngotMeshFilter.transform : transform;
        public MeshRenderer IngotRenderer => activeIngotMeshRenderer; public MeshCollider ActiveIngotCollider => activeIngotMeshCollider;
        public Color CurrentMetalBaseColor => _currentMetalBaseColor; public AnvilThermalController ThermalController => _thermalController;
        public AnvilQuenchDifferential QuenchDifferential => _quenchDifferential; public Vector3 IngotBaseSize => _currentBaseSize;
        public float RemainingMassRatio => _remainingMassRatio;
        public bool WasLastHitDulling { get; private set; }
        public bool IsIngotRuinedTooSmall { get; private set; }
        public string LastCutBlockedMessage { get; private set; }
        public Vector3 WorkpieceCenterLocal => _deformableMesh != null ? _deformableMesh.bounds.center : Vector3.zero;
        public Vector2 KeepPoint2D => IsCarvingHandle ? new Vector2(WorkpieceCenterLocal.x, WorkpieceCenterLocal.z) : new Vector2(WorkpieceCenterLocal.x, WorkpieceCenterLocal.z);
        public bool GetCutPreview(List<Vector3> pts, bool flip, out List<Vector3> tris) => AnvilMeshCutter.GetCutPreviewTriangles(_deformableMesh, _columns, _perimIndices, pts, KeepPoint2D, flip, out tris);

        public event Action<float> EdgeProgressChanged;
        public event Action ReadyForQuenching, ForgingCompleted;

        private void Awake() { Instance = this; EnsureIngotReferences(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void EnsureIngotReferences()
        {
            if (activeIngotMeshFilter == null) activeIngotMeshFilter = GetComponentInChildren<MeshFilter>();
            if (activeIngotMeshFilter == null) return;
            if (activeIngotMeshRenderer == null) activeIngotMeshRenderer = activeIngotMeshFilter.GetComponent<MeshRenderer>();
            if (activeIngotMeshCollider == null)
            {
                if (activeIngotMeshFilter.TryGetComponent<BoxCollider>(out var oldBox)) ForjaGame.Core.UnitySafeUtility.DestroySafe(oldBox);
                activeIngotMeshCollider = activeIngotMeshFilter.gameObject.AddComponent<MeshCollider>();
            }
            activeIngotMeshCollider.convex = true;
            if (_thermalController == null && !TryGetComponent(out _thermalController)) _thermalController = gameObject.AddComponent<AnvilThermalController>();
            _thermalController?.Initialize(this);
            if (_quenchDifferential == null && !TryGetComponent(out _quenchDifferential)) _quenchDifferential = gameObject.AddComponent<AnvilQuenchDifferential>();
            _quenchDifferential?.Initialize(this);
        }

        public bool PlaceIngot(IngotData ingot, WeaponType weaponType, MeshFilter meshFilter = null)
        {
            if (ingot == null) return false;
            IsCarvingHandle = false; IsIngotRuinedTooSmall = false;
            CarvedHandleMesh = null; _forgedBladeMesh = null; _forgedBladeMaterial = null;
            EnsureIngotReferences();
            _thermalController?.ResetThermal(25f);
            if (meshFilter != null) activeIngotMeshFilter = meshFilter;
            if (activeIngotMeshFilter == null) return false;

            _currentIngot = ingot; _selectedWeaponType = weaponType;
            forgingQuality = 1.0f; isForging = true; _polishShineProgress = 0f;
            isQuenched = false; _isEdgeComplete = false; WasLastHitDulling = false;
            currentHits = 0; _cutsPerformed = 0;

            _cols = _selectedWeaponType == WeaponType.Sword ? 29 : 17;
            _rows = _selectedWeaponType == WeaponType.Sword ? 9 : 11;

            activeIngotMeshFilter.transform.localScale = Vector3.one;
            Vector3 baseSize = _selectedWeaponType == WeaponType.Sword
                ? (ingot.size == ItemSize.Small ? new Vector3(0.72f, 0.08f, 0.175f)
                    : ingot.size == ItemSize.Large ? new Vector3(1.45f, 0.095f, 0.200f) : new Vector3(1.02f, 0.09f, 0.190f))
                : (ingot.size == ItemSize.Small ? new Vector3(0.42f, 0.12f, 0.20f)
                    : ingot.size == ItemSize.Large ? new Vector3(0.62f, 0.16f, 0.28f) : new Vector3(0.52f, 0.14f, 0.24f));
            _currentBaseSize = baseSize;

            _initialLayerY = baseSize.y * 0.5f;
            minLayerY = _selectedWeaponType == WeaponType.Hammer ? (_initialLayerY * 0.50f) : 0.003f;
            _layerStep = Mathf.Max(0.008f, (_initialLayerY - minLayerY) / 5.0f);
            _remainingMassRatio = 1.0f;

            _deformableMesh = SubdividedIngotMeshGenerator.CreateSubdividedBillet(_cols, _rows, baseSize, out _perimIndices, out _columns);
            activeIngotMeshFilter.mesh = _deformableMesh;
            _meshVertices = _deformableMesh.vertices;

            _frontEdge.Clear(); _backEdge.Clear(); _rightEdge.Clear(); _leftEdge.Clear();
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                {
                    int k = r * _cols + c;
                    if (r >= _rows - 2) _frontEdge.Add(k); if (r <= 1) _backEdge.Add(k);
                    if (c >= _cols - 2) _rightEdge.Add(k); if (c <= 1) _leftEdge.Add(k);
                }

            int maxP = Mathf.Max(_frontEdge.Count, Mathf.Max(_backEdge.Count, Mathf.Max(_rightEdge.Count, _leftEdge.Count)));
            _requiredEdgeCount = _selectedWeaponType == WeaponType.Sword ? 14 : Mathf.Max(3, Mathf.CeilToInt(maxP * 0.50f));
            _sharpEdgeCount = 0;

            RefreshCollider();
            ApplyIngotMetalColor(_currentIngot);
            activeIngotMeshFilter.gameObject.SetActive(true);
            EdgeProgressChanged?.Invoke(0f);
            return true;
        }

        public void ResumeForging() => isForging = IsCarvingHandle || (_currentIngot != null && !isQuenched);
        public void PauseForgingAndLeaveOnTable() => isForging = false;

        public bool Quench()
        {
            if (!_isEdgeComplete || isQuenched) return false;
            isQuenched = true; isForging = false;
            _thermalController?.ResetThermal(25f);
            ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzTempladoAgua();
            if (_currentIngot != null) _currentIngot.quality = FinalCombinedQuality;
            _forgedBladeMesh = Instantiate(_deformableMesh);
            if (_forgedBladeMesh != null) _forgedBladeMesh.name = $"Blade_{_selectedWeaponType}_{_currentIngot?.id}";
            if (activeIngotMeshRenderer != null)
            {
                _forgedBladeMaterial = new Material(activeIngotMeshRenderer.material);
                _forgedBladeMaterial.DisableKeyword("_NORMALMAP"); _forgedBladeMaterial.mainTexture = null;
                foreach (var p in new[] { "_BaseMap", "_MainTex", "_EmissionMap", "_BumpMap" }) if (_forgedBladeMaterial.HasProperty(p)) _forgedBladeMaterial.SetTexture(p, null);
                Color qCol = Color.Lerp(_currentMetalBaseColor, new Color(0.20f, 0.18f, 0.18f), Mathf.Lerp(0.18f, 0.04f, _polishShineProgress));
                qCol = Color.Lerp(qCol, Color.white, _polishShineProgress * 0.12f);
                _forgedBladeMaterial.color = qCol;
                if (_forgedBladeMaterial.HasProperty("_BaseColor")) _forgedBladeMaterial.SetColor("_BaseColor", qCol);
                if (_forgedBladeMaterial.HasProperty("_EmissionColor")) _forgedBladeMaterial.SetColor("_EmissionColor", Color.black);
                if (_forgedBladeMaterial.HasProperty("_Metallic")) _forgedBladeMaterial.SetFloat("_Metallic", Mathf.Lerp(0.88f, 0.97f, _polishShineProgress));
                if (_forgedBladeMaterial.HasProperty("_Smoothness")) _forgedBladeMaterial.SetFloat("_Smoothness", Mathf.Lerp(0.72f, 0.96f, _polishShineProgress));
            }
            ForgingCompleted?.Invoke(); return true;
        }

        public void ClearAnvilSurface()
        {
            isForging = false; IsCarvingHandle = false; IsIngotRuinedTooSmall = false; CarvedHandleMesh = null;
            _forgedBladeMesh = null; _forgedBladeMaterial = null; _cutsPerformed = 0;
            if (activeIngotMeshFilter != null && activeIngotMeshFilter.gameObject != gameObject)
                activeIngotMeshFilter.gameObject.SetActive(false);
            _currentIngot = null;
        }

        public IngotData CancelAndRetrieveIngot()
        {
            if (!CanRetrieveIngot) return null;
            IngotData r = _currentIngot; ClearAnvilSurface(); return r;
        }

        public bool ApplyCut(List<Vector3> cutPointsLocal, bool? discardInside = null)
        {
            LastCutBlockedMessage = string.Empty;
            if (!isForging || isQuenched || IsIngotRuinedTooSmall || cutPointsLocal == null || cutPointsLocal.Count == 0) return false;
            bool inTutorial = ForjaGame.UI.ForgeTutorialDirector.Instance != null && ForjaGame.UI.ForgeTutorialDirector.Instance.IsTutorialActive;
            int prevCols = _columns != null ? _columns.Length : 0;
            if (!AnvilMeshCutter.CutBilletMesh(_deformableMesh, _columns, _perimIndices, cutPointsLocal,
                out Vector3[] newColumns, out int[] newPerimIndices, out Mesh newMesh,
                out Mesh offcutMesh, _currentBaseSize, discardInside, KeepPoint2D, IsCarvingHandle) || newMesh == null)
            {
                if (!inTutorial && !IsCarvingHandle && GetCutPreview(cutPointsLocal, discardInside ?? false, out var disc) && disc != null && disc.Count >= 3)
                {
                    _cutsPerformed++; IsIngotRuinedTooSmall = true;
                    if (activeIngotMeshFilter != null) activeIngotMeshFilter.mesh = null;
                    return true;
                }
                LastCutBlockedMessage = "Cannot cut empty side - press [F] to flip side or adjust points.";
                return false;
            }
            if (inTutorial && !IsCarvingHandle && (newColumns == null || newColumns.Length < Mathf.Max(14, prevCols / 3)))
            { Destroy(newMesh); if (offcutMesh != null) Destroy(offcutMesh); LastCutBlockedMessage = "Tutorial: remaining blade would be too small - press [F] to flip!"; return false; }
            if (IsCarvingHandle && (newColumns == null || newColumns.Length < 6))
            { Destroy(newMesh); if (offcutMesh != null) Destroy(offcutMesh); LastCutBlockedMessage = "Remaining handle piece would be too small - press [F] to flip!"; return false; }
            _cutsPerformed++;
            if (offcutMesh != null && activeIngotMeshRenderer != null)
            {
                Vector3 outw = offcutMesh.bounds.center - newMesh.bounds.center; outw.y = 0f;
                bool isSurr = offcutMesh.bounds.Contains(newMesh.bounds.center) || outw.sqrMagnitude < 0.002f ||
                    (offcutMesh.bounds.min.x <= newMesh.bounds.min.x && offcutMesh.bounds.max.x >= newMesh.bounds.max.x &&
                     offcutMesh.bounds.min.z <= newMesh.bounds.min.z && offcutMesh.bounds.max.z >= newMesh.bounds.max.z);
                AnvilOffcutSpawner.SpawnOffcut(offcutMesh, IngotTransform, activeIngotMeshRenderer.material, isSurr ? Vector3.up : (outw.sqrMagnitude > 0.0001f ? outw.normalized : Vector3.right));
            }
            if (!IsCarvingHandle && prevCols > 0 && newColumns != null)
                _remainingMassRatio = Mathf.Clamp(_remainingMassRatio * ((float)newColumns.Length / prevCols), 0.05f, 1.0f);
            _deformableMesh = newMesh; _columns = newColumns; _perimIndices = newPerimIndices; _meshVertices = _deformableMesh.vertices;
            if (activeIngotMeshFilter != null) activeIngotMeshFilter.mesh = _deformableMesh;
            if (!inTutorial && !IsCarvingHandle && ComputeCurrentTopArea2D() < GetMinimumAllowedArea2D()) IsIngotRuinedTooSmall = true;
            RebuildEdgeIndices(); RefreshCollider(); EvaluateEdgeCompletion();
            return true;
        }

        private void RebuildEdgeIndices()
        {
            _frontEdge.Clear(); _backEdge.Clear(); _rightEdge.Clear(); _leftEdge.Clear();
            if (_perimIndices == null || _columns == null) return;
            Vector3 center = _deformableMesh != null ? _deformableMesh.bounds.center : Vector3.zero;
            for (int p = 0; p < _perimIndices.Length; p++)
            {
                int k = _perimIndices[p];
                Vector3 c = _columns[k];
                if (c.z > center.z + 0.025f) _frontEdge.Add(k); if (c.z < center.z - 0.025f) _backEdge.Add(k);
                if (c.x > center.x + 0.025f) _rightEdge.Add(k); if (c.x < center.x - 0.025f) _leftEdge.Add(k);
            }
            int maxP = Mathf.Max(_frontEdge.Count, _backEdge.Count);
            _requiredEdgeCount = _selectedWeaponType == WeaponType.Sword ? Mathf.Max(7, Mathf.CeilToInt(maxP * 0.50f)) : 2;
        }
    }
}
