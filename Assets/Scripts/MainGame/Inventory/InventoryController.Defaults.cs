using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Forge;

namespace ForjaGame.Inventory
{
    /// <summary>
    /// Suministros limpios de inicio (sin objetos de depuración/testeo) y gestión del botín
    /// en riesgo durante las incursiones a la caverna y recompensas del Altar de las Profundidades.
    /// </summary>
    public partial class InventoryController
    {
        private const float CaveEntryThresholdZ = 25.0f;
        private const float SafeSurfaceBankZ = 16.5f;

        private readonly Dictionary<string, int> _delveHarvestedItems = new();
        private bool _isInCaveDelve;
        private bool _suppressDelveTracking;

        public bool IsInCaveDelve => _isInCaveDelve;

        public int UnbankedDelveItemCount
        {
            get
            {
                int total = 0;
                foreach (var kv in _delveHarvestedItems) total += kv.Value;
                return total;
            }
        }

        private void InitializeDefaultInventory()
        {
            _suppressDelveTracking = true;

            // Suministros limpios del taller: carbón base para encender la fragua,
            // una pequeña reserva de cobre bruto, mangos de madera sin tallar y el pico de minería.
            AddItem("Coal", 6);
            AddItem("Copper", 3);

            for (int i = 0; i < 4; i++)
                AddHandle(HandleData.CreateDefault());

            AddWeapon(PickaxeModelGenerator.CreateDefaultIronPickaxe());

            _suppressDelveTracking = false;
            _delveHarvestedItems.Clear();
        }

        private void UpdateCaveDelveState()
        {
            float z = transform.position.z;
            if (!_isInCaveDelve && z >= CaveEntryThresholdZ)
            {
                _isInCaveDelve = true;
            }
            else if (_isInCaveDelve && z <= SafeSurfaceBankZ)
            {
                var health = GetComponent<PlayerHealthController>();
                if (health == null || health.IsAlive)
                {
                    _isInCaveDelve = false;
                    _delveHarvestedItems.Clear();
                }
            }
        }

        private void TrackDelveLootIfInCave(string itemId, int amount)
        {
            if (_suppressDelveTracking) return;
            if (_isInCaveDelve || transform.position.z >= CaveEntryThresholdZ)
            {
                _isInCaveDelve = true;
                if (!_delveHarvestedItems.ContainsKey(itemId))
                    _delveHarvestedItems[itemId] = 0;
                _delveHarvestedItems[itemId] += amount;
            }
        }

        /// <summary>
        /// Pierde estrictamente todos los minerales y despojos cosechados durante la incursión actual al caer en combate.
        /// </summary>
        public string ForfeitCurrentDelveLoot()
        {
            _isInCaveDelve = false;
            if (_delveHarvestedItems.Count == 0)
                return "No llevabas minerales sin asegurar de esta incursión.";

            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in _delveHarvestedItems)
            {
                if (kv.Value <= 0) continue;
                RemoveItem(kv.Key, kv.Value);
                if (!first) sb.Append(", ");
                sb.Append($"{TranslateResourceName(kv.Key)} x{kv.Value}");
                first = false;
            }
            _delveHarvestedItems.Clear();
            return first ? "No llevabas minerales sin asegurar." : $"Botín perdido en la caverna: {sb}.";
        }

        /// <summary>
        /// Asegura todo el botín recolectado y otorga la recompensa maestra del Altar de las Profundidades.
        /// </summary>
        public string ClaimDeepAltarReward()
        {
            int securedCount = UnbankedDelveItemCount;
            _isInCaveDelve = false;
            _delveHarvestedItems.Clear();

            _suppressDelveTracking = true;
            AddItem("Gold", 4);
            AddItem("Iron", 6);
            AddItem("Coal", 12);
            AddHandle(HandleData.CreateDefault());
            AddHandle(HandleData.CreateDefault());

            var masterAlloy = AlloyComposition.FromOres(new Dictionary<MetalType, int>
            {
                { MetalType.Iron, 6 },
                { MetalType.Gold, 3 }
            });
            AddIngot(new IngotData(ItemSize.Large, masterAlloy, 1.0f));
            _suppressDelveTracking = false;

            string weaponBlessing = BlessEquippedForgedWeapon();
            string securedMsg = securedCount > 0 ? $"Botín asegurado intacto ({securedCount} minerales). " : "";
            return $"{securedMsg}Recompensa del Altar: Lingote Grande de Acero Áureo, Hierro x6, Oro x4, Carbón x12{weaponBlessing}";
        }

        private string BlessEquippedForgedWeapon()
        {
            var equip = GetComponent<PlayerEquipmentController>();
            WeaponStats active = equip != null ? equip.EquippedWeapon : null;
            if (active == null || active.weaponType == WeaponType.Pickaxe) return ".";

            if (active.qualityPrefix != "Consagrada")
            {
                active.baseDamage = Mathf.Round(active.baseDamage * 1.18f);
                active.maxSharpness = Mathf.Max(active.maxSharpness, 110f);
                active.currentSharpness = active.maxSharpness;
                active.quality = Mathf.Clamp01(active.quality + 0.10f);
                active.qualityPrefix = "Consagrada";
                InventoryUpdated?.Invoke();
                return $" y tu {active.weaponName} fue Consagrada (+18% Daño)!";
            }

            active.currentSharpness = active.maxSharpness;
            InventoryUpdated?.Invoke();
            return " y el filo de tu arma fue restaurado al máximo.";
        }

        private static string TranslateResourceName(string id) => id switch
        {
            "Coal" => "Carbón",
            "Copper" => "Cobre",
            "Iron" => "Hierro",
            "Gold" => "Oro",
            _ => id
        };
    }
}
