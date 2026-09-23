using System.Collections;
using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona la salud del jugador (100 HP), impacto de daño (15 por golpe enemigo),
    /// visualización de hurtbox corporal y reinicio por derrota al morir.
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
        public bool IsAlive => currentHealth > 0;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _rb = GetComponent<Rigidbody>();
            currentHealth = maxHealth;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            // Configurar hurtbox corporal para F3
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
            Debug.Log("<color=red><b>[Jugador] ¡HAS SIDO DERROTADO! Reiniciando partida...</b></color>");

            // Notificar a GameLoopController si existe en la escena
            GameLoopController gameLoop = FindFirstObjectByType<GameLoopController>();
            if (gameLoop != null)
            {
                gameLoop.TriggerDefeat();
            }

            StartCoroutine(RespawnRoutine(gameLoop));
        }

        private IEnumerator RespawnRoutine(GameLoopController gameLoop)
        {
            yield return new WaitForSecondsRealtime(1.2f);

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
            Debug.Log("<color=green>[Jugador] Reaparecido con salud completa (100 HP).</color>");
        }

        private void OnGUI()
        {
            // HUD Superior Izquierdo: Salud del Jugador y Estado de Hitboxes
            GUI.skin.box.fontSize = 13;
            GUI.skin.label.fontSize = 13;

            float padX = 15f;
            float topY = 15f;
            float boxWidth = 260f;

            // 1. Panel de Salud del Jugador
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

            // 2. Panel de Enemigo más cercano si está presente
            var enemy = CombatDummyEnemy.Instance;
            if (enemy != null && enemy.IsAlive)
            {
                float enemyY = topY + 85f;
                GUI.Box(new Rect(padX, enemyY, boxWidth, 55f), "");
                GUI.Label(new Rect(padX + 10f, enemyY + 6f, boxWidth - 20f, 22f), $"<b>ENEMIGO HP:</b> {enemy.CurrentHealth} / {enemy.MaxHealth}");

                float enemyRatio = Mathf.Clamp01((float)enemy.CurrentHealth / enemy.MaxHealth);
                GUI.DrawTexture(new Rect(padX + 10f, enemyY + 30f, barWidth, 12f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.2f, 0.2f, 0.2f, 0.9f), 0f, 0f);
                GUI.DrawTexture(new Rect(padX + 10f, enemyY + 30f, barWidth * enemyRatio, 12f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.9f, 0.2f, 0.2f, 0.95f), 0f, 0f);
            }
        }
    }
}
