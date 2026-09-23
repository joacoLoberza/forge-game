using System.Collections;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Enemigo de prueba tipo cápsula:
    /// - Punto específico de spawn en el mapa con un área 2D circular proyectada sobre el piso.
    /// - Reaparece en su área 2D tras morir después de exactamente 30 segundos.
    /// - Cambia a color AZUL (telegraph) 0.5s antes de atacar para avisar al jugador.
    /// - Frecuencia de ataque: cada 2 segundos.
    /// - Hitboxes y hurtboxes alineadas al centro real del modelo (altura anatómica corregida).
    /// - Vida resistente (500 HP) con 15 de daño al jugador.
    /// </summary>
    public class CombatDummyEnemy : MonoBehaviour, IDamageable
    {
        public static CombatDummyEnemy Instance { get; private set; }

        [Header("Estadísticas del Enemigo")]
        [SerializeField] private int maxHealth = 500;
        [SerializeField] private int currentHealth = 500;
        [SerializeField] private int attackDamage = 15;
        [SerializeField] private float attackInterval = 2.0f; // Cada 2 segundos
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float moveSpeed = 2.2f;

        [Header("Área de Spawn en el Mapa")]
        [SerializeField] private Vector3 mapSpawnPoint = new Vector3(6.5f, 0.03f, 8.0f);
        [SerializeField] private float spawnZoneRadius = 2.2f;

        private Transform _playerTransform;
        private Rigidbody _rb;
        private Renderer _renderer;
        private Collider _collider;
        private HitboxVisualizer _bodyHurtbox;
        private HitboxVisualizer _attackHitbox;

        private Material _bodyMaterial;
        private readonly Color _normalColor = new Color(0.65f, 0.22f, 0.18f, 1.0f); // Terracota óxido
        private readonly Color _telegraphBlueColor = new Color(0.15f, 0.48f, 0.95f, 1.0f); // AZUL previo al ataque

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureEnemyInScene()
        {
            if (FindFirstObjectByType<CombatDummyEnemy>() == null)
            {
                var player = FindFirstObjectByType<PlayerHealthController>();
                Vector3 specificSpawn = new Vector3(6.5f, 0.05f, 8.0f);
                if (player != null)
                {
                    // Ubicado en un punto fijo a 7 metros del jugador en el mapa
                    specificSpawn = player.transform.position + new Vector3(4.5f, 0f, 6.0f);
                    specificSpawn.y = player.transform.position.y;
                }

                CreateDummyGameObject(specificSpawn);
            }
        }

        public static GameObject CreateDummyGameObject(Vector3 position)
        {
            GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObj.name = "DummyEnemy_Capsule";
            enemyObj.transform.position = position + Vector3.up * 1.0f; // Para que la base descanse sobre el piso

            var dummy = enemyObj.AddComponent<CombatDummyEnemy>();
            dummy.mapSpawnPoint = position;
            return enemyObj;
        }

        private void Awake()
        {
            Instance = this;
            currentHealth = maxHealth;

            _collider = GetComponent<Collider>();
            _renderer = GetComponent<Renderer>();
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            SetupMaterial();
            SetupHitboxes();
            Create2DFloorArea();
        }

        private void SetupMaterial()
        {
            if (_renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _bodyMaterial = new Material(shader)
                {
                    name = "EnemyBody_Mat",
                    color = _normalColor
                };
                if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", _normalColor);
                _renderer.sharedMaterial = _bodyMaterial;
            }
        }

        private void SetupHitboxes()
        {
            // 1. Hurtbox corporal (Cápsula centrada exactamente en el modelo)
            // PrimitiveType.Capsule tiene su origen local en el centro (0, 0, 0)
            GameObject bodyObj = new GameObject("EnemyBodyHurtbox");
            bodyObj.transform.SetParent(transform, false);
            _bodyHurtbox = bodyObj.AddComponent<HitboxVisualizer>();
            _bodyHurtbox.SetShape(HitboxShape.Capsule);
            _bodyHurtbox.UpdateCapsule(Vector3.zero, 0.5f, 2.0f);

            // 2. Hitbox de ataque frontal (Alineada al torso frontal del enemigo)
            GameObject atkObj = new GameObject("EnemyAttackHitbox");
            atkObj.transform.SetParent(transform, false);
            _attackHitbox = atkObj.AddComponent<HitboxVisualizer>();
            _attackHitbox.SetShape(HitboxShape.Box);
            _attackHitbox.UpdateBox(new Vector3(0f, 0f, 1.0f), new Vector3(1.2f, 1.2f, 1.5f));
            _attackHitbox.IsVolumeActive = false;
        }

        /// <summary>
        /// Genera un área 2D plana circular sobre el suelo en el punto de spawn definido.
        /// </summary>
        private void Create2DFloorArea()
        {
            if (_floorZoneObj != null) return;

            _floorZoneObj = new GameObject("EnemySpawnFloorArea_2D");
            _floorZoneObj.transform.position = new Vector3(mapSpawnPoint.x, 0.03f, mapSpawnPoint.z);

            var mf = _floorZoneObj.AddComponent<MeshFilter>();
            var mr = _floorZoneObj.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Generar malla plana circular 2D (XZ)
            mf.sharedMesh = GenerateCircleMesh(spawnZoneRadius, 36);

            // Material translúcido para el área de spawn del piso
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            Material zoneMat = new Material(shader)
            {
                name = "EnemySpawnFloor_Mat"
            };
            Color zoneColor = new Color(0.85f, 0.25f, 0.15f, 0.38f);
            if (zoneMat.HasProperty("_BaseColor")) zoneMat.SetColor("_BaseColor", zoneColor);
            if (zoneMat.HasProperty("_Color")) zoneMat.SetColor("_Color", zoneColor);
            zoneMat.SetFloat("_Surface", 1f);
            zoneMat.SetFloat("_Blend", 0f);
            zoneMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            zoneMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            zoneMat.SetInt("_ZWrite", 0);
            zoneMat.renderQueue = 2999;
            mr.sharedMaterial = zoneMat;
        }

        private static Mesh GenerateCircleMesh(float radius, int segments)
        {
            Mesh mesh = new Mesh { name = "SpawnCircleMesh" };
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            float step = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * step);
                vertices[i + 1] = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

                int next = (i + 1) % segments;
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Start()
        {
            FindPlayer();
            _nextAttackTime = Time.time + attackInterval;
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

            // Orientación continua hacia el jugador
            if (dist > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }

            // Lógica de ataque cada 2 segundos con TELEGRAPH AZUL (0.5s previo)
            if (dist <= attackRange)
            {
                float timeToAttack = _nextAttackTime - Time.time;

                // 0.5s antes del ataque: Entrar en modo aviso y cambiar color a AZUL
                if (timeToAttack <= 0.5f && !_isTelegraphing && !_isAttacking)
                {
                    _isTelegraphing = true;
                    SetBodyColor(_telegraphBlueColor);
                }

                // Disparo del ataque al cumplirse los 2 segundos
                if (Time.time >= _nextAttackTime)
                {
                    _isTelegraphing = false;
                    SetBodyColor(_normalColor);
                    ExecuteEnemyAttack();
                }
            }
            else
            {
                // Si el jugador se aleja del rango durante el telegraph, cancela el aviso azul
                if (_isTelegraphing)
                {
                    _isTelegraphing = false;
                    SetBodyColor(_normalColor);
                }
            }

            // Ocultamiento de la hitbox activa tras el golpe
            if (_attackHitbox != null && _isAttacking && Time.time > _attackVisualEndTime)
            {
                _isAttacking = false;
                _attackHitbox.IsVolumeActive = false;
                _attackHitbox.IsConnecting = false;
            }

            // Restauración del color de la hurtbox tras recibir daño del jugador
            if (_bodyHurtbox != null && Time.time > _bodyHurtBlackUntil)
            {
                _bodyHurtbox.IsConnecting = false;
            }
        }

        private void SetBodyColor(Color color)
        {
            if (_bodyMaterial != null)
            {
                _bodyMaterial.color = color;
                if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", color);
            }
        }

        private void FixedUpdate()
        {
            if (!IsAlive || _playerTransform == null || _rb == null) return;

            Vector3 toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            // Si está preparando el ataque (telegraph azul), frena los pies para mayor precisión
            if (_isTelegraphing || _isAttacking)
            {
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x * 0.2f, _rb.linearVelocity.y, _rb.linearVelocity.z * 0.2f);
                return;
            }

            // Persecución terrestre hasta el alcance de ataque
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

            // Detección frontal con altura centrada exactamente en el torso
            Vector3 center = transform.position + transform.forward * 1.0f;
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
            Debug.Log("<color=red>[Enemigo Cápsula] Derrotado! Reapareciendo en su área 2D en 30 segundos...</color>");

            _isTelegraphing = false;
            _isAttacking = false;
            SetBodyColor(_normalColor);

            if (_bodyHurtbox != null) _bodyHurtbox.IsVolumeActive = false;
            if (_attackHitbox != null) _attackHitbox.IsVolumeActive = false;
            if (_renderer != null) _renderer.enabled = false;
            if (_collider != null) _collider.enabled = false;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            // Esperar exactamente 30 segundos solicitados por el usuario
            yield return new WaitForSeconds(30.0f);

            // Reaparición en el punto específico del área 2D sobre el piso
            transform.position = mapSpawnPoint + Vector3.up * 1.0f;
            transform.rotation = Quaternion.identity;
            currentHealth = maxHealth;

            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;
            if (_bodyHurtbox != null) _bodyHurtbox.IsVolumeActive = true;
            _nextAttackTime = Time.time + attackInterval;

            Debug.Log("<color=green>[Enemigo Cápsula] Reaparecido con salud completa (500 HP) en el área de spawn.</color>");
        }
    }
}
