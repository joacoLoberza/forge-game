using System.Collections;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Enemigo de prueba tipo cápsula:
    /// - Salud calibrada: 250 HP (reducida a la mitad para testing ágil).
    /// - Sin barra de vida flotante: su cuerpo (rojo y azul) se oscurece progresivamente al recibir daño.
    /// - Spawn flotante (+1.5m) en punto lejano (~25m del inicio) sobre área 2D plana.
    /// - Aggro por proximidad: radio de detección circundante (8.5m).
    /// - Aviso previo de ataque: cambia a azul exactamente 1.0 segundo antes de golpear.
    /// - Sin daño por contacto físico: el daño solo se aplica cuando ejecuta su golpe activo.
    /// </summary>
    public partial class CombatDummyEnemy : MonoBehaviour, IDamageable
    {
        public static CombatDummyEnemy Instance { get; private set; }

        [Header("Estadísticas del Enemigo")]
        [SerializeField] private int maxHealth = 250;
        [SerializeField] private int currentHealth = 250;
        [SerializeField] private int attackDamage = 15;
        [SerializeField] private float attackInterval = 2.0f; // Cada 2 segundos
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float moveSpeed = 2.2f;

        [Header("Área de Spawn en el Mapa")]
        [SerializeField] private Vector3 mapSpawnPoint = new Vector3(18.0f, 0.05f, 18.0f);
        [SerializeField] private float spawnZoneRadius = 2.2f;

        [Header("Área de Detección y Alerta (Aggro)")]
        [SerializeField] private float detectionRadius = 8.5f; // Radio donde detecta al jugador
        [SerializeField] private float deaggroRadius = 12.5f;  // Distancia para desenganchar y volver
        private bool _isPlayerDetected = false;

        private Transform _playerTransform;
        private Rigidbody _rb;
        private Renderer _renderer;
        private Collider _collider;
        private HitboxVisualizer _bodyHurtbox;
        private HitboxVisualizer _attackHitbox;

        private Material _bodyMaterial;
        private readonly Color _baseNormalColor = new Color(0.85f, 0.22f, 0.18f, 1.0f); // Terracota / Rojo vivo
        private readonly Color _baseTelegraphColor = new Color(0.15f, 0.55f, 1.0f, 1.0f); // Azul previo al ataque

        private float _nextAttackTime = 0f;
        private float _attackVisualEndTime = -1f;
        private float _bodyHurtBlackUntil = -1f;
        private float _respawnEndTime = -1f;
        private bool _isAttacking = false;
        private bool _isTelegraphing = false;

        private GameObject _floorZoneObj;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;
        public float RespawnTimeRemaining => Mathf.Max(0f, _respawnEndTime - Time.time);
        public Vector3 SpawnPoint => mapSpawnPoint;
        public float DetectionRadius => detectionRadius;
        public bool IsPlayerDetected => _isPlayerDetected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitSceneWatcher()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => CheckSceneForEnemy(scene.name);
            CheckSceneForEnemy(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private static void CheckSceneForEnemy(string sceneName)
        {
            var existing = FindObjectsByType<CombatDummyEnemy>(FindObjectsSortMode.None);
            foreach (var dummy in existing)
            {
                if (dummy != null && dummy.gameObject != null) Destroy(dummy.gameObject);
            }

            // Limpiar Golem antiguo suelto en el campamento central (ahora residen dentro de la cueva)
            GameObject strayCampGolem = GameObject.Find("GolemEnemy_Boss");
            if (strayCampGolem != null && strayCampGolem.transform.position.z < 22f)
            {
                Destroy(strayCampGolem);
            }
        }

        public static GameObject CreateDummyGameObject(Vector3 position)
        {
            GameObject golem = new GameObject("GolemEnemy_Boss");
            var ctrl = golem.AddComponent<GolemEnemyController>();
            golem.transform.position = position;
            return golem;
        }

        private void Awake()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase) || sceneName.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            currentHealth = maxHealth;

            _collider = GetComponent<Collider>();
            _renderer = GetComponent<Renderer>();
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            SetupMaterial();
            SetupHitboxes();
            Create2DFloorArea();
        }

        private void Start()
        {
            FindPlayer();
            _nextAttackTime = Time.time + attackInterval;
            RefreshCurrentColor();
        }

        private void FindPlayer()
        {
            _playerTransform = GetClosestLivingPlayerTransform();
        }

        public Transform GetClosestLivingPlayerTransform()
        {
            Transform closest = null;
            float minDist = float.PositiveInfinity;
            var list = PlayerHealthController.AllPlayers;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p != null && p.IsAlive)
                {
                    float d = Vector3.Distance(transform.position, p.transform.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        closest = p.transform;
                    }
                }
            }

            if (closest != null) return closest;

            var pHealth = PlayerHealthController.Instance;
            if (pHealth != null && pHealth.IsAlive) return pHealth.transform;

            var pMove = FindFirstObjectByType<ForjaGame.Core.PlayerMovement>();
            return pMove != null ? pMove.transform : null;
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            _isPlayerDetected = true;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            Debug.Log($"<color=orange>[Enemigo Cápsula]</color> Dañado por {amount}. Vida restante: {currentHealth}/{maxHealth}");

            // Actualizar oscurecimiento del cuerpo según salud restante
            RefreshCurrentColor();

            // Teñir hurtbox corporal de negro al recibir daño
            if (_bodyHurtbox != null)
            {
                _bodyHurtbox.IsConnecting = true;
                _bodyHurtBlackUntil = Time.time + 0.30f;
            }

            if (currentHealth <= 0)
            {
                StartCoroutine(DefeatedRoutine());
            }
        }

        private IEnumerator DefeatedRoutine()
        {
            _respawnEndTime = Time.time + 30.0f;
            GetComponent<EnemyLootDropper>()?.DropLoot(transform.position);
            Debug.Log("<color=red>[Enemigo Cápsula] Derrotado! Reapareciendo en su área 2D en 30 segundos...</color>");

            _isPlayerDetected = false;
            _isTelegraphing = false;
            _isAttacking = false;

            if (_bodyHurtbox != null) _bodyHurtbox.IsVolumeActive = false;
            if (_attackHitbox != null) _attackHitbox.IsVolumeActive = false;
            if (_renderer != null) _renderer.enabled = false;
            if (_collider != null) _collider.enabled = false;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            yield return new WaitForSeconds(30.0f);

            transform.position = mapSpawnPoint + Vector3.up * 1.5f;
            transform.rotation = Quaternion.identity;
            currentHealth = maxHealth;
            _isPlayerDetected = false;
            RefreshCurrentColor();

            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;
            if (_bodyHurtbox != null) _bodyHurtbox.IsVolumeActive = true;
            _nextAttackTime = Time.time + attackInterval;

            Debug.Log("<color=green>[Enemigo Cápsula] Reaparecido con salud completa (250 HP) flotando suavemente sobre el área.</color>");
        }
    }
}
