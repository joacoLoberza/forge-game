using UnityEngine;

namespace ForjaGame.Combat
{
    public enum HitboxShape
    {
        Box,
        Capsule
    }

    /// <summary>
    /// Componente de renderizado en tiempo de ejecución para hitboxes y hurtboxes.
    /// Crea y escala una malla translúcida (Rojo = Inactivo/Libre, Negro = Conectando a objetivo)
    /// visible únicamente cuando HitboxDebugManager.ShowHitboxes (F3) está activo.
    /// </summary>
    public class HitboxVisualizer : MonoBehaviour
    {
        [SerializeField] private HitboxShape shape = HitboxShape.Box;

        private GameObject _visualObj;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private bool _isConnecting = false;
        private bool _isVolumeActive = true;

        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                if (_isConnecting != value)
                {
                    _isConnecting = value;
                    UpdateMaterial();
                }
            }
        }

        public bool IsVolumeActive
        {
            get => _isVolumeActive;
            set
            {
                _isVolumeActive = value;
                UpdateVisibility();
            }
        }

        private void Awake()
        {
            EnsureVisualObject();
        }

        private void OnEnable()
        {
            HitboxDebugManager.OnHitboxVisibilityChanged += HandleVisibilityChanged;
            UpdateVisibility();
        }

        private void OnDisable()
        {
            HitboxDebugManager.OnHitboxVisibilityChanged -= HandleVisibilityChanged;
            if (_visualObj != null) _visualObj.SetActive(false);
        }

        private void OnDestroy()
        {
            HitboxDebugManager.OnHitboxVisibilityChanged -= HandleVisibilityChanged;
            if (_visualObj != null) Destroy(_visualObj);
        }

        private void HandleVisibilityChanged(bool show)
        {
            UpdateVisibility();
        }

        private void EnsureVisualObject()
        {
            if (_visualObj != null) return;

            _visualObj = new GameObject($"[Visual_{name}]");
            _visualObj.transform.SetParent(transform, false);

            _meshFilter = _visualObj.AddComponent<MeshFilter>();
            _meshRenderer = _visualObj.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            // Obtener malla compartida sin instanciar colisionadores
            GameObject tempPrimitive = GameObject.CreatePrimitive(shape == HitboxShape.Capsule ? PrimitiveType.Capsule : PrimitiveType.Cube);
            _meshFilter.sharedMesh = tempPrimitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempPrimitive);

            UpdateMaterial();
            UpdateVisibility();
        }

        public void SetShape(HitboxShape newShape)
        {
            if (shape == newShape && _meshFilter?.sharedMesh != null) return;
            shape = newShape;

            EnsureVisualObject();
            GameObject temp = GameObject.CreatePrimitive(shape == HitboxShape.Capsule ? PrimitiveType.Capsule : PrimitiveType.Cube);
            _meshFilter.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        public void UpdateBox(Vector3 localCenter, Vector3 size)
        {
            EnsureVisualObject();
            SetShape(HitboxShape.Box);
            _visualObj.transform.localPosition = localCenter;
            _visualObj.transform.localRotation = Quaternion.identity;
            _visualObj.transform.localScale = size;
        }

        public void UpdateBoxWorld(Vector3 worldCenter, Vector3 size, Quaternion worldRot)
        {
            EnsureVisualObject();
            SetShape(HitboxShape.Box);
            _visualObj.transform.position = worldCenter;
            _visualObj.transform.rotation = worldRot;
            _visualObj.transform.localScale = size;
        }

        public void UpdateCapsule(Vector3 localCenter, float radius, float height)
        {
            EnsureVisualObject();
            SetShape(HitboxShape.Capsule);
            _visualObj.transform.localPosition = localCenter;
            _visualObj.transform.localRotation = Quaternion.identity;
            _visualObj.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        }

        private void UpdateMaterial()
        {
            if (_meshRenderer == null) return;
            var mgr = HitboxDebugManager.Instance;
            if (mgr == null) return;

            _meshRenderer.sharedMaterial = _isConnecting ? mgr.BlackMaterial : mgr.RedMaterial;
        }

        private void UpdateVisibility()
        {
            if (_visualObj == null) return;
            bool shouldShow = HitboxDebugManager.ShowHitboxes && _isVolumeActive;
            _visualObj.SetActive(shouldShow);
            if (shouldShow) UpdateMaterial();
        }

        private void OnDrawGizmos()
        {
            if (!HitboxDebugManager.ShowHitboxes || !_isVolumeActive) return;

            Gizmos.color = _isConnecting ? new Color(0f, 0f, 0f, 0.75f) : new Color(1f, 0f, 0f, 0.40f);
            if (_visualObj != null)
            {
                Gizmos.matrix = _visualObj.transform.localToWorldMatrix;
                if (shape == HitboxShape.Box)
                    Gizmos.DrawCube(Vector3.zero, Vector3.one);
                else
                    Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            }
        }
    }
}
