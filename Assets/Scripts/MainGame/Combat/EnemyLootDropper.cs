using System;
using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    [Serializable]
    public class LootDropEntry
    {
        public string itemId = "Iron";
        [Range(0f, 1f)] public float dropChance = 0.70f;
        public int minAmount = 1;
        public int maxAmount = 2;

        public LootDropEntry() { }

        public LootDropEntry(string id, float chance, int min, int max)
        {
            itemId = id;
            dropChance = chance;
            minAmount = min;
            maxAmount = max;
        }
    }

    /// <summary>
    /// Componente de tabla de botín (loot table) para enemigos.
    /// Al morir el enemigo, dispersa ítems coleccionables en el suelo con impulso físico.
    /// </summary>
    public class EnemyLootDropper : MonoBehaviour
    {
        [Header("Tabla de Botín")]
        [SerializeField] private List<LootDropEntry> lootTable = new();
        [SerializeField] private float burstForce = 3.2f;

        private void Awake()
        {
            if (lootTable == null || lootTable.Count == 0)
            {
                // Tabla de botín por defecto si no ha sido configurada en inspector
                lootTable = new List<LootDropEntry>
                {
                    new LootDropEntry("Iron", 0.75f, 1, 2),
                    new LootDropEntry("Coal", 0.65f, 2, 4),
                    new LootDropEntry("Copper", 0.50f, 1, 2),
                    new LootDropEntry("Gold", 0.15f, 1, 1)
                };
            }
            lootTable.RemoveAll(e => e == null || e.itemId == "MonsterBones");
        }

        public void SetLootTable(List<LootDropEntry> newTable)
        {
            if (newTable != null)
            {
                lootTable = newTable;
                lootTable.RemoveAll(e => e == null || e.itemId == "MonsterBones");
            }
        }

        public void DropLoot(Vector3 position)
        {
            if (lootTable == null || lootTable.Count == 0) return;
            lootTable.RemoveAll(e => e == null || e.itemId == "MonsterBones");

            foreach (var entry in lootTable)
            {
                if (UnityEngine.Random.value <= entry.dropChance)
                {
                    int qty = UnityEngine.Random.Range(entry.minAmount, entry.maxAmount + 1);
                    if (qty <= 0) continue;

                    Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.6f, 1.8f);
                    Vector3 dropPos = position + new Vector3(randomCircle.x, 0.35f, randomCircle.y);
                    Vector3 popVel = new Vector3(randomCircle.x * burstForce, UnityEngine.Random.Range(2.5f, 4.2f), randomCircle.y * burstForce);

                    DroppedItemController.Spawn(dropPos, entry.itemId, qty, popVel);
                    Debug.Log($"<color=#FFD700>[Loot Enemigo]</color> Drop generado: {entry.itemId} x{qty}");
                }
            }
        }
    }
}
