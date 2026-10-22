using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Métodos de control térmico activo: combustible (carbón), fuelle y ventilación (SRP).
    /// </summary>
    public partial class FurnaceController
    {
        /// <summary>
        /// Bombea el fuelle para dar un impulso táctil de aire caliente (+28°C objetivo, +16°C inmediato).
        /// </summary>
        public bool PumpBellows()
        {
            if (_bellowsCooldown > 0f) return false;
            _bellowsCooldown = 0.3f;

            targetTemperature = Mathf.Min(maxTemperature, targetTemperature + 28f);
            currentTemperature = Mathf.Min(maxTemperature, currentTemperature + 16f);
            return true;
        }

        /// <summary>
        /// Abre la compuerta de ventilación para enfriar el horno. El enfriamiento es mayor que el aumento por carbón.
        /// </summary>
        public void VentFurnace(float intensity = 1f)
        {
            float coolingForce = 180f * intensity;
            targetTemperature = Mathf.Max(ambientTemperature, targetTemperature - (coolingForce * 1.25f * Time.deltaTime));
            currentTemperature = Mathf.Max(ambientTemperature, currentTemperature - (coolingForce * Time.deltaTime));
        }

        /// <summary>
        /// Agrega carbón como combustible principal (desligado de si hay o no minerales).
        /// </summary>
        public void AddCoal(int count = 1)
        {
            if (count <= 0) return;
            AddFuelDirect(coalHeatBonus * count);
        }

        public void AddFuel(IFuelSource fuel)
        {
            if (fuel == null || fuel.HeatPower <= 0f) return;
            AddFuelDirect(fuel.HeatPower);
        }

        private void AddFuelDirect(float heatAmount)
        {
            targetTemperature = Mathf.Min(maxTemperature, Mathf.Max(currentTemperature, targetTemperature) + heatAmount);
        }
    }
}
