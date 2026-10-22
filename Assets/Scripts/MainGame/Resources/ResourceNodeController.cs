using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.Resources
{
    public class ResourceNodeController : MonoBehaviour, IDamageable
    {
        [Header("Item Drop Configuration")]
        [SerializeField] private string itemId = "Iron";
        [SerializeField] private int amount = 1;
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private GameObject droppedItemPrefab;

        [Header("Mining Requirements")]
        [SerializeField] private bool requiresPickaxe = true;

        private int _currentHealth;
        private bool _destroyed;

        public bool IsAlive => !_destroyed && _currentHealth > 0;
        public string ItemId => itemId;
        public int CurrentHealth => _currentHealth;

        private void Awake()
        {
            _currentHealth = Mathf.Max(1, maxHealth);
        }

        public void TakeDamage(int damage)
        {
            TakeDamageFrom(damage, null);
        }

        public void TakeDamageFrom(int damage, GameObject attacker)
        {
            if (!IsAlive || damage <= 0)
                return;

            if (requiresPickaxe && attacker != null)
            {
                AttackController attack = attacker.GetComponentInParent<AttackController>() ??
                                          attacker.GetComponentInChildren<AttackController>();

                if (attack != null && !attack.HasPickaxeEquipped)
                {
                    Debug.Log($"[ResourceNode] Se requiere un pico para extraer {itemId}.");
                    return;
                }
            }

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            Debug.Log($"[ResourceNode] {itemId} recibió {damage} de daño. Salud: {_currentHealth}/{maxHealth}");

            if (_currentHealth > 0)
                return;

            _destroyed = true;
            SpawnDrop();
            Debug.Log($"[ResourceNode] Veta destruida. Drop generado: {itemId} x{amount}");
            Destroy(gameObject);
        }

        private void SpawnDrop()
        {
            if (droppedItemPrefab == null)
                return;

            // Instancia el drop ligeramente por encima de la veta destruida
            Vector3 dropPosition = transform.position + Vector3.up * 0.3f;
            GameObject drop = Instantiate(droppedItemPrefab, dropPosition, Quaternion.identity);

            DroppedItemController droppedItem = drop.GetComponent<DroppedItemController>();
            if (droppedItem != null)
            {
                droppedItem.Initialize(itemId, amount);
            }
            else
            {
                Debug.LogWarning("[ResourceNode] El prefab de drop no tiene DroppedItemController.", drop);
            }
        }
    }
}
