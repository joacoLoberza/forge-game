using System;
using UnityEngine;

namespace ForjaGame.Forge
{
    public enum BellowsPumpQuality
    {
        Weak,
        Good,
        Perfect
    }

    /// <summary>
    /// Métodos de control térmico activo: combustión natural de carbón (~700°C),
    /// fuelle rítmico de oxígeno e inyección de aire para forja (~1200°C) y ventilación (SRP).
    /// </summary>
    public partial class FurnaceController
    {
        public const float CoalPassiveLimit = 700f;
        public const float BellowsFullChargeTime = 1.15f;

        [System.NonSerialized] private float _bellowsCharge = 1.0f;

        public float BellowsCharge => Mathf.Clamp01(_bellowsCharge);
        public bool IsBellowsReady => _bellowsCharge >= 0.85f;
        public bool IsAtCoalLimit => currentTemperature >= (CoalPassiveLimit - 20f);

        public event Action<BellowsPumpQuality, float> BellowsPumped;

        public void UpdateBellowsCharge(float deltaTime)
        {
            if (_bellowsCharge < 1.0f)
            {
                _bellowsCharge = Mathf.MoveTowards(_bellowsCharge, 1.0f, deltaTime / BellowsFullChargeTime);
            }
        }

        /// <summary>
        /// Insufla aire con el fuelle rítmico. Si el fuelle está lleno (>=85%), produce una inyección masiva de oxígeno (+125°C).
        /// </summary>
        public BellowsPumpQuality PumpRhythmicBellows(out float heatGained)
        {
            float charge = _bellowsCharge;
            _bellowsCharge = 0.05f; // Reinicia el ciclo de llenado del fuelle

            BellowsPumpQuality quality;
            if (charge >= 0.85f)
            {
                quality = BellowsPumpQuality.Perfect;
                heatGained = 125f;
                currentTemperature = Mathf.Min(maxTemperature, currentTemperature + 45f);
            }
            else if (charge >= 0.50f)
            {
                quality = BellowsPumpQuality.Good;
                heatGained = 65f;
                currentTemperature = Mathf.Min(maxTemperature, currentTemperature + 20f);
            }
            else
            {
                quality = BellowsPumpQuality.Weak;
                heatGained = 20f;
                currentTemperature = Mathf.Min(maxTemperature, currentTemperature + 5f);
            }

            targetTemperature = Mathf.Min(maxTemperature, targetTemperature + heatGained);
            ForjaGame.Audio.ForgeAudioSystem.PlayFurnaceStoke(transform.position);
            BellowsPumped?.Invoke(quality, heatGained);
            return quality;
        }

        public bool PumpBellows()
        {
            PumpRhythmicBellows(out _);
            return true;
        }

        /// <summary>
        /// Acciona el fuelle de enfriamiento rápido: baja la temperatura a velocidad constante (440°C/s).
        /// </summary>
        public void ApplyBellowsCooling(float deltaTime, float intensity = 1f)
        {
            float coolingForce = 440f * intensity;
            targetTemperature = Mathf.Max(ambientTemperature, targetTemperature - (coolingForce * deltaTime));
            currentTemperature = Mathf.MoveTowards(currentTemperature, ambientTemperature, coolingForce * deltaTime);
            TemperatureChanged?.Invoke(currentTemperature);
        }

        /// <summary>
        /// Pulso suave del fuelle al pulsar [Espacio] o hacer clic: frenada controlada y predecible.
        /// </summary>
        public void PulseBellows(float burst = 45f)
        {
            targetTemperature = Mathf.Max(ambientTemperature, targetTemperature - burst);
            currentTemperature = Mathf.MoveTowards(currentTemperature, ambientTemperature, burst * 0.7f);
            TemperatureChanged?.Invoke(currentTemperature);
        }

        public void VentFurnace(float intensity = 1f)
        {
            ApplyBellowsCooling(Time.deltaTime, intensity);
        }

        /// <summary>
        /// Añade carbón al fogón de forma constante y predecible (+140°C al objetivo). La aguja sube a velocidad constante (260°C/s).
        /// </summary>
        public bool AddCoal(int count = 1)
        {
            if (count <= 0) return false;
            float addedHeat = coalHeatBonus * count;
            targetTemperature = Mathf.Min(maxTemperature, Mathf.Max(currentTemperature, targetTemperature) + addedHeat);
            ForjaGame.Audio.ForgeAudioSystem.PlayFurnaceStoke(transform.position);
            return true;
        }

        public void AddFuel(IFuelSource fuel)
        {
            if (fuel == null || fuel.HeatPower <= 0f) return;
            targetTemperature = Mathf.Min(maxTemperature, Mathf.Max(currentTemperature, targetTemperature) + fuel.HeatPower);
        }
    }
}
