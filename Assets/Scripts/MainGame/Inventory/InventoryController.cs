using System;
using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Inventory
{
    public partial class InventoryController : MonoBehaviour, IInventoryReceiver
    {
        [Header("Starter Workshop Supplies")]
        [SerializeField] private bool giveStarterSuppliesOnStart = true;

        private readonly Dictionary<string, int> _items = new();
        private readonly List<BloomData> _blooms = new();
        private readonly List<IngotData> _ingots = new();
        private readonly List<HandleData> _handles = new();
        private readonly List<WeaponStats> _weapons = new();

        public static InventoryController LocalInstance { get; set; }
        private static InventoryController _fallbackInstance;
        public static InventoryController Instance
        {
            get => LocalInstance != null ? LocalInstance : _fallbackInstance;
            set => _fallbackInstance = value;
        }

        public IReadOnlyList<BloomData> Blooms => _blooms;
        public IReadOnlyList<IngotData> Ingots => _ingots;
        public IReadOnlyList<HandleData> Handles => _handles;
        public IReadOnlyList<WeaponStats> Weapons => _weapons;
        public IReadOnlyDictionary<string, int> RawMaterials => _items;
        public event Action<string, int> InventoryChanged;
        public event Action InventoryUpdated;

        private void Awake()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || netObj.IsOwner) LocalInstance = this;
            _fallbackInstance = this;
        }

        private void OnDestroy()
        {
            if (LocalInstance == this) LocalInstance = null;
        }

        private void Start()
        {
            if (giveStarterSuppliesOnStart)
                InitializeDefaultInventory();
        }

        private void Update()
        {
            UpdateCaveDelveState();
        }

        public bool AddItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return false;

            if (!_items.ContainsKey(itemId))
                _items[itemId] = 0;

            _items[itemId] += amount;
            TrackDelveLootIfInCave(itemId, amount);

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

        public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_items);

        public void AddBloom(BloomData bloom)
        {
            if (bloom == null) return;
            _blooms.Add(bloom);
            InventoryUpdated?.Invoke();
        }

        public bool RemoveBloom(BloomData bloom)
        {
            if (bloom == null || !_blooms.Remove(bloom)) return false;
            InventoryUpdated?.Invoke();
            return true;
        }

        public void AddIngot(IngotData ingot)
        {
            if (ingot == null) return;
            _ingots.Add(ingot);
            InventoryUpdated?.Invoke();
        }

        public bool RemoveIngot(IngotData ingot)
        {
            if (ingot == null || !_ingots.Remove(ingot)) return false;
            InventoryUpdated?.Invoke();
            return true;
        }

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
            _delveHarvestedItems.Clear();
            InventoryChanged?.Invoke(string.Empty, 0);
            InventoryUpdated?.Invoke();
        }
    }
}
