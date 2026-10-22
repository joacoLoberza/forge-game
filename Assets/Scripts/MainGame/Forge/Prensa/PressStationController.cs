using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Estación física 3D de la Prensa Mecánica.
    /// Hereda de BaseForgeStationController (LSP, OCP, SRP).
    /// </summary>
    public class PressStationController : BaseForgeStationController
    {
        [Header("Referencias de Prensa")]
        [SerializeField] private PressController pressController;
        [SerializeField] private GameObject pressUI;

        public PressController Press => pressController;
        public override string InteractionPrompt => "Operar Prensa";

        private void Awake()
        {
            if (pressController == null)
                pressController = GetComponentInChildren<PressController>() ?? GetComponent<PressController>();

            if (stationUI == null && pressUI != null)
                stationUI = pressUI;
            else if (pressUI == null && stationUI != null)
                pressUI = stationUI;

            if (stationUI == null)
            {
                var panel = FindFirstObjectByType<UI.PressPanel>(FindObjectsInactive.Include);
                if (panel != null)
                {
                    stationUI = panel.gameObject;
                    pressUI = stationUI;
                }
            }
        }
    }
}
