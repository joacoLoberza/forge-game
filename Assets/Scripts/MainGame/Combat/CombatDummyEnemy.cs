using System.Collections;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Enemigo de prueba en forma de cápsula para testing de combate.
    /// - Persigue al jugador directamente en el plano terrestre.
    /// - Ejecuta un ataque frontal cada 1 segundo cuando está a distancia de combate (<= 2m).
    /// - Cada golpe inflige 15 puntos de daño al jugador.
    /// - Posee una hurtbox rígida de cuerpo y una hitbox de ataque frontal visibles con F3
    ///   (rojo transparente / negro al conectar con el jugador).
    /// - Vida resistente (500 HP) para soportar múltiples combos de prueba.
    /// </summary>
    public class CombatDummyEnemy : MonoBehaviour, IDamageable
    {
        public static CombatDummyEnemy Instance { get; private set; }

        [Header("Estadísticas del Enemigo")]
        [SerializeField] private int maxHealth = 500;
        [SerializeField] private int currentHealth = 500;
        [SerializeField] private int attackDamage = 15;
        [SerializeField] private float attackInterval = 1.0f;
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float moveSpeed = 2.2f;

        private Transform _playerTransform;
        private Rigidbody _rb;
        private HitboxVisualizer _bodyHurtbox;
        private HitboxVisualizer _attackHitbox;

        private float _nextAttackTime = 0f;
        private float _attackVisualEndTime = -1f;
        private float _bodyHurtBlackUntil = -1f;
        private bool _isAttacking = false;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureEnemyInScene()
        {
            if (FindFirstObjectByType<CombatDummyEnemy>() == null)
            {
                // Buscar al jugador en la escena para colocar el enemigo frente a él
                var player = FindFirstObjectByType<PlayerHealthController>();
                Vector3 spawnPos = Vector3.forward * 4.0f + Vector3.up * 0.1f;
                if (player != null)
                {
                    spawnPos = player.transform.position + player.transform.forward * 4.0f;
                    spawnPos.y = player.transform.position.y;
                }

                CreateDummyGameObject(spawnPos);
            }
        }

        public static GameObject CreateDummyGameObject(Vector3 position)
        {
            GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObj.name = "DummyEnemy_Capsule";
            enemyObj.transform.position = position;

            // Ajustar escala y visual
            enemyObj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            var renderer = enemyObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(unlit)
                {
                    color = new Color(0.65f, 0.22f, 0.18f, 1.0f) // Rojo terracota/óxido distintivo
                };
                renderer.sharedMaterial = mat;
            }

            enemyObj.AddComponent<CombatDummyEnemy>();
            return enemyObj;
        }

        private void Awake()
        {
            Instance = this;
            currentHealth = maxHealth;

            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            SetupHitboxes();
        }

        private void SetupHitboxes()
        {
            // 1. Hurtbox corporal (Cápsula rígida)
            GameObject bodyObj = new GameObject("EnemyBodyHurtbox");
            bodyObj.transform.SetParent(transform, false);
            _bodyHurtbox = bodyObj.AddComponent<HitboxVisualizer>();
            _bodyHurtbox.SetShape(HitboxShape.Capsule);
            _bodyHurtbox.UpdateCapsule(new Vector3(0f, 1.0f, 0f), 0.5f, 2.0f);

            // 2. Hitbox de ataque frontal (Caja rígida en frente)
            GameObject atkObj = new GameObject("EnemyAttackHitbox");
            atkObj.transform.SetParent(transform, false);
            _attackHitbox = atkObj.AddComponent<HitboxVisualizer>();
            _attackHitbox.SetShape(HitboxShape.Box);
            _attackHitbox.UpdateBox(new Vector3(0f, 1.0f, 1.0f), new Vector3(1.2f, 1.2f, 1.5f));
            _attackHitbox.IsVolumeActive = false; // Se activa al lanzar el golpe
        }

        private void Start()
        {
            FindPlayer();
            _nextAttackTime = Time.time + 1.0f;
        }

        private void FindPlayer()
        {
            var pHealth = PlayerHealthController.Instance;
            if (pHealth != null) _playerTransform = pHealth.transform;
            else
            {
                var pMove = FindFirstObjectByType<ForjaGame.Core.PlayerMovement>();
                if (pMove != null) _playerTransform = pMove.transform;
            }
        }

        private void Update()
        {
            if (!IsAlive) return;
            if (_playerTransform == null) FindPlayer();
            if (_playerTransform == null) return;

            Vector3 toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Orientarse hacia el jugador
            if (dist > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }

            // Temporizador de ataque cada 1 segundo al estar en rango
            if (dist <= attackRange)
            {
                if (Time.time >= _nextAttackTime)
                {
                    ExecuteEnemyAttack();
                }
            }

            // Control de visualización activa del ataque
            if (_attackHitbox != null)
            {
                if (_isAttacking && Time.time > _attackVisualEndTime)
                {
                    _isAttacking = false;
                    _attackHitbox.IsVolumeActive = false;
                    _attackHitbox.IsConnecting = false;
                }
            }

            // Restaurar color rojo de la hurtbox tras impacto
            if (_bodyHurtbox != null && Time.time > _bodyHurtBlackUntil)
            {
                _bodyHurtbox.IsConnecting = false;
            }
        }

        private void FixedUpdate()
        {
            if (!IsAlive || _playerTransform == null || _rb == null) return;

            Vector3 toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Persecución terrestre hasta alcanzar la distancia de ataque
            if (dist > attackRange * 0.90f)
            {
                Vector3 moveDir = toPlayer.normalized;
                Vector3 vel = moveDir * moveSpeed;
                _rb.linearVelocity = new Vector3(vel.x, _rb.linearVelocity.y, vel.z);
            }
            else
            {
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x * 0.2f, _rb.linearVelocity.y, _rb.linearVelocity.z * 0.2f);
            }
        }

        private void ExecuteEnemyAttack()
        {
            _nextAttackTime = Time.time + attackInterval;
            _isAttacking = true;
            _attackVisualEndTime = Time.time + 0.35f;

            if (_attackHitbox != null)
            {
                _attackHitbox.IsVolumeActive = true;
                _attackHitbox.IsConnecting = false;
            }

            // Detección de impacto frontal de la hitbox
            Vector3 center = transform.position + transform.forward * 1.0f + Vector3.up * 1.0f;
            Vector3 halfExtents = new Vector3(0.6f, 0.6f, 0.75f);
            Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore);

            bool connectedToPlayer = false;
            foreach (Collider hit in hits)
            {
                if (hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;

                var playerHealth = hit.GetComponentInParent<PlayerHealthController>() ?? hit.GetComponentInChildren<PlayerHealthController>();
                if (playerHealth != null && playerHealth.IsAlive)
                {
                    connectedToPlayer = true;
                    playerHealth.TakeDamage(attackDamage);
                    break;
                }
            }

            if (_attackHitbox != null)
            {
                _attackHitbox.IsConnecting = connectedToPlayer;
            }
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);
            Debug.Log($"<color=orange>[Enemigo Cápsula]</color> Dañado por {amount}. Vida restante: {currentHealth}/{maxHealth}");

            // Teñir hurtbox de negro al ser impactado por el jugador
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
            Debug.Log("<color=red>[Enemigo Cápsula] Derrotado! Reapareciendo en 3 segundos...</color>");
            if (_bodyHurtbox != null) _bodyHurtbox.IsVolumeActive = false;
            if (_attackHitbox != null) _attackHitbox.IsVolumeActive = false;
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            yield return new WaitForSeconds(3.0f);

            // Reaparición para seguir testeando
            if (PlayerHealthController.Instance != null)
            {
                transform.position = PlayerHealthController.Instance.transform.position + PlayerHealthController.Instance.transform.forward * 4.0f;
            }
            currentHealth = maxHealth;
            if (r != null) r.enabled = true;
            if (col != null) col.enabled = true;
            if (_bodyHurtbox != null) _bodyHurtbox.IsVolumeActive = true;
            Debug.Log("<color=green>[Enemigo Cápsula] Reaparecido con salud completa (500 HP).</color>");
        }
    }
}
