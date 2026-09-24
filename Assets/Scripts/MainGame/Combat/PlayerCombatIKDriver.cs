using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador anatómico de Cinemática Inversa (IK) y Cadena Cinética (Hips, Spine, Chest) para el Paladín.
    /// - Cadena Cinética y Torsión de Cadera: La cadera (Hips), columna (Spine) y pecho (Chest) rotan de forma
    ///   coordinada para proyectar los ataques con potencia marcial, manteniendo las piernas en postura de combate.
    /// - Restricciones de Brazos: Two-Bone IK analítico con alcance seguro para evitar quiebres o deformaciones.
    /// - Alineación de Muñecas: Garantiza que las manos se adapten firmemente al mango del arma sin quiebres ni saltos.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public partial class PlayerCombatIKDriver : MonoBehaviour
    {
        public static PlayerCombatIKDriver Instance { get; private set; }

        [Header("Calibración Anatómica del Torso y Brazos")]
        [SerializeField] private float maxReachRatio = 0.96f;
        [SerializeField] private float ikFadeSpeed = 8.0f;

        [Header("Offsets de Agarre")]
        [SerializeField] private Vector3 rightHandOffsetPos = new Vector3(0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 rightHandOffsetRot = new Vector3(-90f, 0f, 90f);
        [SerializeField] private Vector3 leftHandOffsetPos = new Vector3(-0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 leftHandOffsetRot = new Vector3(-90f, 0f, -90f);

        private Animator _animator;
        private Transform _hipsBone;
        private Transform _spineBone;
        private Transform _chestBone;
        private Transform _rightShoulder;
        private Transform _leftShoulder;
        private Transform _rightUpperArm;
        private Transform _leftUpperArm;
        private Transform _rightForearm;
        private Transform _leftForearm;
        private Transform _rightHand;
        private Transform _leftHand;

        private float _rightArmLength = 0.65f;
        private float _leftArmLength = 0.65f;

        // Estado del arma
        private bool _isEquipped = false;
        private Transform _weaponHolder;
        private Transform _gripSocketRear;
        private Transform _gripSocketFront;
        private WeaponCombatStance _currentStance = WeaponCombatStance.OneHanded;
        private WeaponStats _currentWeapon;

        // Pesos dinámicos
        private float _currentIkWeight = 0f;
        private float _targetIkWeight = 0f;

        // Cinemática de Cadera y Torso (Hips, Spine, Chest) con Yaw, Pitch y Roll
        private bool _isAttacking = false;
        private int _currentComboIndex = 0;
        private float _attackProgress = 0f;

        private float _currentHipsYaw = 0f;
        private float _currentHipsPitch = 0f;
        private float _currentHipsRoll = 0f;
        private float _currentSpineYaw = 0f;
        private float _currentSpinePitch = 0f;
        private float _currentSpineRoll = 0f;
        private float _currentChestYaw = 0f;
        private float _currentChestPitch = 0f;
        private float _currentChestRoll = 0f;

        private float _strikeStartHipsYaw = 0f;
        private float _strikeStartHipsPitch = 0f;
        private float _strikeStartHipsRoll = 0f;
        private float _strikeStartSpineYaw = 0f;
        private float _strikeStartSpinePitch = 0f;
        private float _strikeStartSpineRoll = 0f;
        private float _strikeStartChestYaw = 0f;
        private float _strikeStartChestPitch = 0f;
        private float _strikeStartChestRoll = 0f;

        public float CurrentWeight => _currentIkWeight;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _animator = GetComponent<Animator>();
            CacheBones();
        }

        private void Start()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            CacheBones();
        }

        private void CacheBones()
        {
            if (_animator == null || !_animator.isHuman) return;

            _hipsBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spineBone = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chestBone = _animator.GetBoneTransform(HumanBodyBones.Chest) ?? _spineBone;
            _rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            _leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightForearm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _leftForearm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (_rightUpperArm != null && _rightForearm != null && _rightHand != null)
            {
                float lenUpper = Vector3.Distance(_rightUpperArm.position, _rightForearm.position);
                float lenLower = Vector3.Distance(_rightForearm.position, _rightHand.position);
                _rightArmLength = Mathf.Max(0.55f, lenUpper + lenLower);
            }

            if (_leftUpperArm != null && _leftForearm != null && _leftHand != null)
            {
                float lenUpper = Vector3.Distance(_leftUpperArm.position, _leftForearm.position);
                float lenLower = Vector3.Distance(_leftForearm.position, _leftHand.position);
                _leftArmLength = Mathf.Max(0.55f, lenUpper + lenLower);
            }
        }

        public void SetWeaponEquipped(bool equipped, Transform weaponHolder, Transform gripRear, Transform gripFront, WeaponCombatStance stance, WeaponStats weapon = null)
        {
            _isEquipped = equipped;
            _weaponHolder = weaponHolder;
            _gripSocketRear = gripRear;
            _gripSocketFront = gripFront;
            _currentStance = stance;
            _currentWeapon = weapon;
            _targetIkWeight = equipped ? 1.0f : 0.0f;
        }

        public void SetAttackState(bool isAttacking, int comboIndex, float progress)
        {
            if (isAttacking && (!_isAttacking || comboIndex != _currentComboIndex))
            {
                _strikeStartHipsYaw = _currentHipsYaw;
                _strikeStartHipsPitch = _currentHipsPitch;
                _strikeStartHipsRoll = _currentHipsRoll;
                _strikeStartSpineYaw = _currentSpineYaw;
                _strikeStartSpinePitch = _currentSpinePitch;
                _strikeStartSpineRoll = _currentSpineRoll;
                _strikeStartChestYaw = _currentChestYaw;
                _strikeStartChestPitch = _currentChestPitch;
                _strikeStartChestRoll = _currentChestRoll;
            }

            _isAttacking = isAttacking;
            _currentComboIndex = comboIndex;
            _attackProgress = Mathf.Clamp01(progress);
        }

        private void Update()
        {
            _currentIkWeight = Mathf.MoveTowards(_currentIkWeight, _targetIkWeight, Time.deltaTime * ikFadeSpeed);

            if (_isAttacking && _isEquipped)
            {
                CalculateTorsoKinematics(_currentComboIndex, _attackProgress,
                    out float hY, out float hP, out float hR,
                    out float sY, out float sP, out float sR,
                    out float cY, out float cP, out float cR);

                _currentHipsYaw = hY;
                _currentHipsPitch = hP;
                _currentHipsRoll = hR;
                _currentSpineYaw = sY;
                _currentSpinePitch = sP;
                _currentSpineRoll = sR;
                _currentChestYaw = cY;
                _currentChestPitch = cP;
                _currentChestRoll = cR;
            }
            else
            {
                float speed = Time.deltaTime * 60f;
                _currentHipsYaw = Mathf.MoveTowards(_currentHipsYaw, 0f, speed);
                _currentHipsPitch = Mathf.MoveTowards(_currentHipsPitch, 0f, speed);
                _currentHipsRoll = Mathf.MoveTowards(_currentHipsRoll, 0f, speed);
                _currentSpineYaw = Mathf.MoveTowards(_currentSpineYaw, 0f, speed);
                _currentSpinePitch = Mathf.MoveTowards(_currentSpinePitch, 0f, speed);
                _currentSpineRoll = Mathf.MoveTowards(_currentSpineRoll, 0f, speed);
                _currentChestYaw = Mathf.MoveTowards(_currentChestYaw, 0f, speed);
                _currentChestPitch = Mathf.MoveTowards(_currentChestPitch, 0f, speed);
                _currentChestRoll = Mathf.MoveTowards(_currentChestRoll, 0f, speed);
            }
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || _currentIkWeight <= 0.001f) return;

            Transform playerTransform = transform.parent != null ? transform.parent : transform;
            ApplyTorsoAndHipsKinematics(playerTransform);
            SolveArmKinematics(playerTransform);
        }
    }
}
