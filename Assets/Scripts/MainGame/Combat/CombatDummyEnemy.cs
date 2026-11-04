using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Componente de compatibilidad para el prototipo previo de dummy de combate.
    /// La fauna hostil definitiva reside en CaveCreatureBase, GolemEnemyController y SpiderEnemyController.
    /// </summary>
    public partial class CombatDummyEnemy : MonoBehaviour, IDamageable
    {
        public static CombatDummyEnemy Instance { get; private set; }

        [SerializeField] private int maxHealth = 300;
        [SerializeField] private int currentHealth = 300;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public float RespawnTimeRemaining => 0f;
        public bool IsPlayerDetected => false;
        public float DetectionRadius => 8f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            currentHealth = maxHealth;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void TakeDamage(int amount)
        {
            currentHealth = Mathf.Max(0, currentHealth - amount);
        }
    }
}
