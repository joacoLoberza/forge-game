using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador de la Etapa E1: Sandbox sin humanoide.
    /// Valida que el filo (+Y) lidere la velocidad de la punta a lo largo de todo el golpe
    /// mediante HandTargets y WeaponRig con orientación horneada determinista.
    /// </summary>
    [ExecuteAlways]
    public partial class CombatSandboxE1 : MonoBehaviour
    {
        [Header("Estructura Sandbox")]
        [SerializeField] private Transform _bodySpace;
        [SerializeField] private Transform _shoulderRight;
        [SerializeField] private Transform _handTargetR;
        [SerializeField] private WeaponRig _weaponRig;

        [Header("Parámetros Físicos")]
        [SerializeField] private float _armLength = 0.72f;
        [SerializeField] private float _weaponLength = 0.95f;
        [SerializeField] private float _baseDuration = 0.65f;

        [Header("Control de Reproducción")]
        [SerializeField] private bool _isPlaying = true;
        [SerializeField] private bool _loop = true;
        [SerializeField, Range(0.1f, 2.0f)] private float _playbackSpeed = 1.0f;
        [SerializeField, Range(0f, 1f)] private float _normalizedTime = 0f;

        [Header("Gizmos y Visualización")]
        [SerializeField] private bool _showTrajectory = true;
        [SerializeField] private bool _showReachSpheres = true;
        [SerializeField] private bool _showEdgeNormals = true;

        private BakedAttackData _bakedData;
        private AttackKeyframe[] _keyframes;

        public float NormalizedTime { get => _normalizedTime; set => _normalizedTime = Mathf.Clamp01(value); }
        public bool IsPlaying { get => _isPlaying; set => _isPlaying = value; }
        public BakedAttackData BakedData => _bakedData;

        private void Awake()
        {
            SetupDefaultHierarchy();
            InitKeyframes();
            BakeMotion();
        }

        private void OnEnable()
        {
            if (_bakedData == null)
            {
                InitKeyframes();
                BakeMotion();
            }
        }

        private void OnValidate()
        {
            InitKeyframes();
            BakeMotion();
        }

        private void Update()
        {
            if (_bakedData == null || _bakedData.FrameCount == 0) return;

            if (Application.isPlaying && _isPlaying)
            {
                float dt = Time.deltaTime * _playbackSpeed;
                _normalizedTime += dt / Mathf.Max(0.1f, _baseDuration);
                if (_normalizedTime > 1f)
                {
                    if (_loop) _normalizedTime %= 1f;
                    else
                    {
                        _normalizedTime = 1f;
                        _isPlaying = false;
                    }
                }
            }

            ApplyFrameAtTime(_normalizedTime);
        }

        public void ApplyFrameAtTime(float t)
        {
            if (_bakedData == null || _shoulderRight == null || _handTargetR == null) return;

            BakedAttackFrame frame = _bakedData.Evaluate(t);
            Quaternion bodyRot = _bodySpace != null ? _bodySpace.rotation : Quaternion.identity;

            // R3: handTargetWorld = shoulderPos + BodySpace.rotation * handLocalPos
            Vector3 handWorld = _shoulderRight.position + bodyRot * frame.handLocalPos;
            _handTargetR.position = handWorld;
            _handTargetR.rotation = bodyRot * frame.weaponLocalRot;

            if (_weaponRig != null)
            {
                _weaponRig.transform.localPosition = Vector3.zero;
                _weaponRig.transform.localRotation = Quaternion.identity;
            }
        }

        public void BakeMotion()
        {
            if (_keyframes == null || _keyframes.Length == 0) InitKeyframes();
            _bakedData = WeaponMotionBaker.Bake(
                _keyframes,
                _armLength,
                _weaponLength,
                _baseDuration,
                anticipationEnd: 0.30f,
                hitWindowStart: 0.45f,
                hitWindowEnd: 0.55f,
                recoveryStart: 0.75f);
        }

        private void InitKeyframes()
        {
            // Datos canónicos especificados para Espada 1H, golpe 1 (diagonal descendente derecha->izquierda)
            _keyframes = new AttackKeyframe[]
            {
                new AttackKeyframe(0.00f, +20f,  -5f, 0.55f, +40f, +10f, EdgeMode.LeadVelocity),
                new AttackKeyframe(0.30f, +75f, +55f, 0.60f, +65f, +30f, EdgeMode.LeadVelocity),
                new AttackKeyframe(0.50f,  -5f, +15f, 0.90f,  +5f,  -5f, EdgeMode.LeadVelocity),
                new AttackKeyframe(0.75f, -55f, -25f, 0.70f, -20f, -60f, EdgeMode.LeadVelocity),
                new AttackKeyframe(1.00f, +20f,  -5f, 0.55f, +40f, +10f, EdgeMode.LeadVelocity)
            };
        }
    }
}
