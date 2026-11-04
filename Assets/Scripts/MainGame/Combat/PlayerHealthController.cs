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
        public static PlayerHealthController LocalInstance { get; set; }
        private static PlayerHealthController _fallbackInstance;
        public static PlayerHealthController Instance
        {
            get => LocalInstance != null ? LocalInstance : (_fallbackInstance != null ? _fallbackInstance : (AllPlayers.Count > 0 ? AllPlayers[0] : null));
            set => _fallbackInstance = value;
        }
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
            if (LocalInstance == this) LocalInstance = null;
        }

        private void Awake()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || netObj.IsOwner) LocalInstance = this;
            _fallbackInstance = this;

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

        private GUIStyle _hudPlaqueStyle, _hudTitleStyle, _hudSubStyle;
        private static Texture2D _hpFillCrimsonTex, _hpFillGoldTex;

        public void TakeDamage(int amount)
        {
            if (!IsAlive || _isDefeated) return;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            ForjaGame.Audio.ForgeAudioSystem.PlayPlayerHurt(transform.position);
            GetComponentInChildren<DynamicEdgeAligner>()?.NotifyCombatAlert();

            if (_bodyHurtbox != null) { _bodyHurtbox.IsConnecting = true; _hurtboxBlackUntil = Time.time + 0.30f; }

            bool isLocal = (LocalInstance == this) || (GetComponent<Unity.Netcode.NetworkObject>()?.IsOwner ?? true);
            if (isLocal)
            {
                if (_playerMovement != null && _playerMovement.PlayerCamera != null)
                    _playerMovement.PlayerCamera.GetComponent<PlayerCameraFollow>()?.TriggerImpulse(0.12f);
                else PlayerCameraFollow.Instance?.TriggerImpulse(0.12f);
            }
            if (currentHealth <= 0) TriggerPlayerDefeat();
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
            _playerMovement?.SetMovementEnabled(false);

            // En cooperativo local, comprobar si todos los jugadores fueron derrotados
            bool anyOtherAlive = false;
            for (int i = 0; i < AllPlayers.Count; i++)
            {
                var p = AllPlayers[i];
                if (p != null && p != this && p.gameObject.activeInHierarchy && p.IsAlive)
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

                GameLoopController gameLoop = FindFirstObjectByType<GameLoopController>(FindObjectsInactive.Include);
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
            _playerMovement?.SetMovementEnabled(true);

            if (!ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen && !GameLoopController.IsEndScreenOpen)
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

        private void EnsureHudStyles()
        {
            if (_hudPlaqueStyle != null) return;
            _hudPlaqueStyle = new GUIStyle
            {
                normal = { background = ForjaGame.UI.BookTextureBuilder.GetParchmentSprite().texture },
                border = new RectOffset(16, 16, 16, 16), padding = new RectOffset(12, 12, 8, 8)
            };
            _hudTitleStyle = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = new Color(0.17f, 0.11f, 0.06f) } };
            _hudSubStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Italic, richText = true, normal = { textColor = new Color(0.32f, 0.20f, 0.10f) } };
            ForjaGame.UI.MedievalTypographyProvider.ApplyToGUIStyle(_hudTitleStyle);
            ForjaGame.UI.MedievalTypographyProvider.ApplyToGUIStyle(_hudSubStyle);

            _hpFillCrimsonTex = new Texture2D(1, 8, TextureFormat.RGBA32, false);
            _hpFillGoldTex = new Texture2D(1, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
            {
                float hl = y >= 5 ? 1.22f : (y <= 1 ? 0.72f : 1f);
                _hpFillCrimsonTex.SetPixel(0, y, new Color(Mathf.Clamp01(0.76f * hl), Mathf.Clamp01(0.16f * hl), Mathf.Clamp01(0.14f * hl), 1f));
                _hpFillGoldTex.SetPixel(0, y, new Color(Mathf.Clamp01(0.86f * hl), Mathf.Clamp01(0.58f * hl), Mathf.Clamp01(0.16f * hl), 1f));
            }
            _hpFillCrimsonTex.Apply(); _hpFillGoldTex.Apply();
        }

        private void OnGUI()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && !netObj.IsOwner) return;
            if (ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen || GameLoopController.IsEndScreenOpen) return;

            EnsureHudStyles();
            float w = 236f, h = 42f, x = 18f, y = 18f;

            GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), ForjaGame.UI.BookTextureBuilder.GetLeatherCoverSprite().texture);
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _hudPlaqueStyle);

            var heartStyle = new GUIStyle(_hudTitleStyle) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
            heartStyle.normal.textColor = currentHealth < 30 ? new Color(0.85f, 0.2f, 0.1f) : new Color(0.65f, 0.12f, 0.10f);
            GUI.Label(new Rect(x + 8f, y + 8f, 24f, 26f), "♥", heartStyle);

            float barX = x + 36f, barY = y + 12f, barW = 130f, barH = 18f;
            float ratio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), ForjaGame.UI.BookTextureBuilder.GetSliderTrackSprite().texture);
            if (ratio > 0.005f)
                GUI.DrawTexture(new Rect(barX + 2f, barY + 3f, (barW - 4f) * ratio, 12f), ratio < 0.32f ? _hpFillGoldTex : _hpFillCrimsonTex);

            var numStyle = new GUIStyle(_hudTitleStyle) { fontSize = 13, alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(barX + barW + 6f, y + 9f, 48f, 24f), $"<b>{currentHealth}</b>", numStyle);
        }
    }
}
