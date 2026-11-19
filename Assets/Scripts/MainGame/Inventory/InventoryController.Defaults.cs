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

        public void EnsureStarterSupplies()
        {
            if (_weapons != null && _weapons.Count > 0) return;
            InitializeDefaultInventory();
        }

        public void InitializeDefaultInventory()
        {
            _suppressDelveTracking = true;

            // Suministros limpios del taller: carbón base abundante para encender y calibrar la fragua,
            // una pequeña reserva de cobre bruto, mangos de madera sin tallar y el pico de minería.
            AddItem("Coal", 12);
            AddItem("Copper", 3);

            for (int i = 0; i < 4; i++)
                AddHandle(HandleData.CreateDefault());

            bool alreadyHasPickaxe = false;
            if (_weapons != null)
            {
                for (int i = 0; i < _weapons.Count; i++)
                {
                    if (_weapons[i] != null && _weapons[i].weaponType == WeaponType.Pickaxe)
                    {
                        alreadyHasPickaxe = true;
                        break;
                    }
                }
            }

            if (!alreadyHasPickaxe)
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
        /// Suelta físicamente en el suelo (como los enemigos) los minerales recolectados al morir en combate.
        /// </summary>
        public string ForfeitCurrentDelveLoot(Vector3? dropOrigin = null)
        {
            _isInCaveDelve = false;
            var toDrop = new Dictionary<string, int>();
            if (_delveHarvestedItems.Count > 0)
            {
                foreach (var kv in _delveHarvestedItems) if (kv.Value > 0) toDrop[kv.Key] = kv.Value;
            }
            else
            {
                foreach (var kv in _items) if (kv.Value > 0) toDrop[kv.Key] = kv.Value;
            }
            if (toDrop.Count == 0) return "No ores carried when defeated.";

            Vector3 origin = dropOrigin ?? transform.position;
            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in toDrop)
            {
                RemoveItem(kv.Key, kv.Value);
                SpawnScatteredOreDrops(origin, kv.Key, kv.Value);
                if (!first) sb.Append(", ");
                sb.Append($"{TranslateResourceName(kv.Key)} x{kv.Value}");
                first = false;
            }
            _delveHarvestedItems.Clear();
            return first ? "No ores carried." : $"Ores dropped at your fall: {sb}.";
        }

        private static void SpawnScatteredOreDrops(Vector3 origin, string itemId, int totalQty)
        {
            int rem = totalQty;
            while (rem > 0)
            {
                int batch = rem > 3 ? UnityEngine.Random.Range(1, 4) : rem;
                rem -= batch;
                Vector2 dir2 = UnityEngine.Random.insideUnitCircle;
                if (dir2.sqrMagnitude < 0.05f) dir2 = Vector2.right;
                dir2.Normalize();
                Vector3 dropPos = origin + new Vector3(dir2.x * UnityEngine.Random.Range(0.9f, 2.1f), 0.40f, dir2.y * UnityEngine.Random.Range(0.9f, 2.1f));
                Vector3 popVel = new Vector3(dir2.x * 3.4f, UnityEngine.Random.Range(2.6f, 4.4f), dir2.y * 3.4f);
                ForjaGame.Resources.DroppedItemController.Spawn(dropPos, itemId, batch, popVel);
            }
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
            string securedMsg = securedCount > 0 ? $"Delve loot safely secured ({securedCount} ores). " : "";
            return $"{securedMsg}Sanctum Reward: Master Golden Steel Ingot, Iron x6, Gold x4, Coal x12{weaponBlessing}";
        }

        private string BlessEquippedForgedWeapon()
        {
            var equip = GetComponent<PlayerEquipmentController>();
            WeaponStats active = equip != null ? equip.EquippedWeapon : null;
            if (active == null || active.weaponType == WeaponType.Pickaxe) return ".";

            if (active.qualityPrefix != "Consecrated")
            {
                active.baseDamage = Mathf.Round(active.baseDamage * 1.18f);
                active.maxSharpness = Mathf.Max(active.maxSharpness, 110f);
                active.currentSharpness = active.maxSharpness;
                active.quality = Mathf.Clamp01(active.quality + 0.10f);
                active.qualityPrefix = "Consecrated";
                InventoryUpdated?.Invoke();
                return $" and your {active.weaponName} was Consecrated (+18% Damage)!";
            }

            active.currentSharpness = active.maxSharpness;
            InventoryUpdated?.Invoke();
            return " and your weapon's edge was restored to peak sharpness.";
        }

        private static string TranslateResourceName(string id) => id;
    }
}
