using System;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona el equipamiento de combate del jugador montando el arma directamente
    /// en la mano derecha (RightHand) del Paladín, garantizando cero flotación.
    /// </summary>
    public partial class PlayerEquipmentController : MonoBehaviour
    {
        public static PlayerEquipmentController Instance { get; private set; }

        [Header("Referencias")]
        [SerializeField] private AttackController attackController;

        [Header("Offset de Agarre en Mano Derecha")]
        [SerializeField] private Vector3 gripInHandPos = new Vector3(0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 gripInHandRot = new Vector3(-90f, 0f, 90f);

        [Header("Modelo 3D del Personaje")]
        [SerializeField] private GameObject paladinPrefab;
        [SerializeField] private RuntimeAnimatorController paladinAnimatorController;

        private Transform _weaponHolder;
        private WeaponStats _currentEquippedWeapon;
        private GameObject _spawnedWeaponInstance;
        private Transform _gripSocketRear;
        private Transform _gripSocketFront;
        private Animator _paladinAnimator;
        private RuntimeAnimatorController _baseAnimatorController;

        public WeaponStats CurrentWeapon => _currentEquippedWeapon;
        public bool HasWeaponEquipped => _currentEquippedWeapon != null;
        public bool IsEquipped(WeaponStats weapon) => _currentEquippedWeapon != null && _currentEquippedWeapon == weapon;
        public Transform WeaponHolder => _weaponHolder;
        public Transform GripSocketFront => _gripSocketFront;
        public Transform GripSocketRear => _gripSocketRear;
        public Animator PaladinAnimator => _paladinAnimator;

        public event Action<WeaponStats> OnWeaponEquipped;
        public event Action OnWeaponUnequipped;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            EnsurePaladinVisual();
            EnsureHolder();
            if (attackController == null) attackController = GetComponent<AttackController>();
        }

        private void Start()
        {
            EnsurePaladinVisual();
            EnsureHolder();
            LoadDefaultAssetsIfMissing();

            if (_currentEquippedWeapon == null)
            {
                if (attackController != null) attackController.ApplyWeaponStats(null);
                UpdateAnimatorUnequipped();
            }
        }

        private void UpdateAnimatorUnequipped()
        {
            if (_paladinAnimator != null && _baseAnimatorController != null)
            {
                if (_paladinAnimator.runtimeAnimatorController != _baseAnimatorController)
                {
                    _paladinAnimator.runtimeAnimatorController = _baseAnimatorController;
                }
            }
            PlayerCombatIKDriver.Instance?.SetWeaponEquipped(false, null, null, null, WeaponCombatStance.OneHanded);
        }

        private void EnsureHolder()
        {
            Transform rightHand = _paladinAnimator != null
                ? _paladinAnimator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;

            if (rightHand != null)
            {
                Transform existing = rightHand.Find("CombatWeaponHolder");
                if (existing != null)
                {
                    _weaponHolder = existing;
                }
                else
                {
                    GameObject holderGo = new GameObject("CombatWeaponHolder");
                    holderGo.transform.SetParent(rightHand, false);
                    holderGo.transform.localPosition = gripInHandPos;
                    holderGo.transform.localRotation = Quaternion.Euler(gripInHandRot);
                    _weaponHolder = holderGo.transform;
                }
                return;
            }

            if (_weaponHolder == null)
            {
                Transform existing = transform.Find("CombatWeaponHolder");
                if (existing != null) _weaponHolder = existing;
                else
                {
                    GameObject holderGo = new GameObject("CombatWeaponHolder");
                    holderGo.transform.SetParent(transform, false);
                    _weaponHolder = holderGo.transform;
                }
            }
        }

        public void EquipWeapon(WeaponStats weapon)
        {
            if (weapon == null) return;
            EnsurePaladinVisual();
            EnsureHolder();

            Transform rightHand = _paladinAnimator != null
                ? _paladinAnimator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;

            if (rightHand != null && _weaponHolder != null && _weaponHolder.parent != rightHand)
            {
                _weaponHolder.SetParent(rightHand, false);
                _weaponHolder.localPosition = gripInHandPos;
                _weaponHolder.localRotation = Quaternion.Euler(gripInHandRot);
            }

            if (_spawnedWeaponInstance != null)
            {
                UnequipWeapon();
            }

            _currentEquippedWeapon = weapon;
            var stance = weapon.GetCombatStance();
            float separation = stance switch
            {
                WeaponCombatStance.Polearm => handSeparation_Polearm,
                WeaponCombatStance.TwoHanded => handSeparation_2H,
                _ => handSeparation_1H
            };

            // 1. Instanciar arma rígidamente dentro de _weaponHolder (en la mano derecha)
            GameObject fallbackPrefab = GetPrefabForWeapon(weapon);
            _spawnedWeaponInstance = WeaponVisualBuilder.BuildWeaponVisual(weapon, _weaponHolder, fallbackPrefab);

            if (_spawnedWeaponInstance != null)
            {
                _spawnedWeaponInstance.transform.localPosition = Vector3.zero;
                _spawnedWeaponInstance.transform.localRotation = Quaternion.identity;
                _spawnedWeaponInstance.transform.localScale = Vector3.one;

                SetupGripGauntlets(weapon, _spawnedWeaponInstance.transform, separation);
            }

            if (attackController != null) attackController.ApplyWeaponStats(weapon);

            PlayerCombatIKDriver.Instance?.SetWeaponEquipped(true, _weaponHolder, _gripSocketRear, _gripSocketFront, stance, weapon);
            OnWeaponEquipped?.Invoke(weapon);
        }

        public void UnequipWeapon()
        {
            if (_spawnedWeaponInstance != null)
            {
                Destroy(_spawnedWeaponInstance);
                _spawnedWeaponInstance = null;
            }

            _currentEquippedWeapon = null;
            _gripSocketRear = null;
            _gripSocketFront = null;

            UpdateAnimatorUnequipped();
            if (attackController != null) attackController.ApplyWeaponStats(null);
            OnWeaponUnequipped?.Invoke();
        }
    }
}
