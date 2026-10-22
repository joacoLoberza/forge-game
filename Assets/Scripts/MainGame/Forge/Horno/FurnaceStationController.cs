using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Estación física 3D del Horno.
    /// Hereda de BaseForgeStationController (LSP, OCP, SRP).
    /// </summary>
    public class FurnaceStationController : BaseForgeStationController
    {
        [Header("Referencias de Horno")]
        [SerializeField] private FurnaceController furnaceController;
        [SerializeField] private GameObject furnaceUI;

        public FurnaceController Furnace => furnaceController;
        public override string InteractionPrompt => "Horno de Fundición";

        private void Awake()
        {
            if (furnaceController == null)
                furnaceController = GetComponentInChildren<FurnaceController>() ?? GetComponent<FurnaceController>();

            // Compatibilidad hacia atrás si furnaceUI estaba asignado en el Inspector
            if (stationUI == null && furnaceUI != null)
                stationUI = furnaceUI;
            else if (furnaceUI == null && stationUI != null)
                furnaceUI = stationUI;
        }
    }
}
