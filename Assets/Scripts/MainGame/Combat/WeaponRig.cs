using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Rig canónico de un arma forjada o de pruebas (R1: hijo directo de HandTarget_R).
    /// Convención de ejes canónicos:
    /// Origen (0,0,0) = GripRear (punto de agarre trasero, mano dominante).
    /// +Z = Dirección longitudinal hacia la punta (TipPoint).
    /// +Y = Dirección del elemento de impacto (filo del tajo, cara del martillo, barba del hacha).
    /// +X = Cara plana lateral.
    /// </summary>
    [ExecuteAlways]
    public class WeaponRig : MonoBehaviour
    {
        [Header("Dimensiones Físicas (Metros)")]
        [SerializeField] private float _totalLength = 0.95f;
        [SerializeField] private float _handleLength = 0.25f;
        [SerializeField] private float _headCenterDist = 0.70f;
        [SerializeField] private bool _isTwoHanded = false;

        [Header("Visualización Procedural de Prueba")]
        [SerializeField] private bool _buildProceduralVisual = true;
        [SerializeField] private Color _bladeColor = new Color(0.85f, 0.88f, 0.92f);
        [SerializeField] private Color _edgeGlowColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _hiltColor = new Color(0.35f, 0.22f, 0.12f);

        [Header("Gizmos de Inspección")]
        [SerializeField] private bool _showCanonicalAxes = true;
        [SerializeField] private bool _showSockets = true;

        public float TotalLength => _totalLength;
        public float HandleLength => _handleLength;
        public float HeadCenterDist => _headCenterDist;
        public bool IsTwoHanded => _isTwoHanded;

        public Vector3 GripRearLocal => Vector3.zero;
        public Vector3 GripFrontLocal => new Vector3(0f, 0f, Mathf.Min(0.30f, 0.60f * _handleLength));
        public Vector3 TipPointLocal => new Vector3(0f, 0f, _totalLength);
        public Vector3 HeadCenterLocal => new Vector3(0f, 0f, _headCenterDist);

        public Vector3 GripRearWorld => transform.TransformPoint(GripRearLocal);
        public Vector3 GripFrontWorld => transform.TransformPoint(GripFrontLocal);
        public Vector3 TipPointWorld => transform.TransformPoint(TipPointLocal);
        public Vector3 HeadCenterWorld => transform.TransformPoint(HeadCenterLocal);

        public Vector3 EdgeDirectionWorld => transform.up;
        public Vector3 TipDirectionWorld => transform.forward;
        public Vector3 FlatDirectionWorld => transform.right;

        private void Awake()
        {
            if (_buildProceduralVisual && transform.Find("Visual_ProceduralSword") == null)
            {
                CreateProceduralSwordVisual();
            }
        }

        public void ConfigureDimensions(float totalLen, float handleLen, float headDist, bool twoHanded)
        {
            _totalLength = Mathf.Max(0.2f, totalLen);
            _handleLength = Mathf.Clamp(handleLen, 0.1f, _totalLength * 0.8f);
            _headCenterDist = Mathf.Clamp(headDist, _handleLength, _totalLength);
            _isTwoHanded = twoHanded;
        }

        public void CreateProceduralSwordVisual()
        {
            Transform existing = transform.Find("Visual_ProceduralSword");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            GameObject root = new GameObject("Visual_ProceduralSword");
            root.transform.SetParent(transform, false);

            // Mango (Cilindro / prisma a lo largo de +Z)
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Hilt";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0f, _handleLength * 0.5f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.035f, _handleLength * 0.5f, 0.035f);
            RemoveCollider(handle);
            ApplyColor(handle, _hiltColor);

            // Guarda transversal (Cubo a lo largo de +/-X)
            GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "Crossguard";
            guard.transform.SetParent(root.transform, false);
            guard.transform.localPosition = new Vector3(0f, 0f, _handleLength);
            guard.transform.localScale = new Vector3(0.22f, 0.03f, 0.035f);
            RemoveCollider(guard);
            ApplyColor(guard, Color.gray);

            // Hoja (Cubo estirado en +Z, plano en X, filo en +Y)
            float bladeLen = Mathf.Max(0.1f, _totalLength - _handleLength);
            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(root.transform, false);
            blade.transform.localPosition = new Vector3(0f, 0f, _handleLength + bladeLen * 0.5f);
            blade.transform.localScale = new Vector3(0.02f, 0.07f, bladeLen);
            RemoveCollider(blade);
            ApplyColor(blade, _bladeColor);

            // Indicador de filo (+Y en verde sutil)
            GameObject edgeMark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edgeMark.name = "CuttingEdgeMarker";
            edgeMark.transform.SetParent(root.transform, false);
            edgeMark.transform.localPosition = new Vector3(0f, 0.038f, _handleLength + bladeLen * 0.5f);
            edgeMark.transform.localScale = new Vector3(0.008f, 0.012f, bladeLen * 0.98f);
            RemoveCollider(edgeMark);
            ApplyColor(edgeMark, _edgeGlowColor);
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }

        private static void ApplyColor(GameObject go, Color c)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.color = c;
                r.sharedMaterial = m;
            }
        }

        private void OnDrawGizmos()
        {
            if (!_showCanonicalAxes && !_showSockets) return;

            Vector3 grip = GripRearWorld;
            Vector3 tip = TipPointWorld;
            Vector3 edge = EdgeDirectionWorld;
            Vector3 flat = FlatDirectionWorld;

            if (_showCanonicalAxes)
            {
                // +Z = Azul (Eje longitudinal hacia la punta)
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(grip, tip);

                // +Y = Verde (Filo del arma / cara de impacto)
                Gizmos.color = Color.green;
                Vector3 bladeCenter = (grip + tip) * 0.5f;
                Gizmos.DrawRay(bladeCenter, edge * 0.35f);

                // +X = Rojo (Plano lateral)
                Gizmos.color = Color.red;
                Gizmos.DrawRay(bladeCenter, flat * 0.25f);
            }

            if (_showSockets)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(grip, 0.03f); // GripRear

                if (_isTwoHanded)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(GripFrontWorld, 0.025f); // GripFront
                }

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(tip, 0.025f); // TipPoint

                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawWireSphere(HeadCenterWorld, 0.025f); // HeadCenter
            }
        }
    }
}
