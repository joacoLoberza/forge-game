using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona la salud del jugador (100 HP), impacto de daño (15 por golpe enemigo),
    /// visualización de hurtbox corporal y pantalla de derrota con botón interactivo de Reiniciar.
    /// No reaparece hasta que el usuario pulse [REINICIAR].
    /// </summary>
    public class PlayerHealthController : MonoBehaviour, IDamageable
    {
        public static PlayerHealthController Instance { get; private set; }

        [Header("Configuración de Salud")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth = 100;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Rigidbody _rb;
        private HitboxVisualizer _bodyHurtbox;
        private float _hurtboxBlackUntil = -1f;
        private bool _isDefeated = false;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0 && !_isDefeated;
        public bool IsDefeated => _isDefeated;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _rb = GetComponent<Rigidbody>();
            currentHealth = maxHealth;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            SetupBodyHurtbox();
        }

        private void Start()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }

        private void SetupBodyHurtbox()
        {
            GameObject hurtboxObj = new GameObject("PlayerBodyHurtbox");
            hurtboxObj.transform.SetParent(transform, false);
            _bodyHurtbox = hurtboxObj.AddComponent<HitboxVisualizer>();
            _bodyHurtbox.SetShape(HitboxShape.Capsule);
            _bodyHurtbox.UpdateCapsule(new Vector3(0f, 1.0f, 0f), 0.45f, 2.0f);
        }

        private void Update()
        {
            if (_bodyHurtbox != null)
            {
                if (Time.time > _hurtboxBlackUntil)
                {
                    _bodyHurtbox.IsConnecting = false;
                }
            }
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive || _isDefeated) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);
            Debug.Log($"[Jugador] Recibió {amount} de daño. Salud restante: {currentHealth}/{maxHealth}");

            // Teñir hurtbox de negro al recibir daño (contacto)
            if (_bodyHurtbox != null)
            {
                _bodyHurtbox.IsConnecting = true;
                _hurtboxBlackUntil = Time.time + 0.30f;
            }

            // Impulso de cámara por impacto
            PlayerCameraFollow.Instance?.TriggerImpulse(0.12f);

            if (currentHealth <= 0)
            {
                TriggerPlayerDefeat();
            }
        }

        private void TriggerPlayerDefeat()
        {
            if (_isDefeated) return;
            _isDefeated = true;
            Debug.Log("<color=red><b>[Jugador] ¡HAS SIDO DERROTADO! Esperando botón de reinicio...</b></color>");

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            // Notificar a GameLoopController si existe en la escena
            GameLoopController gameLoop = FindFirstObjectByType<GameLoopController>();
            if (gameLoop != null)
            {
                gameLoop.TriggerDefeat();
            }
        }

        public void RespawnPlayer()
        {
            GameLoopController gameLoop = FindFirstObjectByType<GameLoopController>();
            if (gameLoop != null)
            {
                gameLoop.AcceptDefeat();
            }
            else
            {
                transform.position = _spawnPosition;
                transform.rotation = _spawnRotation;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }

            currentHealth = maxHealth;
            _isDefeated = false;
            Time.timeScale = 1f;

            Debug.Log("<color=green>[Jugador] Reaparecido con salud completa (100 HP) tras pulsar Reiniciar.</color>");
        }

        private void OnGUI()
        {
            GUI.skin.box.fontSize = 13;
            GUI.skin.label.fontSize = 13;

            // 1. PANTALLA MODAL DE DERROTA (Solo si el jugador ha muerto)
            if (_isDefeated)
            {
                // Fondo oscurecido completo
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.78f), 0f, 0f);

                // Cuadro central
                float dialogWidth = 420f;
                float dialogHeight = 220f;
                float dialogX = (Screen.width - dialogWidth) * 0.5f;
                float dialogY = (Screen.height - dialogHeight) * 0.5f;

                GUI.Box(new Rect(dialogX, dialogY, dialogWidth, dialogHeight), "");

                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);
                GUI.Label(new Rect(dialogX, dialogY + 25f, dialogWidth, 35f), "¡HAS SIDO DERROTADO!", titleStyle);

                var subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                subtitleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(dialogX, dialogY + 70f, dialogWidth, 25f), "Tu salud ha llegado a 0 puntos.", subtitleStyle);

                // Botón interactivo de Reiniciar
                var btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                float btnWidth = 200f;
                float btnHeight = 48f;
                float btnX = dialogX + (dialogWidth - btnWidth) * 0.5f;
                float btnY = dialogY + 130f;

                if (GUI.Button(new Rect(btnX, btnY, btnWidth, btnHeight), "↻ REINICIAR", btnStyle))
                {
                    RespawnPlayer();
                }

                return;
            }

            // 2. HUD SUPERIOR IZQUIERDO (Durante el juego normal)
            float padX = 15f;
            float topY = 15f;
            float boxWidth = 260f;

            // Panel de Salud del Jugador
            GUI.Box(new Rect(padX, topY, boxWidth, 75f), "");
            GUI.Label(new Rect(padX + 10f, topY + 6f, boxWidth - 20f, 22f), $"<b>JUGADOR HP:</b> {currentHealth} / {maxHealth}");

            // Barra de vida
            float barWidth = boxWidth - 20f;
            float healthRatio = Mathf.Clamp01((float)currentHealth / maxHealth);
            GUI.DrawTexture(new Rect(padX + 10f, topY + 30f, barWidth, 14f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.2f, 0.2f, 0.2f, 0.9f), 0f, 0f);
            Color barColor = healthRatio > 0.5f ? Color.Lerp(Color.yellow, Color.green, (healthRatio - 0.5f) * 2f) : Color.Lerp(Color.red, Color.yellow, healthRatio * 2f);
            GUI.DrawTexture(new Rect(padX + 10f, topY + 30f, barWidth * healthRatio, 14f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, barColor, 0f, 0f);

            // Recordatorio F3
            string f3Text = HitboxDebugManager.ShowHitboxes ? "<color=#00FF66><b>[F3] HITBOXES: VISIBLES</b></color>" : "<color=#AAAAAA>[F3] Hitboxes: Ocultas</color>";
            GUI.Label(new Rect(padX + 10f, topY + 48f, boxWidth - 20f, 22f), f3Text);

            // Panel de Enemigo si está activo en combate
            var enemy = CombatDummyEnemy.Instance;
            if (enemy != null)
            {
                float enemyY = topY + 85f;
                GUI.Box(new Rect(padX, enemyY, boxWidth, 55f), "");

                if (enemy.IsAlive)
                {
                    GUI.Label(new Rect(padX + 10f, enemyY + 6f, boxWidth - 20f, 22f), $"<b>ENEMIGO HP:</b> {enemy.CurrentHealth} / {enemy.MaxHealth}");
                    float enemyRatio = Mathf.Clamp01((float)enemy.CurrentHealth / enemy.MaxHealth);
                    GUI.DrawTexture(new Rect(padX + 10f, enemyY + 30f, barWidth, 12f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.2f, 0.2f, 0.2f, 0.9f), 0f, 0f);
                    GUI.DrawTexture(new Rect(padX + 10f, enemyY + 30f, barWidth * enemyRatio, 12f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.9f, 0.2f, 0.2f, 0.95f), 0f, 0f);
                }
                else
                {
                    float remaining = Mathf.Max(0f, enemy.RespawnTimeRemaining);
                    GUI.Label(new Rect(padX + 10f, enemyY + 16f, boxWidth - 20f, 24f), $"<color=orange><b>ENEMIGO DERROTADO</b></color>\nReaparición en: {Mathf.CeilToInt(remaining)}s");
                }
            }
        }
    }
}
