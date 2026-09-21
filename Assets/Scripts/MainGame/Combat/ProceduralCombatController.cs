using System;
using UnityEngine;
using ForjaGame.Forge;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Fases de ejecución de un ataque procedural.
    /// </summary>
    public enum AttackSubPhase
    {
        InGuard,
        Windup,
        Strike,
        Recovery
    }

    /// <summary>
    /// Controlador central de combate procedural conducido por el perfil físico del arma forjada.
    /// Bloque B (Fase 3):
    /// - Ejecución de ataques procedurales derivados de AttackDefinitionSO (9 ataques base por GripMode).
    /// - Interpolación por curvas (Ease) y arcos Bézier con poses intermedias.
    /// - Cadena cinética secuencial (efecto látigo: Hips -> Spine -> Chest -> Arms) con retardos temporales configurables.
    /// - Límites anatómicos de hombro, muñeca y alcance bimanual clamped sin pop ni hiperextensión.
    /// - Volumen de exclusión corporal (5 puntos del arma testeados contra torso, cabeza y piernas con repulsión física).
    /// - Sincronización del lunge hacia Rigidbody.
    /// </summary>
    public partial class ProceduralCombatController : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private ProceduralCombatConfigSO config;

        [Header("Referencias Opcionales")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private AttackController attackController;

        // Jerarquía de huesos
        private Transform _hipsBone;
        private Transform _spineBone;
        private Transform _chestBone;
        private Transform _headBone;
        private Transform _rightShoulder;
        private Transform _leftShoulder;
        private Transform _weaponPivot;
        private Transform _gripSocketR;
        private Transform _gripSocketL;
        private Transform _bladeTip;
        private Transform _bladeBase;

        // Longitudes anatómicas de brazos
        private float _rightArmLength = 0.72f;
        private float _leftArmLength = 0.72f;
        private float _currentDistR = 0f;
        private float _currentDistL = 0f;

        // Estado del arma
        private WeaponStats _currentWeapon;
        private WeaponProfile _currentProfile;
        private GameObject _spawnedWeaponInstance;
        private bool _isEquipped = false;
        private float _ikWeight = 0.0f;
        private float _targetIkWeight = 0.0f;

        // Pose de guardia base
        private Vector3 _currentGuardLocalPos;
        private Quaternion _currentGuardLocalRot;
        private Vector3 _dynamicGuardOffset = Vector3.zero;

        // Máquina de estados de ataque procedural
        private AttackSubPhase _currentSubPhase = AttackSubPhase.InGuard;
        private AttackDefinitionSO _currentAttackDef;
        private AttackDefinitionSO[] _currentComboSequence;
        private int _currentComboIndex = 0;
        private float _phaseTime = 0f;
        private float _phaseDuration = 0.15f;
        private float _inertiaScale = 1.0f;

        // Poses e interpolación
        private Pose _fromHandPose;
        private Pose _toHandPose;
        private AttackPhase _activePhaseData;

        // Cadena cinética (ángulos y desplazamiento acumulados)
        private float _hipsYaw = 0f;
        private float _spineYaw = 0f;
        private float _spinePitch = 0f;
        private float _spineRoll = 0f;
        private float _chestYaw = 0f;
        private float _chestPitch = 0f;
        private float _chestRoll = 0f;
        private Vector3 _weightShift = Vector3.zero;

        private float _fromHipYaw = 0f, _toHipYaw = 0f;
        private float _fromSpineYaw = 0f, _toSpineYaw = 0f;
        private float _fromSpinePitch = 0f, _toSpinePitch = 0f;
        private float _fromSpineRoll = 0f, _toSpineRoll = 0f;
        private float _fromChestYaw = 0f, _toChestYaw = 0f;
        private float _fromChestPitch = 0f, _toChestPitch = 0f;
        private float _fromChestRoll = 0f, _toChestRoll = 0f;
        private Vector3 _fromWeightShift = Vector3.zero, _toWeightShift = Vector3.zero;

        // Propiedades públicas para Gizmos, Debug y Banco de Pruebas
        public ProceduralCombatConfigSO Config => config;
        public WeaponProfile CurrentProfile => _currentProfile;
        public WeaponStats CurrentWeapon => _currentWeapon;
        public Transform WeaponPivot => _weaponPivot;
        public Transform GripSocketR => _gripSocketR;
        public Transform GripSocketL => _gripSocketL;
        public Transform BladeTip => _bladeTip;
        public Transform BladeBase => _bladeBase;
        public Transform ChestBone => _chestBone;
        public Transform HipsBone => _hipsBone;
        public Transform RightShoulder => _rightShoulder;
        public Transform LeftShoulder => _leftShoulder;
        public float RightArmLength => _rightArmLength;
        public float LeftArmLength => _leftArmLength;
        public float CurrentDistR => _currentDistR;
        public float CurrentDistL => _currentDistL;
        public float IKWeight => _ikWeight;
        public bool IsEquipped => _isEquipped;

        public AttackSubPhase CurrentSubPhase => _currentSubPhase;
        public AttackDefinitionSO CurrentAttackDef => _currentAttackDef;
        public int CurrentComboIndex => _currentComboIndex;
        public float InertiaScale => _inertiaScale;
        public float PhaseDuration => _phaseDuration;
        public float PhaseTime => _phaseTime;
        public float PhaseNormalizedTime => _phaseDuration > 0.001f ? Mathf.Clamp01(_phaseTime / _phaseDuration) : 0f;
        public Vector3 CurrentHipsEuler => new Vector3(0f, _hipsYaw, 0f);
        public Vector3 CurrentChestEuler => new Vector3(_chestPitch, _chestYaw, _chestRoll);
        public Vector3 CurrentWeightShift => _weightShift;

        private void Awake()
        {
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (playerRigidbody == null) playerRigidbody = GetComponent<Rigidbody>();
            if (attackController == null) attackController = GetComponent<AttackController>();
            if (config == null)
            {
#if UNITY_EDITOR
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<ProceduralCombatConfigSO>(
                    "Assets/ScriptableObjects/Combat/Config/ProceduralCombatConfig.asset");
#endif
                if (config == null) config = ScriptableObject.CreateInstance<ProceduralCombatConfigSO>();
            }

            InitializeSkeletonAndPivot();
        }

        private void Update()
        {
            // Suavizado del peso del IK general
            _ikWeight = Mathf.MoveTowards(_ikWeight, _targetIkWeight, Time.deltaTime * 6.0f);

            if (!_isEquipped || _weaponPivot == null) return;

            UpdateAttackStateMachine();

            // Repeler el arma si cualquier punto penetra cápsulas de exclusión corporal
            ApplyBodyExclusion();
        }

        public void ResetToGuard()
        {
            _currentSubPhase = AttackSubPhase.InGuard;
            _phaseTime = 0f;
            _phaseDuration = 0.20f;
            if (_weaponPivot != null)
            {
                _fromHandPose = new Pose(_weaponPivot.localPosition, _weaponPivot.localRotation);
            }
            Vector3 guardPos = _currentGuardLocalPos + _dynamicGuardOffset;
            _toHandPose = new Pose(guardPos, _currentGuardLocalRot);
            _activePhaseData = AttackPhase.CreateDefault(_toHandPose, 0.20f);

            _fromHipYaw = _hipsYaw; _toHipYaw = 0f;
            _fromSpineYaw = _spineYaw; _toSpineYaw = 0f;
            _fromSpinePitch = _spinePitch; _toSpinePitch = 0f;
            _fromSpineRoll = _spineRoll; _toSpineRoll = 0f;
            _fromChestYaw = _chestYaw; _toChestYaw = 0f;
            _fromChestPitch = _chestPitch; _toChestPitch = 0f;
            _fromChestRoll = _chestRoll; _toChestRoll = 0f;
            _fromWeightShift = _weightShift; _toWeightShift = Vector3.zero;
        }

        public void SetDynamicGuardOffset(Vector3 offset)
        {
            _dynamicGuardOffset = offset;
        }
    }
}
