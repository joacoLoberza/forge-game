using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador anatómico unificado de Combate e IK para el Paladín.
    /// Fuente de verdad única: la trayectoria del arma en espacio del Pecho gobierna el torso y los brazos.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public partial class PlayerCombatIKDriver : MonoBehaviour
    {
        public static PlayerCombatIKDriver Instance { get; private set; }

        [Header("Offsets de Agarre")]
        [SerializeField] private Vector3 rightHandOffsetPos = new Vector3(0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 rightHandOffsetRot = new Vector3(-90f, 0f, 90f);
        [SerializeField] private Vector3 leftHandOffsetPos = new Vector3(-0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 leftHandOffsetRot = new Vector3(-90f, 0f, -90f);

        [Header("Parámetros de Suavizado")]
        [SerializeField] private float ikFadeSpeed = 8.0f;
        [SerializeField] private float returnToGuardTime = 0.22f;

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

        private bool _isEquipped = false;
        private Transform _weaponHolder;
        private Transform _gripSocketRear;
        private Transform _gripSocketFront;
        private WeaponCombatStance _currentStance = WeaponCombatStance.OneHanded;
        private WeaponStats _currentWeapon;
        private WeaponAnalysis _analysis;

        private TorsoDriver _torsoDriver;
        private AttackMove _currentMove;
        private float _attackProgress = 0f;
        private bool _isAttacking = false;

        private float _currentIkWeight = 0f;
        private float _targetIkWeight = 0f;

        private Vector3 _guardGripChest = new Vector3(0.24f, -0.16f, 0.38f);
        private Quaternion _guardRotChest = Quaternion.Euler(0f, 15f, 25f);
        private Vector3 _springVelocity = Vector3.zero;

        public float CurrentWeight => _currentIkWeight;
        public float RightArmLength => _rightArmLength;
        public Transform ChestBone => _chestBone;
        public WeaponAnalysis CurrentAnalysis => _analysis;

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

            _torsoDriver = new TorsoDriver(_hipsBone, _spineBone, _chestBone);
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

            if (equipped && weapon != null)
            {
                _analysis = WeaponAnalyzer.Analyze(weapon, _rightArmLength);
                ConfigureGuardPose(_analysis.archetype);
            }
        }

        private void ConfigureGuardPose(WeaponArchetype archetype)
        {
            switch (archetype)
            {
                case WeaponArchetype.Dagger:
                    _guardGripChest = new Vector3(0.20f, -0.12f, 0.32f);
                    _guardRotChest = Quaternion.Euler(0f, 10f, 15f);
                    break;
                case WeaponArchetype.Heavy2H:
                    _guardGripChest = new Vector3(0.18f, -0.22f, 0.35f);
                    _guardRotChest = Quaternion.Euler(15f, 20f, 35f);
                    break;
                case WeaponArchetype.Polearm:
                    _guardGripChest = new Vector3(0.20f, -0.20f, 0.34f);
                    _guardRotChest = Quaternion.Euler(10f, 15f, 20f);
                    break;
                default:
                    _guardGripChest = new Vector3(0.24f, -0.16f, 0.38f);
                    _guardRotChest = Quaternion.Euler(0f, 15f, 25f);
                    break;
            }
        }

        public void StartAttackMove(AttackMove move, float duration)
        {
            _currentMove = move;
            _attackProgress = 0f;
            _isAttacking = true;
        }

        public void UpdateAttackProgress(float progress)
        {
            _attackProgress = Mathf.Clamp01(progress);
        }

        public void EndAttack()
        {
            _isAttacking = false;
        }

        public void SetAttackState(bool isAttacking, int comboIndex, float progress)
        {
            _isAttacking = isAttacking;
            _attackProgress = Mathf.Clamp01(progress);
        }

        private void Update()
        {
            _currentIkWeight = Mathf.MoveTowards(_currentIkWeight, _targetIkWeight, Time.deltaTime * ikFadeSpeed);
        }
    }
}
