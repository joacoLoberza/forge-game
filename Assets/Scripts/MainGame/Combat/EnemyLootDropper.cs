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
                lootTable = BuildAreaLootTable(transform.position.z);
            lootTable.RemoveAll(e => e == null || e.itemId == "MonsterBones");
        }

        public static List<LootDropEntry> BuildAreaLootTable(float zPos, string preferredOre = null, bool isElite = false)
        {
            var table = new List<LootDropEntry>();
            if (zPos < 145f)
            {
                // Zona inicial (Entrada, Cámara 1 y comienzo de Cámara 2): únicamente Carbón y Cobre
                table.Add(new LootDropEntry("Coal", preferredOre == "Coal" ? 0.90f : 0.72f, 1, 3));
                table.Add(new LootDropEntry("Copper", preferredOre == "Copper" ? 0.90f : 0.62f, 1, 2));
            }
            else if (zPos < 245f)
            {
                // Zona intermedia (Fondo Cámara 2 y Cámara 3 entrada): Carbón, Cobre y Hierro (sin Oro)
                table.Add(new LootDropEntry("Coal", 0.52f, 1, 2));
                table.Add(new LootDropEntry("Copper", preferredOre == "Copper" ? 0.85f : 0.58f, 1, 2));
                table.Add(new LootDropEntry("Iron", preferredOre == "Iron" ? 0.92f : 0.68f, 1, 2));
            }
            else
            {
                // Zona profunda (Cámaras 3–6): Hierro y Oro (y Cobre residual antes de Z=290)
                table.Add(new LootDropEntry("Iron", preferredOre == "Iron" ? 0.92f : 0.75f, 1, 3));
                if (zPos < 290f) table.Add(new LootDropEntry("Copper", 0.35f, 1, 2));
                float goldChance = (preferredOre == "Gold" || isElite) ? 0.85f : 0.36f;
                table.Add(new LootDropEntry("Gold", goldChance, 1, isElite ? 2 : 1));
                table.Add(new LootDropEntry("Coal", 0.42f, 1, 2));
            }
            return table;
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
            Transform closestPlayer = CaveCreatureBase.FindClosestLivingPlayer(position);
            if (closestPlayer == null || (closestPlayer.position - position).sqrMagnitude > 30f * 30f) return;
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
