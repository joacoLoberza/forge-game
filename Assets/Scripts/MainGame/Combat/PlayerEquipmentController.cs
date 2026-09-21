using System;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona el arma equipada en la mano del jugador, sincronizando el modelo 3D,
    /// la postura/animaciones en el Animator y las estadísticas en AttackController.
    /// </summary>
    public partial class PlayerEquipmentController : MonoBehaviour
    {
        public static PlayerEquipmentController Instance { get; private set; }

        [Header("Referencias Visuales y de Combate")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private AttackController attackController;

        [Header("Animadores de Postura y Estilos")]
        [SerializeField] private RuntimeAnimatorController baseController;
        [SerializeField] private RuntimeAnimatorController swordOverride;
        [SerializeField] private RuntimeAnimatorController axeOverride;
        [SerializeField] private RuntimeAnimatorController hammerOverride;
        [SerializeField] private RuntimeAnimatorController heavy2HOverride;
        [SerializeField] private RuntimeAnimatorController polearmOverride;

        [Header("Prefabs de Armas en Mano (Fallbacks)")]
        [SerializeField] private GameObject swordPrefab;
        [SerializeField] private GameObject hammerPrefab;
        [SerializeField] private GameObject axePrefab;

        [Header("Calibración de Agarre por Postura")]
        [SerializeField] private Vector3 gripPositionOffset_1H = new Vector3(0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 gripRotationOffset_1H = new Vector3(-90f, 0f, 90f);

        [SerializeField] private Vector3 gripPositionOffset_2H = new Vector3(0.035f, 0.085f, -0.015f);
        [SerializeField] private Vector3 gripRotationOffset_2H = new Vector3(-85f, 5f, 85f);

        [SerializeField] private Vector3 gripPositionOffset_Polearm = new Vector3(0.035f, 0.090f, -0.015f);
        [SerializeField] private Vector3 gripRotationOffset_Polearm = new Vector3(-80f, 0f, 90f);

        [Header("Ajustes Finos Globales de Mano Derecha")]
        [SerializeField] private Vector3 gripPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 gripRotationOffset = Vector3.zero;

        private WeaponStats _currentEquippedWeapon;
        private GameObject _spawnedWeaponInstance;
        private Transform _rightHandTransform;

        public WeaponStats CurrentWeapon => _currentEquippedWeapon;
        public bool HasWeaponEquipped => _currentEquippedWeapon != null;

        public event Action<WeaponStats> OnWeaponEquipped;
        public event Action OnWeaponUnequipped;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();
            LoadDefaultAssetsIfMissing();

            // Si al arrancar no hay arma equipada, asegurar postura y locomoción uniforme desarmada
            if (_currentEquippedWeapon == null)
            {
                if (characterAnimator != null && baseController != null)
                {
                    characterAnimator.runtimeAnimatorController = baseController;
                }
                SetCombatLayerWeights(0.0f, 0.0f);
                if (attackController != null)
                {
                    attackController.ApplyWeaponStats(null);
                }
            }
        }

        public void EquipWeapon(WeaponStats weapon)
        {
            if (weapon == null) return;
            EnsureReferences();

            // Si ya tiene un arma equipada, quitarla antes
            if (_spawnedWeaponInstance != null)
            {
                Destroy(_spawnedWeaponInstance);
                _spawnedWeaponInstance = null;
            }

            _currentEquippedWeapon = weapon;

            // 1. Instanciar y equipar arma según el sistema activo (Procedural vs Legado)
            bool isProcedural = attackController != null && attackController.UseProceduralCombat;
            var proceduralCombat = GetComponent<ProceduralCombatController>() ?? gameObject.AddComponent<ProceduralCombatController>();

            if (isProcedural && proceduralCombat != null)
            {
                GameObject fallbackPrefab = GetPrefabForWeapon(weapon.weaponType);
                _spawnedWeaponInstance = WeaponVisualBuilder.BuildWeaponVisual(weapon, null, fallbackPrefab);
                proceduralCombat.EquipWeapon(weapon, _spawnedWeaponInstance);
                SetCombatLayerWeights(0.0f, 0.0f);
            }
            else
            {
                // 1. Sistema legado: anclar modelo al socket de la mano
                Transform hand = GetRightHandBone();
                if (hand != null)
                {
                    Transform socket = GetOrCreateWeaponGripSocket(hand);
                    ApplySocketCalibration(socket, weapon.GetCombatStance());

                    GameObject fallbackPrefab = GetPrefabForWeapon(weapon.weaponType);
                    _spawnedWeaponInstance = WeaponVisualBuilder.BuildWeaponVisual(weapon, socket, fallbackPrefab);
                }

                // 2. Cambiar postura y animaciones con override controllers
                if (characterAnimator != null)
                {
                    RuntimeAnimatorController targetController = GetControllerForWeapon(weapon);
                    if (targetController != null)
                    {
                        characterAnimator.runtimeAnimatorController = targetController;
                    }
                    SetCombatLayerWeights(1.0f, 1.0f);
                }
            }

            // 3. Sincronizar estadísticas con AttackController
            if (attackController != null)
            {
                attackController.ApplyWeaponStats(weapon);
            }

            OnWeaponEquipped?.Invoke(weapon);
            Debug.Log($"[Equipment] Arma equipada: {weapon.DisplayName} ({weapon.weaponType}, Stance: {weapon.GetCombatStance()}, Procedural: {isProcedural}). Daño: {weapon.EffectiveDamage:F1}");
        }

        private void SetCombatLayerWeights(float upperBodyWeight, float handsWeight)
        {
            if (characterAnimator == null) return;
            for (int i = 0; i < characterAnimator.layerCount; i++)
            {
                string layerName = characterAnimator.GetLayerName(i);
                if (layerName.IndexOf("Upper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    layerName.IndexOf("Arms", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    characterAnimator.SetLayerWeight(i, upperBodyWeight);
                }
                else if (layerName.IndexOf("Hands", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    characterAnimator.SetLayerWeight(i, handsWeight);
                }
            }
        }

        public void UnequipWeapon()
        {
            if (_spawnedWeaponInstance != null)
            {
                Destroy(_spawnedWeaponInstance);
                _spawnedWeaponInstance = null;
            }

            var proceduralCombat = GetComponent<ProceduralCombatController>();
            if (proceduralCombat != null)
            {
                proceduralCombat.UnequipWeapon();
            }

            _currentEquippedWeapon = null;

            // Restaurar animaciones base
            if (characterAnimator != null)
            {
                if (baseController != null)
                    characterAnimator.runtimeAnimatorController = baseController;
                SetCombatLayerWeights(0.0f, 0.0f);
            }

            // Restaurar estadísticas base en AttackController
            if (attackController != null)
            {
                attackController.ResetToDefaultStats();
            }

            OnWeaponUnequipped?.Invoke();
            Debug.Log("[Equipment] Arma desequipada. Postura desarmada restaurada.");
        }

        public bool IsEquipped(WeaponStats weapon)
        {
            if (_currentEquippedWeapon == null || weapon == null) return false;
            return _currentEquippedWeapon.Id == weapon.Id;
        }



        private void EnsureReferences()
        {
            if (characterAnimator == null)
                characterAnimator = GetComponentInChildren<Animator>();

            if (attackController == null)
                attackController = GetComponent<AttackController>();

            if (baseController == null && characterAnimator != null)
                baseController = characterAnimator.runtimeAnimatorController;
        }


    }
}
