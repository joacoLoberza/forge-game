using System;
using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Inventory
{
    public class InventoryController : MonoBehaviour, IInventoryReceiver
    {
        [Header("Test / Debug Default Items")]
        [SerializeField] private bool giveDefaultOresOnStart = true;
        [SerializeField] private int defaultOreAmount = 9;

        private readonly Dictionary<string, int> _items = new();
        private readonly List<BloomData> _blooms = new();
        private readonly List<IngotData> _ingots = new();
        private readonly List<HandleData> _handles = new();
        private readonly List<WeaponStats> _weapons = new();

        public IReadOnlyList<BloomData> Blooms => _blooms;
        public IReadOnlyList<IngotData> Ingots => _ingots;
        public IReadOnlyList<HandleData> Handles => _handles;
        public IReadOnlyList<WeaponStats> Weapons => _weapons;
        public IReadOnlyDictionary<string, int> RawMaterials => _items;

        public event Action<string, int> InventoryChanged;
        public event Action InventoryUpdated;

        private void Start()
        {
            if (giveDefaultOresOnStart)
            {
                AddItem("Iron", defaultOreAmount);
                AddItem("Copper", defaultOreAmount);
                AddItem("Gold", defaultOreAmount);
                AddItem("Coal", 80); // 80 base coal for testing

                // 9 mangos de madera base para crafteo
                for (int i = 0; i < 9; i++) AddHandle(HandleData.CreateDefault());

                // Test Blooms: Small Copper (Level 1) and Large Iron (Level 10)
                for (int i = 0; i < 3; i++)
                {
                    var comp = AlloyComposition.FromOres(new Dictionary<MetalType, int> { { MetalType.Copper, 3 } });
                    AddBloom(new BloomData(ItemSize.Small, comp, 1f));
                }
                for (int i = 0; i < 2; i++)
                {
                    var comp = AlloyComposition.FromOres(new Dictionary<MetalType, int> { { MetalType.Iron, 9 } });
                    AddBloom(new BloomData(ItemSize.Large, comp, 1f));
                }

                // Sample test ingots for testing Anvil forging directly
                var ironComp = AlloyComposition.FromOres(new Dictionary<MetalType, int> { { MetalType.Iron, 6 } });
                AddIngot(new IngotData(ItemSize.Medium, ironComp, 1f));

                var copperComp = AlloyComposition.FromOres(new Dictionary<MetalType, int> { { MetalType.Copper, 3 } });
                AddIngot(new IngotData(ItemSize.Small, copperComp, 1f));

                // Armas de prueba con posturas y combos claramente diferenciados
                AddWeapon(new WeaponStats
                {
                    weaponName = "Espada Ligera de Cobre (1H)",
                    weaponType = WeaponType.Sword,
                    ingotSize = ItemSize.Small,
                    handleLength = HandleLength.Short,
                    composition = copperComp,
                    baseDamage = 18f, attackSpeed = 1.55f, attackRange = 1.9f,
                    maxSharpness = 80f, currentSharpness = 80f, quality = 0.95f, qualityPrefix = "Ágil"
                });

                AddWeapon(new WeaponStats
                {
                    weaponName = "Espada de Hierro (1H)",
                    weaponType = WeaponType.Sword,
                    ingotSize = ItemSize.Medium,
                    handleLength = HandleLength.Medium,
                    composition = ironComp,
                    baseDamage = 25f, attackSpeed = 1.20f, attackRange = 2.2f,
                    maxSharpness = 100f, currentSharpness = 100f, quality = 0.95f, qualityPrefix = "Excelente"
                });

                AddWeapon(new WeaponStats
                {
                    weaponName = "Mandoble Pesado (2H)",
                    weaponType = WeaponType.Sword,
                    ingotSize = ItemSize.Large,
                    handleLength = HandleLength.Long,
                    composition = ironComp,
                    baseDamage = 55f, attackSpeed = 0.82f, attackRange = 3.2f,
                    maxSharpness = 100f, currentSharpness = 100f, quality = 0.98f, qualityPrefix = "Maestro"
                });

                AddWeapon(new WeaponStats
                {
                    weaponName = "Martillo de Guerra (1H)",
                    weaponType = WeaponType.Hammer,
                    ingotSize = ItemSize.Medium,
                    handleLength = HandleLength.Medium,
                    composition = ironComp,
                    baseDamage = 38f, attackSpeed = 0.92f, attackRange = 2.0f,
                    maxSharpness = 100f, currentSharpness = 100f, quality = 0.90f, qualityPrefix = "Forjado"
                });

                AddWeapon(new WeaponStats
                {
                    weaponName = "Lanza de Hoja (Polearm)",
                    weaponType = WeaponType.Sword,
                    ingotSize = ItemSize.Medium,
                    handleLength = HandleLength.Long,
                    composition = ironComp,
                    baseDamage = 32f, attackSpeed = 1.05f, attackRange = 3.6f,
                    maxSharpness = 90f, currentSharpness = 90f, quality = 0.90f, qualityPrefix = "Artesanal"
                });
            }
        }

        public bool AddItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return false;

            if (!_items.ContainsKey(itemId))
                _items[itemId] = 0;

            _items[itemId] += amount;
            InventoryChanged?.Invoke(itemId, _items[itemId]);
            InventoryUpdated?.Invoke();
            return true;
        }

        public bool RemoveItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return false;

            if (!_items.TryGetValue(itemId, out int currentAmount))
                return false;

            int nextAmount = currentAmount - amount;
            if (nextAmount <= 0)
            {
                _items.Remove(itemId);
                InventoryChanged?.Invoke(itemId, 0);
                InventoryUpdated?.Invoke();
                return true;
            }

            _items[itemId] = nextAmount;
            InventoryChanged?.Invoke(itemId, nextAmount);
            InventoryUpdated?.Invoke();
            return true;
        }

        public bool HasItem(string itemId, int requiredAmount = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || requiredAmount <= 0)
                return false;

            return _items.TryGetValue(itemId, out int currentAmount) && currentAmount >= requiredAmount;
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;

            return _items.TryGetValue(itemId, out int currentAmount) ? currentAmount : 0;
        }

        public IReadOnlyDictionary<string, int> Snapshot()
        {
            return new Dictionary<string, int>(_items);
        }

        // --- Blooms Management ---
        public void AddBloom(BloomData bloom)
        {
            if (bloom == null) return;
            _blooms.Add(bloom);
            InventoryUpdated?.Invoke();
        }

        public bool RemoveBloom(BloomData bloom)
        {
            if (bloom == null) return false;
            if (_blooms.Remove(bloom))
            {
                InventoryUpdated?.Invoke();
                return true;
            }
            return false;
        }

        // --- Ingots Management ---
        public void AddIngot(IngotData ingot)
        {
            if (ingot == null) return;
            _ingots.Add(ingot);
            InventoryUpdated?.Invoke();
        }

        public bool RemoveIngot(IngotData ingot)
        {
            if (ingot == null) return false;
            if (_ingots.Remove(ingot))
            {
                InventoryUpdated?.Invoke();
                return true;
            }
            return false;
        }

        // --- Handles & Weapons Management ---
        public void AddHandle(HandleData handle) { if (handle == null) return; _handles.Add(handle); InventoryUpdated?.Invoke(); }
        public bool RemoveHandle(HandleData handle) { if (handle != null && _handles.Remove(handle)) { InventoryUpdated?.Invoke(); return true; } return false; }
        public bool ConsumeHandle() { if (_handles.Count > 0) { _handles.RemoveAt(_handles.Count - 1); InventoryUpdated?.Invoke(); return true; } return false; }
        public void AddWeapon(WeaponStats weapon) { if (weapon == null) return; _weapons.Add(weapon); InventoryUpdated?.Invoke(); }
        public bool RemoveWeapon(WeaponStats weapon) { if (weapon != null && _weapons.Remove(weapon)) { InventoryUpdated?.Invoke(); return true; } return false; }

        public void Clear()
        {
            _items.Clear();
            _blooms.Clear();
            _ingots.Clear();
            _handles.Clear();
            _weapons.Clear();
            InventoryChanged?.Invoke(string.Empty, 0);
            InventoryUpdated?.Invoke();
        }
    }
}
