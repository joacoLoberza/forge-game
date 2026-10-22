using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI.Core;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    /// <summary>
    /// Vista de la forja. Muestra la fase que administra ForgeController.
    /// </summary>
    public class ForgeMenuPanel : UIPanelBase
    {
        [SerializeField] private ForgeZoneOption[] zones;
        [SerializeField] private Text statusLabel;
        [SerializeField] private TMP_Text statusLabelTMP;

        [SerializeField] private ForgeController forgeController;

        [Tooltip("Botón de prueba para simular que terminaste el minijuego de la fase actual.")]
        [SerializeField] private Button debugAdvanceButton;

        protected override void Awake()
        {
            base.Awake();

            if (forgeController == null)
                forgeController = FindFirstObjectByType<ForgeController>();

            if (statusLabelTMP == null)
                statusLabelTMP = GetComponentInChildren<TMP_Text>();

            for (int i = 0; i < zones.Length; i++)
            {
                int capturedIndex = i; // Evita el bug de closures compartidas en loops
                if (zones[capturedIndex].button != null)
                {
                    BindPressDown(zones[capturedIndex].button, () => SelectZone(capturedIndex));
                }
            }

            if (debugAdvanceButton != null)
                BindPressDown(debugAdvanceButton, AdvancePhase);
        }

            private void OnEnable()
            {
                if (forgeController != null)
                forgeController.PhaseChanged += OnPhaseChanged;
            }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (forgeController != null)
                forgeController.PhaseChanged -= OnPhaseChanged;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
            
            forgeController?.StartForge();
            UpdateVisuals();
        }

        private void SelectZone(int index)
        {
            if (forgeController == null || index < 0 || index >= zones.Length)
                return;

            ForgePhase selectedPhase = GetPhaseForZone(index);
            forgeController.ChangePhase(selectedPhase);
            SetStatus($"Trabajando en: {zones[index].zoneName}...");
        }

        /// <summary>
        /// Se llama cuando el jugador termina la tarea de la zona actual o pulsa debug.
        /// </summary>
        public void AdvancePhase()
        {
            if (forgeController == null) return;

            switch (forgeController.CurrentPhase)
            {
                case ForgePhase.RawMaterial:
                    forgeController.StartForge();
                    break;
                case ForgePhase.Furnace:
                    forgeController.ChangePhase(ForgePhase.Press);
                    break;
                case ForgePhase.Press:
                    forgeController.ChangePhase(ForgePhase.WeaponSelection);
                    break;
                case ForgePhase.WeaponSelection:
                    forgeController.SelectWeaponType(WeaponType.Sword);
                    break;
                case ForgePhase.Anvil:
                    forgeController.ChangePhase(ForgePhase.Quenching);
                    break;
                case ForgePhase.Quenching:
                    forgeController.ChangePhase(ForgePhase.HandleAssembly);
                    break;
                case ForgePhase.HandleAssembly:
                    forgeController.SelectHandle(HandleLength.Medium);
                    break;
            }
        }

        private void OnPhaseChanged(ForgePhase phase)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (forgeController == null || zones == null)
                return;

            ForgePhase currentPhase = forgeController.CurrentPhase;
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].highlight != null) 
                    zones[i].highlight.SetActive(GetPhaseForZone(i) == currentPhase);

                if (zones[i].button != null)
                    zones[i].button.interactable = IsZoneAvailable(GetPhaseForZone(i), currentPhase);
            }

            SetStatus(GetPhaseLabel(currentPhase));
        }

        private ForgePhase GetPhaseForZone(int index)
        {
            return index switch
            {
                0 => ForgePhase.Furnace,
                1 => ForgePhase.Press,
                _ => ForgePhase.Anvil
            };
        }

        private bool IsZoneAvailable(ForgePhase zonePhase, ForgePhase currentPhase)
        {
            return zonePhase == currentPhase ||
                   (zonePhase == ForgePhase.Furnace && currentPhase > ForgePhase.Furnace) ||
                   (zonePhase == ForgePhase.Press && currentPhase > ForgePhase.Press) ||
                   (zonePhase == ForgePhase.Anvil && currentPhase >= ForgePhase.Anvil);
        }

        private string GetPhaseLabel(ForgePhase phase)
        {
            return phase switch
            {
                ForgePhase.RawMaterial => "Current Phase: Raw Materials",
                ForgePhase.Furnace => "Current Phase: Furnace (Smelting)",
                ForgePhase.Press => "Current Phase: Mechanical Press",
                ForgePhase.WeaponSelection => "Current Phase: Weapon Selection",
                ForgePhase.Anvil => "Current Phase: Anvil (Hammering)",
                ForgePhase.Quenching => "Current Phase: Quenching",
                ForgePhase.HandleAssembly => "Current Phase: Handle Assembly",
                ForgePhase.Completed => "Weapon Finished!",
                _ => "Forge Phase"
            };
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;

            if (statusLabelTMP != null)
                statusLabelTMP.text = message;
        }
    }
}