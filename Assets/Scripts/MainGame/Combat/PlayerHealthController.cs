using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona la salud del jugador (100 HP), impacto de daño, visualización de hurtbox corporal
    /// y pantalla de derrota. Soporta registro multi-jugador para cooperativo local.
    /// </summary>
    public class PlayerHealthController : MonoBehaviour, IDamageable
    {
        public static PlayerHealthController Instance { get; private set; }
        public static readonly List<PlayerHealthController> AllPlayers = new();

        [Header("Configuración de Salud")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth = 100;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Rigidbody _rb;
        private HitboxVisualizer _bodyHurtbox;
        private float _hurtboxBlackUntil = -1f;
        private bool _isDefeated = false;
        private PlayerMovement _playerMovement;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0 && !_isDefeated;
        public bool IsDefeated => _isDefeated;

        private void OnEnable()
        {
            if (!AllPlayers.Contains(this))
                AllPlayers.Add(this);
        }

        private void OnDisable()
        {
            AllPlayers.Remove(this);
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _rb = GetComponent<Rigidbody>();
            _playerMovement = GetComponent<PlayerMovement>();
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
            _bodyHurtbox.UpdateCapsule(Vector3.zero, 0.45f, 2.0f);
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
            int pNum = (_playerMovement != null ? _playerMovement.PlayerIndex : 0) + 1;
            Debug.Log($"[Jugador {pNum}] Recibió {amount} de daño. Salud restante: {currentHealth}/{maxHealth}");

            // Teñir hurtbox de negro al recibir daño (contacto)
            if (_bodyHurtbox != null)
            {
                _bodyHurtbox.IsConnecting = true;
                _hurtboxBlackUntil = Time.time + 0.30f;
            }

            // Impulso en la cámara propia del jugador
            if (_playerMovement != null && _playerMovement.PlayerCamera != null)
            {
                _playerMovement.PlayerCamera.GetComponent<PlayerCameraFollow>()?.TriggerImpulse(0.12f);
            }
            else
            {
                PlayerCameraFollow.Instance?.TriggerImpulse(0.12f);
            }

            if (currentHealth <= 0)
            {
                TriggerPlayerDefeat();
            }
        }

        private void TriggerPlayerDefeat()
        {
            if (_isDefeated) return;
            _isDefeated = true;
            int pNum = (_playerMovement != null ? _playerMovement.PlayerIndex : 0) + 1;
            Debug.Log($"<color=red><b>[Jugador {pNum}] ¡HAS SIDO DERROTADO!</b></color>");

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            // En cooperativo local, comprobar si todos los jugadores fueron derrotados
            bool anyOtherAlive = false;
            for (int i = 0; i < AllPlayers.Count; i++)
            {
                var p = AllPlayers[i];
                if (p != null && p != this && p.IsAlive)
                {
                    anyOtherAlive = true;
                    break;
                }
            }

            // Si ningún jugador queda vivo, disparar fin de partida global
            if (!anyOtherAlive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                GameLoopController gameLoop = FindFirstObjectByType<GameLoopController>();
                if (gameLoop != null)
                {
                    gameLoop.TriggerDefeat();
                }
            }
        }

        public void RestoreHealthFull()
        {
            currentHealth = maxHealth;
            _isDefeated = false;
            Time.timeScale = 1f;

            if (!ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            int pNum = (_playerMovement != null ? _playerMovement.PlayerIndex : 0) + 1;
            Debug.Log($"<color=green>[Jugador {pNum}] Salud restaurada por completo ({maxHealth} HP).</color>");
        }

        public void RespawnPlayer()
        {
            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            RestoreHealthFull();
        }

        private void OnGUI()
        {
            GUI.skin.box.fontSize = 13;
            GUI.skin.label.fontSize = 13;

            int pIdx = _playerMovement != null ? _playerMovement.PlayerIndex : 0;
            float boxWidth = 260f;
            float padX = (pIdx == 0) ? 15f : (Screen.width - boxWidth - 15f);
            float topY = 15f;

            string playerColorTag = (pIdx == 0) ? "#55AAFF" : "#FFAA33";
            string playerName = $"<color={playerColorTag}><b>JUGADOR {pIdx + 1} HP:</b></color> {currentHealth} / {maxHealth}";

            GUI.Box(new Rect(padX, topY, boxWidth, 75f), "");
            GUI.Label(new Rect(padX + 10f, topY + 6f, boxWidth - 20f, 22f), playerName);

            // Barra de vida del jugador
            float barWidth = boxWidth - 20f;
            float healthRatio = Mathf.Clamp01((float)currentHealth / maxHealth);
            GUI.DrawTexture(new Rect(padX + 10f, topY + 30f, barWidth, 14f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.2f, 0.2f, 0.2f, 0.9f), 0f, 0f);
            Color barColor = (pIdx == 0)
                ? (healthRatio > 0.5f ? Color.Lerp(Color.yellow, new Color(0.2f, 0.7f, 1f), (healthRatio - 0.5f) * 2f) : Color.Lerp(Color.red, Color.yellow, healthRatio * 2f))
                : (healthRatio > 0.5f ? Color.Lerp(Color.yellow, new Color(1.0f, 0.6f, 0.1f), (healthRatio - 0.5f) * 2f) : Color.Lerp(Color.red, Color.yellow, healthRatio * 2f));
            GUI.DrawTexture(new Rect(padX + 10f, topY + 30f, barWidth * healthRatio, 14f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, barColor, 0f, 0f);

            // Recordatorio de debug o estado
            string subText = _isDefeated ? "<color=red><b>CAÍDO EN COMBATE</b></color>" : (HitboxDebugManager.ShowHitboxes ? "<color=#00FF66>[F3] Hitboxes</color>" : "<color=#888888>En exploración</color>");
            GUI.Label(new Rect(padX + 10f, topY + 48f, boxWidth - 20f, 22f), subText);
        }
    }
}
