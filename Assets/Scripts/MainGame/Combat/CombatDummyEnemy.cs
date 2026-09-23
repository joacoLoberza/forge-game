using System.Collections;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Enemigo de prueba tipo cápsula:
    /// - Punto específico de spawn en el mapa alejado del punto inicial (~25m), proyectando un área 2D en el piso.
    /// - Aparece flotando a corta distancia (+1.5m) sobre el suelo para posarse suavemente sin atravesar el piso.
    /// - Sistema de Detección / Aggro: Solo detecta al jugador y empieza a perseguirlo/atacarlo cuando entra en su radio circundante (8.5m).
    /// - Si el jugador sale del radio de alerta (12.5m) o muere, el enemigo regresa pacíficamente a su área de spawn.
    /// - Reaparece en su área 2D tras morir después de exactamente 30 segundos.
    /// - Cambia a color AZUL (telegraph) 0.5s antes de atacar para avisar al jugador.
    /// - Frecuencia de ataque: cada 2 segundos.
    /// - Hurtbox centrada anatómicamente en el modelo.
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
        [SerializeField] private Vector3 mapSpawnPoint = new Vector3(18.0f, 0.05f, 18.0f);
        [SerializeField] private float spawnZoneRadius = 2.2f;

        [Header("Área de Detección y Alerta (Aggro)")]
        [SerializeField] private float detectionRadius = 8.5f; // Radio para detectar y empezar a atacar
        [SerializeField] private float deaggroRadius = 12.5f;  // Distancia para desenganchar y volver a la base
        private bool _isPlayerDetected = false;

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
        public float DetectionRadius => detectionRadius;
        public bool IsPlayerDetected => _isPlayerDetected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureEnemyInScene()
        {
            if (FindFirstObjectByType<CombatDummyEnemy>() == null)
            {
                var player = FindFirstObjectByType<PlayerHealthController>();
                float floorY = player != null ? player.transform.position.y : 0.05f;
                // Ubicado en un punto fijo del mapa alejado del punto inicial (~25 metros del origen)
                Vector3 specificSpawn = new Vector3(18.0f, floorY, 18.0f);

                CreateDummyGameObject(specificSpawn);
            }
        }

        public static GameObject CreateDummyGameObject(Vector3 position)
        {
            GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObj.name = "DummyEnemy_Capsule";
            // Aparece flotando a corta distancia (+1.5m) sobre el suelo para posarse suavemente y no traspasarlo
            enemyObj.transform.position = position + Vector3.up * 1.5f;

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
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
        /// Genera el área 2D plana circular sobre el suelo en el punto de spawn definido:
        /// 1. Zona central de spawn (disco terracota).
        /// 2. Zona de detección / aggro circundante (anillo y disco translúcido dorado delimitador).
        /// </summary>
        private void Create2DFloorArea()
        {
            if (_floorZoneObj != null) return;

            _floorZoneObj = new GameObject("EnemySpawnFloorArea_2D");
            _floorZoneObj.transform.position = new Vector3(mapSpawnPoint.x, 0.03f, mapSpawnPoint.z);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");

            // 1. Disco de spawn central
            GameObject spawnPad = new GameObject("SpawnPad");
            spawnPad.transform.SetParent(_floorZoneObj.transform, false);
            var mfPad = spawnPad.AddComponent<MeshFilter>();
            var mrPad = spawnPad.AddComponent<MeshRenderer>();
            mrPad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrPad.receiveShadows = false;
            mfPad.sharedMesh = GenerateCircleMesh(spawnZoneRadius, 36);

            Material padMat = CreateTranslucentMaterial(shader, new Color(0.85f, 0.25f, 0.15f, 0.38f), "EnemySpawnPad_Mat");
            mrPad.sharedMaterial = padMat;

            // 2. Anillo delimitador del radio de detección (Aggro Zone Perimeter)
            GameObject detectRing = new GameObject("DetectionPerimeterRing");
            detectRing.transform.SetParent(_floorZoneObj.transform, false);
            var mfRing = detectRing.AddComponent<MeshFilter>();
            var mrRing = detectRing.AddComponent<MeshRenderer>();
            mrRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrRing.receiveShadows = false;
            mfRing.sharedMesh = GenerateRingMesh(detectionRadius - 0.18f, detectionRadius + 0.18f, 54);

            Material ringMat = CreateTranslucentMaterial(shader, new Color(0.95f, 0.68f, 0.10f, 0.45f), "EnemyDetectRing_Mat");
            mrRing.sharedMaterial = ringMat;

            // 3. Disco sutil del área de detección completa
            GameObject detectFill = new GameObject("DetectionAreaFill");
            detectFill.transform.SetParent(_floorZoneObj.transform, false);
            detectFill.transform.localPosition = new Vector3(0f, -0.005f, 0f);
            var mfFill = detectFill.AddComponent<MeshFilter>();
            var mrFill = detectFill.AddComponent<MeshRenderer>();
            mrFill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrFill.receiveShadows = false;
            mfFill.sharedMesh = GenerateCircleMesh(detectionRadius, 48);

            Material fillMat = CreateTranslucentMaterial(shader, new Color(0.95f, 0.68f, 0.10f, 0.07f), "EnemyDetectFill_Mat");
            mrFill.sharedMaterial = fillMat;
        }

        private static Material CreateTranslucentMaterial(Shader shader, Color col, string matName)
        {
            Material mat = new Material(shader) { name = matName };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 2999;
            return mat;
        }

        private static Mesh GenerateCircleMesh(float radius, int segments)
        {
            Mesh mesh = new Mesh { name = "CircleMesh" };
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

        private static Mesh GenerateRingMesh(float innerRadius, float outerRadius, int segments)
        {
            Mesh mesh = new Mesh { name = "RingMesh" };
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];

            float step = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * step);
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                int idxInner = i * 2;
                int idxOuter = i * 2 + 1;

                vertices[idxInner] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[idxOuter] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);

                int nextI = (i + 1) % segments;
                int nextInner = nextI * 2;
                int nextOuter = nextI * 2 + 1;

                int triBase = i * 6;
                triangles[triBase + 0] = idxInner;
                triangles[triBase + 1] = nextInner;
                triangles[triBase + 2] = idxOuter;

                triangles[triBase + 3] = idxOuter;
                triangles[triBase + 4] = nextInner;
                triangles[triBase + 5] = nextOuter;
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

            // Failsafe de seguridad física: si por algún motivo traspasa el piso, reposicionarlo flotando arriba
            if (transform.position.y < -5.0f)
            {
                transform.position = mapSpawnPoint + Vector3.up * 1.5f;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }

            if (_playerTransform == null) FindPlayer();
            if (_playerTransform == null) return;

            Vector3 toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float distToPlayer = toPlayer.magnitude;

            var pHealth = PlayerHealthController.Instance;
            bool isPlayerAlive = pHealth != null && pHealth.IsAlive && !pHealth.IsDefeated;

            // 1. Gestión de estado de detección (Aggro)
            if (!isPlayerAlive)
            {
                if (_isPlayerDetected)
                {
                    _isPlayerDetected = false;
                    _isTelegraphing = false;
                    _isAttacking = false;
                    SetBodyColor(_normalColor);
                }
            }
            else
            {
                if (!_isPlayerDetected)
                {
                    // Solo detecta al entrar dentro del área circundante
                    if (distToPlayer <= detectionRadius)
                    {
                        _isPlayerDetected = true;
                        _nextAttackTime = Time.time + attackInterval;
                        Debug.Log("<color=yellow>[Enemigo Cápsula] ¡Jugador detectado en el área circundante! Iniciando persecución.</color>");
                    }
                }
                else
                {
                    // Si el jugador se aleja más allá de la distancia de desenganche, pierde interés
                    if (distToPlayer > deaggroRadius)
                    {
                        _isPlayerDetected = false;
                        _isTelegraphing = false;
                        _isAttacking = false;
                        SetBodyColor(_normalColor);
                        Debug.Log("<color=grey>[Enemigo Cápsula] Jugador fuera de rango. Retornando a zona de spawn.</color>");
                    }
                }
            }

            // 2. Comportamiento en combate cuando el jugador está detectado
            if (_isPlayerDetected && isPlayerAlive)
            {
                // Orientación continua hacia el jugador
                if (distToPlayer > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
                }

                // Lógica de ataque cada 2 segundos con TELEGRAPH AZUL (0.5s previo)
                if (distToPlayer <= attackRange)
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
            }
            else
            {
                // Fuera de alerta: asegurarse de restaurar color normal
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
            if (!IsAlive || _rb == null) return;

            if (_isPlayerDetected && _playerTransform != null)
            {
                Vector3 toPlayer = _playerTransform.position - transform.position;
                toPlayer.y = 0f;
                float dist = toPlayer.magnitude;

                // Si está preparando el ataque (telegraph azul) o ejecutando ataque, frena los pies
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
            else
            {
                // Jugador no detectado o fuera de rango: Retorno tranquilo a su zona de spawn
                Vector3 toHome = mapSpawnPoint - transform.position;
                toHome.y = 0f;
                float distHome = toHome.magnitude;

                if (distHome > 0.8f)
                {
                    Vector3 returnDir = toHome.normalized;
                    Quaternion homeRot = Quaternion.LookRotation(returnDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, 4f * Time.fixedDeltaTime);
                    Vector3 vel = returnDir * (moveSpeed * 0.65f);
                    _rb.linearVelocity = new Vector3(vel.x, _rb.linearVelocity.y, vel.z);
                }
                else
                {
                    _rb.linearVelocity = new Vector3(_rb.linearVelocity.x * 0.2f, _rb.linearVelocity.y, _rb.linearVelocity.z * 0.2f);
                }
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

            // Recibir daño alerta inmediatamente al enemigo si aún no lo estaba
            _isPlayerDetected = true;

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

            _isPlayerDetected = false;
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

            // Reaparición flotando a corta distancia (+1.5m) sobre el área 2D en el suelo
            transform.position = mapSpawnPoint + Vector3.up * 1.5f;
            transform.rotation = Quaternion.identity;
            currentHealth = maxHealth;
            _isPlayerDetected = false;

            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;
            if (_bodyHurtbox != null) _bodyHurtbox.IsVolumeActive = true;
            _nextAttackTime = Time.time + attackInterval;

            Debug.Log("<color=green>[Enemigo Cápsula] Reaparecido con salud completa (500 HP) flotando suavemente sobre el área de spawn.</color>");
        }
    }
}
