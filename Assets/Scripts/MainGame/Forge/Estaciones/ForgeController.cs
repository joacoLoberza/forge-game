using System;
using UnityEngine;
using ForjaGame.Inventory;

namespace ForjaGame.Forge
{
    public enum ForgePhase
    {
        RawMaterial,
        Furnace,
        Press,
        WeaponSelection,
        Anvil,
        Quenching,
        HandleAssembly,
        Completed
    }

    /// <summary>
    /// Orquestador principal del proceso de forja: coordina horno, prensa,
    /// yunque y ensamblaje, permitiendo retirar objetos intermedios.
    /// </summary>
    public class ForgeController : MonoBehaviour
    {
        [Header("Controladores de Estación")]
        [SerializeField] private FurnaceController furnaceController;
        [SerializeField] private PressController pressController;
        [SerializeField] private AnvilController anvilController;
        [SerializeField] private WeaponAssemblyController assemblyController;

        [Header("Estado de Forja")]
        [SerializeField] private ForgePhase currentPhase = ForgePhase.RawMaterial;
        [SerializeField] private WeaponType selectedWeaponType = WeaponType.Sword;
        [SerializeField] private HandleLength selectedHandleLength = HandleLength.Medium;

        // Objetos intermedios activos en la sesión de forja
        private BloomData _activeBloom;
        private IngotData _activeIngot;
        private WeaponStats _completedWeapon;

        public ForgePhase CurrentPhase => currentPhase;
        public BloomData ActiveBloom => _activeBloom;
        public IngotData ActiveIngot => _activeIngot;
        public WeaponStats CompletedWeapon => _completedWeapon;

        public event Action<ForgePhase> PhaseChanged;
        public event Action<WeaponStats> ForgeCompleted;

        private void Awake()
        {
            if (furnaceController == null) furnaceController = GetComponentInChildren<FurnaceController>() ?? GetComponent<FurnaceController>();
            if (pressController == null) pressController = GetComponentInChildren<PressController>() ?? GetComponent<PressController>();
            if (anvilController == null) anvilController = GetComponentInChildren<AnvilController>() ?? GetComponent<AnvilController>();
            if (assemblyController == null) assemblyController = GetComponentInChildren<WeaponAssemblyController>() ?? GetComponent<WeaponAssemblyController>();
        }

        private void OnEnable()
        {
            if (furnaceController != null) furnaceController.SmeltCompleted += OnSmeltCompleted;
            if (pressController != null) pressController.PressCompleted += OnPressCompleted;
            if (anvilController != null)
            {
                anvilController.ReadyForQuenching += OnReadyForQuenching;
                anvilController.ForgingCompleted += OnForgingCompleted;
            }
        }

        private void OnDisable()
        {
            if (furnaceController != null) furnaceController.SmeltCompleted -= OnSmeltCompleted;
            if (pressController != null) pressController.PressCompleted -= OnPressCompleted;
            if (anvilController != null)
            {
                anvilController.ReadyForQuenching -= OnReadyForQuenching;
                anvilController.ForgingCompleted -= OnForgingCompleted;
            }
        }

        public void StartForge()
        {
            if (currentPhase == ForgePhase.RawMaterial)
                ChangePhase(ForgePhase.Furnace);
        }

        public bool SelectWeaponType(WeaponType weaponType)
        {
            selectedWeaponType = weaponType;
            if (currentPhase == ForgePhase.WeaponSelection)
            {
                ChangePhase(ForgePhase.Anvil);
                anvilController?.PlaceIngot(_activeIngot, selectedWeaponType);
                return true;
            }
            return false;
        }

        public bool SelectHandle(HandleLength length)
        {
            selectedHandleLength = length;
            if (currentPhase == ForgePhase.HandleAssembly)
            {
                CompleteAssembly();
                return true;
            }
            return false;
        }

        private void OnSmeltCompleted(BloomData bloom)
        {
            _activeBloom = bloom;
            ChangePhase(ForgePhase.Press);
            // La inserción ahora la gestiona el jugador desde su inventario en la estación de prensa
        }

        private void OnPressCompleted(IngotData ingot)
        {
            _activeIngot = ingot;
            _activeBloom = null;
            ChangePhase(ForgePhase.WeaponSelection);
        }

        private void OnReadyForQuenching()
        {
            ChangePhase(ForgePhase.Quenching);
        }

        private void OnForgingCompleted()
        {
            ChangePhase(ForgePhase.HandleAssembly);
        }

        private void CompleteAssembly()
        {
            if (assemblyController != null && _activeIngot != null)
            {
                HandleData handle = HandleData.Create(selectedHandleLength);
                _completedWeapon = assemblyController.AssembleWeapon(_activeIngot, selectedWeaponType, handle);
                ChangePhase(ForgePhase.Completed);
                ForgeCompleted?.Invoke(_completedWeapon);
            }
        }

        public void ResetForge()
        {
            _activeBloom = null;
            _activeIngot = null;
            _completedWeapon = null;
            furnaceController?.ResetFurnace();
            ChangePhase(ForgePhase.RawMaterial);
        }

        public void ChangePhase(ForgePhase nextPhase)
        {
            if (currentPhase == nextPhase) return;
            currentPhase = nextPhase;
            PhaseChanged?.Invoke(currentPhase);
        }
    }
}
