using System;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona el equipamiento de combate del jugador en estilo Rayman (manos flotantes estilizadas).
    /// El arma y los guanteletes de forja se mueven en perfecta sincronía mediante WeaponComboAnimator,
    /// garantizando que los pies permanezcan 100% firmes en el suelo sin animaciones que quiebren articulaciones.
    /// </summary>
    public partial class PlayerEquipmentController : MonoBehaviour
    {
        public static PlayerEquipmentController Instance { get; private set; }

        [Header("Referencias")]
        [SerializeField] private AttackController attackController;

        [Header("Estilo Visual de Guanteletes Flotantes (Estilo Rayman)")]
        [SerializeField] private float gauntletScale = 1.0f;
        [SerializeField] private Color metalPlateColor = new Color(0.72f, 0.74f, 0.78f);
        [SerializeField] private Color leatherUnderColor = new Color(0.16f, 0.13f, 0.11f);
        [SerializeField] private Material customGauntletMetalMaterial;
        [SerializeField] private Material customGauntletLeatherMaterial;

        [Header("Modelo 3D del Personaje")]
        [SerializeField] private GameObject paladinPrefab;
        [SerializeField] private RuntimeAnimatorController paladinAnimatorController;

        private Transform _weaponHolder;
        private WeaponStats _currentEquippedWeapon;
        private GameObject _spawnedWeaponInstance;
        private GameObject _rearGauntletInstance;
        private GameObject _frontGauntletInstance;
        private Material _metalMaterialInstance;
        private Material _leatherMaterialInstance;

        private Transform _gripSocketRear;
        private Transform _gripSocketFront;
        private Animator _paladinAnimator;
        private RuntimeAnimatorController _baseAnimatorController;

        public WeaponStats CurrentWeapon => _currentEquippedWeapon;
        public bool HasWeaponEquipped => _currentEquippedWeapon != null;
        public Transform WeaponHolder => _weaponHolder;
        public GameObject RearHandSphere => _rearGauntletInstance;
        public GameObject FrontHandSphere => _frontGauntletInstance;
        public Transform GripSocketFront => _gripSocketFront;
        public Transform GripSocketRear => _gripSocketRear;
        public Animator PaladinAnimator => _paladinAnimator;

        public event Action<WeaponStats> OnWeaponEquipped;
        public event Action OnWeaponUnequipped;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            EnsureHolder();
            EnsurePaladinVisual();
            if (attackController == null) attackController = GetComponent<AttackController>();
            if (GetComponent<WeaponComboAnimator>() == null) gameObject.AddComponent<WeaponComboAnimator>();
        }

        private void Start()
        {
            EnsureHolder();
            EnsurePaladinVisual();
            LoadDefaultAssetsIfMissing();

            if (_currentEquippedWeapon == null)
            {
                if (attackController != null) attackController.ApplyWeaponStats(null);
                UpdateAnimatorUnequipped();
            }
        }

        private void UpdateAnimatorUnequipped()
        {
            if (_paladinAnimator != null)
            {
                if (_baseAnimatorController != null && _paladinAnimator.runtimeAnimatorController != _baseAnimatorController)
                {
                    _paladinAnimator.runtimeAnimatorController = _baseAnimatorController;
                }
                if (_paladinAnimator.layerCount > 1) _paladinAnimator.SetLayerWeight(1, 0f);
                if (_paladinAnimator.layerCount > 2) _paladinAnimator.SetLayerWeight(2, 0f);
            }
            PlayerCombatIKDriver.Instance?.SetWeaponEquipped(false, null, null, null, WeaponCombatStance.OneHanded);
        }

        private void EnsureHolder()
        {
            if (_weaponHolder != null) return;

            Transform existing = transform.Find("CombatWeaponHolder");
            if (existing != null)
            {
                _weaponHolder = existing;
            }
            else
            {
                GameObject holderGo = new GameObject("CombatWeaponHolder");
                holderGo.transform.SetParent(transform, false);
                _weaponHolder = holderGo.transform;
            }
        }

        public void EquipWeapon(WeaponStats weapon)
        {
            if (weapon == null) return;
            EnsureHolder();
            EnsurePaladinVisual();

            if (_spawnedWeaponInstance != null)
            {
                UnequipWeapon();
            }

            _currentEquippedWeapon = weapon;
            var stance = weapon.GetCombatStance();
            (Vector3 targetPos, Vector3 targetRot, float separation) = GetStanceCalibration(weapon, stance);

            // 1. Ubicar y orientar el WeaponHolder en paralelo a la cápsula (frente al jugador)
            _weaponHolder.localPosition = targetPos;
            _weaponHolder.localRotation = Quaternion.Euler(targetRot);

            // 2. Instanciar el modelo visual del arma (procedural forjada o fallback FBX)
            GameObject fallbackPrefab = GetPrefabForWeapon(weapon);
            _spawnedWeaponInstance = WeaponVisualBuilder.BuildWeaponVisual(weapon, _weaponHolder, fallbackPrefab);

            if (_spawnedWeaponInstance != null)
            {
                _spawnedWeaponInstance.transform.localPosition = Vector3.zero;
                _spawnedWeaponInstance.transform.localRotation = Quaternion.identity;
                _spawnedWeaponInstance.transform.localScale = Vector3.one;

                // 3. Crear sockets de agarre en el mango
                SetupGripGauntlets(weapon, _spawnedWeaponInstance.transform, separation);
            }

            // 4. Activar postura marcial (CombatIdle) y agarre de dedos en el Animator del Paladín
            if (_paladinAnimator != null)
            {
                var overrideCtrl = GetOverrideControllerForWeapon(weapon);
                if (overrideCtrl != null && _paladinAnimator.runtimeAnimatorController != overrideCtrl)
                {
                    _paladinAnimator.runtimeAnimatorController = overrideCtrl;
                }
                if (_paladinAnimator.layerCount > 2)
                {
                    _paladinAnimator.SetLayerWeight(2, 1.0f);
                }
            }

            // 5. Aplicar estadísticas a AttackController
            if (attackController != null)
            {
                attackController.ApplyWeaponStats(weapon);
            }

            OnWeaponEquipped?.Invoke(weapon);
            Debug.Log($"[Equipment] Arma equipada: {weapon.DisplayName} ({weapon.weaponType}, Stance: {stance}) montada con cinemática de agarre.");
        }

        public void UnequipWeapon()
        {
            if (_spawnedWeaponInstance != null)
            {
                Destroy(_spawnedWeaponInstance);
                _spawnedWeaponInstance = null;
            }

            _gripSocketRear = null;
            _gripSocketFront = null;
            _rearGauntletInstance = null;
            _frontGauntletInstance = null;
            _currentEquippedWeapon = null;
            WeaponComboAnimator.Instance?.CancelOrReturnToGuard();

            UpdateAnimatorUnequipped();

            if (attackController != null)
            {
                attackController.ResetToDefaultStats();
            }

            OnWeaponUnequipped?.Invoke();
            Debug.Log("[Equipment] Arma desequipada limpiamente. Postura humana relajada restaurada.");
        }

        public bool IsEquipped(WeaponStats weapon)
        {
            if (_currentEquippedWeapon == null || weapon == null) return false;
            return _currentEquippedWeapon.Id == weapon.Id;
        }
    }
}
