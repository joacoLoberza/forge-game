using System;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona el equipamiento de combate del jugador montando el arma en la mano derecha (RightHand)
    /// y vinculando postura/IK de dos manos directamente en el GameObject del Animator (PaladinVisual).
    /// </summary>
    public partial class PlayerEquipmentController : MonoBehaviour
    {
        public static PlayerEquipmentController LocalInstance { get; set; }
        private static PlayerEquipmentController _fallbackInstance;
        public static PlayerEquipmentController Instance
        {
            get => LocalInstance != null ? LocalInstance : _fallbackInstance;
            set => _fallbackInstance = value;
        }

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
        private DynamicEdgeAligner _edgeAligner;

        public WeaponStats CurrentWeapon => _currentEquippedWeapon;
        public WeaponStats EquippedWeapon => _currentEquippedWeapon;
        public bool HasWeaponEquipped => _currentEquippedWeapon != null;
        public bool IsEquipped(WeaponStats weapon) => _currentEquippedWeapon != null && _currentEquippedWeapon == weapon;
        public Transform WeaponHolder => _weaponHolder;
        public Transform GripSocketFront => _gripSocketFront;
        public Transform GripSocketRear => _gripSocketRear;
        public Animator PaladinAnimator => _paladinAnimator;
        public DynamicEdgeAligner EdgeAligner => _edgeAligner;

        public event Action<WeaponStats> OnWeaponEquipped;
        public event Action OnWeaponUnequipped;

        private void Awake()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || netObj.IsOwner) LocalInstance = this;
            _fallbackInstance = this;

            EnsurePaladinVisual();
            EnsureHolder();
            EnsureEdgeAlignerOnAnimator();
            if (attackController == null) attackController = GetComponent<AttackController>();
        }

        private void Start()
        {
            EnsurePaladinVisual();
            EnsureHolder();
            EnsureEdgeAlignerOnAnimator();
            LoadDefaultAssetsIfMissing();

            if (_currentEquippedWeapon == null)
            {
                var inv = GetComponent<ForjaGame.Inventory.InventoryController>() ?? ForjaGame.Inventory.InventoryController.LocalInstance;
                WeaponStats pickaxe = null;
                if (inv != null && inv.Weapons != null)
                {
                    for (int i = 0; i < inv.Weapons.Count; i++)
                    {
                        if (inv.Weapons[i] != null && inv.Weapons[i].weaponType == WeaponType.Pickaxe)
                        {
                            pickaxe = inv.Weapons[i];
                            break;
                        }
                    }
                }
                if (pickaxe == null) pickaxe = PickaxeModelGenerator.CreateDefaultIronPickaxe();
                EquipWeapon(pickaxe);
            }
        }

        private void EnsureEdgeAlignerOnAnimator()
        {
            GameObject targetGo = _paladinAnimator != null ? _paladinAnimator.gameObject : gameObject;
            // Si quedó una instancia antigua en la raíz cuando el Animator está en un hijo, limpiarla
            if (targetGo != gameObject)
            {
                var legacyOnRoot = GetComponent<DynamicEdgeAligner>();
                if (legacyOnRoot != null) ForjaGame.Core.UnitySafeUtility.DestroySafe(legacyOnRoot);
            }
            if (!targetGo.TryGetComponent<DynamicEdgeAligner>(out _edgeAligner)) _edgeAligner = targetGo.AddComponent<DynamicEdgeAligner>();
            _edgeAligner.BindAnimator();
        }

        private void UpdateAnimatorUnequipped()
        {
            if (_paladinAnimator != null)
            {
                if (_baseAnimatorController != null && _paladinAnimator.runtimeAnimatorController != _baseAnimatorController)
                    _paladinAnimator.runtimeAnimatorController = _baseAnimatorController;
                if (_paladinAnimator.layerCount > 1) _paladinAnimator.SetLayerWeight(1, 0f);
                if (_paladinAnimator.layerCount > 2) _paladinAnimator.SetLayerWeight(2, 0f);
            }
        }

        private void EnsureHolder()
        {
            Transform rightHand = _paladinAnimator != null ? _paladinAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (rightHand != null)
            {
                Transform existing = rightHand.Find("CombatWeaponHolder");
                if (existing != null) _weaponHolder = existing;
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
                _weaponHolder = existing != null ? existing : new GameObject("CombatWeaponHolder").transform;
                _weaponHolder.SetParent(transform, false);
            }
        }

        public void EquipWeapon(WeaponStats weapon)
        {
            if (weapon == null) return;
            EnsurePaladinVisual();
            EnsureHolder();
            EnsureEdgeAlignerOnAnimator();

            Transform rightHand = _paladinAnimator != null ? _paladinAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (rightHand != null && _weaponHolder != null && _weaponHolder.parent != rightHand)
            {
                _weaponHolder.SetParent(rightHand, false);
                _weaponHolder.localPosition = gripInHandPos;
                _weaponHolder.localRotation = Quaternion.Euler(gripInHandRot);
            }

            UnequipWeapon();

            _currentEquippedWeapon = weapon;
            var stance = weapon.GetCombatStance();
            bool is2H = stance == WeaponCombatStance.TwoHanded || stance == WeaponCombatStance.Polearm;
            float separation = stance switch
            {
                WeaponCombatStance.Polearm => handSeparation_Polearm,
                WeaponCombatStance.TwoHanded => handSeparation_2H,
                _ => handSeparation_1H
            };

            GameObject fallbackPrefab = GetPrefabForWeapon(weapon);
            _spawnedWeaponInstance = WeaponVisualBuilder.BuildWeaponVisual(weapon, _weaponHolder, fallbackPrefab);

            if (_spawnedWeaponInstance != null)
            {
                _spawnedWeaponInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                _spawnedWeaponInstance.transform.localScale = Vector3.one;
                SetupGripGauntlets(weapon, _spawnedWeaponInstance.transform, separation);
            }

            if (_edgeAligner != null)
            {
                _edgeAligner.weaponSocket = _spawnedWeaponInstance != null ? _spawnedWeaponInstance.transform : _weaponHolder;
                _edgeAligner.frontGripSocket = _gripSocketFront;
                _edgeAligner.weaponLengthY = Mathf.Max(0.5f, weapon.attackRange * 0.75f);
                _edgeAligner.isTwoHandedWeapon = is2H;
                _edgeAligner.isPickaxeOrTool = weapon.weaponType == WeaponType.Pickaxe;
                _edgeAligner.secondaryGripDistanceOnShaft = stance == WeaponCombatStance.Polearm ? 0.44f : 0.28f;
                _edgeAligner.CaptureCalibratedRotation();
            }

            var overrideCtrl = GetOverrideControllerForWeapon(weapon);
            if (_paladinAnimator != null)
            {
                if (overrideCtrl != null) _paladinAnimator.runtimeAnimatorController = overrideCtrl;
                if (_paladinAnimator.layerCount > 1)
                {
                    float initWeight = weapon.weaponType == WeaponType.Pickaxe ? 0.18f : (is2H ? 0.42f : 0.22f);
                    _paladinAnimator.SetLayerWeight(1, initWeight);
                    _paladinAnimator.CrossFadeInFixedTime("CombatStance", 0.15f, 1);
                }
                if (_paladinAnimator.layerCount > 2) _paladinAnimator.SetLayerWeight(2, 1.0f);
            }

            if (attackController != null) attackController.ApplyWeaponStats(weapon);
            OnWeaponEquipped?.Invoke(weapon);
        }

        public void UnequipWeapon()
        {
            if (_weaponHolder != null)
            {
                for (int i = _weaponHolder.childCount - 1; i >= 0; i--)
                    ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(_weaponHolder.GetChild(i).gameObject);
            }
            if (_spawnedWeaponInstance != null) { ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(_spawnedWeaponInstance); _spawnedWeaponInstance = null; }
            _currentEquippedWeapon = null;
            _gripSocketRear = null;
            _gripSocketFront = null;
            if (_edgeAligner != null)
            {
                _edgeAligner.weaponSocket = null;
                _edgeAligner.frontGripSocket = null;
                _edgeAligner.isTwoHandedWeapon = false;
                _edgeAligner.isAttacking = false;
            }
            UpdateAnimatorUnequipped();
            if (attackController != null) attackController.ApplyWeaponStats(null);
            OnWeaponUnequipped?.Invoke();
        }
    }
}
