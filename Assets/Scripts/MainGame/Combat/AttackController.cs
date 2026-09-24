using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Core;
using ForjaGame.Resources;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona los ataques del jugador, registro de combos y detección de impacto físico.
    /// Opera de forma desacoplada de animaciones de cuerpo humanoide.
    /// </summary>
    public partial class AttackController : MonoBehaviour
    {
        [Header("Configuración de Combate")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private float attackCooldown = 0.8f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private GameStateController gameStateController;

        [Header("Configuración de Combo")]
        [SerializeField] private float comboTransitionTime = 0.52f;
        [SerializeField] private float attackDuration = 0.75f;
        [SerializeField] private float postComboCooldown = 0.35f;

        private Rigidbody _rb;
        private WeaponStats _equippedWeapon;
        private float _lastAttackTime = -1f, _currentAttackStartTime = -1f, _comboAvailableTime = -1f;
        private int _currentComboIndex = 0, _maxComboSteps = 0, _lastAttackInputFrame = -1;
        private bool _isAttacking = false, _isCarryingAttackStep = false, _hasBufferedNextStrike = false;
        private HitboxVisualizer _attackHitboxVisualizer;
        private float _hitBlackTime = -1f;

        public bool HasPickaxeEquipped { get; set; } = false;
        public float AttackCooldown => attackCooldown;
        public bool IsAttacking => _isAttacking;
        public bool IsCarryingAttackStep => _isCarryingAttackStep;
        public int CurrentComboIndex => _currentComboIndex;
        public int MaxComboSteps => _maxComboSteps;
        public bool HasBufferedNextStrike => _hasBufferedNextStrike;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (gameStateController == null) gameStateController = FindFirstObjectByType<GameStateController>();

            if (GetComponent<PlayerHealthController>() == null)
                gameObject.AddComponent<PlayerHealthController>();

            SetupAttackHitboxVisualizer();
        }

        private void SetupAttackHitboxVisualizer()
        {
            GameObject atkObj = new GameObject("PlayerAttackHitbox");
            atkObj.transform.SetParent(transform, false);
            _attackHitboxVisualizer = atkObj.AddComponent<HitboxVisualizer>();
            _attackHitboxVisualizer.SetShape(HitboxShape.Box);
            _attackHitboxVisualizer.IsVolumeActive = false;
        }

        private void Update()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (UnityEngine.EventSystems.EventSystem.current == null ||
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    HandleAttackInput();
                }
            }

            UpdateHitboxVisualizer();

            if (!_isAttacking) return;
            float elapsed = Time.time - _currentAttackStartTime;
            if (_isCarryingAttackStep && elapsed > 0.22f) _isCarryingAttackStep = false;

            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attackDuration));
            PlayerCombatIKDriver.Instance?.SetAttackState(true, _currentComboIndex, progress);

            // Transición de combo por click durante la ventana
            if (elapsed >= comboTransitionTime)
            {
                if (_hasBufferedNextStrike && _currentComboIndex + 1 < _maxComboSteps)
                {
                    _hasBufferedNextStrike = false;
                    ExecuteComboStrike(_currentComboIndex + 1);
                    return;
                }
            }

            // Fin del ataque al completar recuperación
            if (elapsed >= attackDuration)
            {
                EndAttack();
                return;
            }

            // Cancelación suave por movimiento si no hay golpe en cola
            if (!_hasBufferedNextStrike && elapsed >= attackDuration * 0.85f && HasMovementInput())
            {
                EndAttack();
            }
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.started && (UnityEngine.EventSystems.EventSystem.current == null ||
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                HandleAttackInput();
            }
        }

        private void HandleAttackInput()
        {
            if (Time.frameCount == _lastAttackInputFrame) return;
            _lastAttackInputFrame = Time.frameCount;
            Attack();
        }

        public void Attack()
        {
            if (UI.Core.UIPanelBase.IsAnyPanelOpen || Cursor.lockState != CursorLockMode.Locked) return;
            if (AnvilController.Instance != null && (AnvilController.Instance.IsForging || AnvilController.Instance.HasIngotOnAnvil || AnvilController.Instance.IsCarvingHandle)) return;
            if (gameStateController != null && !gameStateController.IsInState(GameState.Exploration)) return;

            var equipment = PlayerEquipmentController.Instance;
            if (equipment == null || !equipment.HasWeaponEquipped || _maxComboSteps <= 0) return;

            if (_isAttacking)
            {
                float elapsed = Time.time - _currentAttackStartTime;
                if (elapsed >= comboTransitionTime * 0.35f && _currentComboIndex + 1 < _maxComboSteps)
                {
                    _hasBufferedNextStrike = true;
                }
                return;
            }

            if (Time.time < _comboAvailableTime) return;

            _hasBufferedNextStrike = false;
            ExecuteComboStrike(0);
        }

        private void ExecuteComboStrike(int comboIndex)
        {
            _isAttacking = true;
            _currentComboIndex = comboIndex;
            _currentAttackStartTime = Time.time;
            _lastAttackTime = Time.time;

            // Reorientar hacia la dirección del input de movimiento del jugador si existe
            Vector3 attackDir = GetPlayerAttackInputDirection();
            if (attackDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(attackDir);
            }
            else
            {
                attackDir = transform.forward;
            }

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                // Mantener los pies firmes y anclados al suelo en la postura de combate (CombatIdle), sin patinar
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            }
            _isCarryingAttackStep = false;
            PlayerCombatIKDriver.Instance?.SetAttackState(true, comboIndex, 0f);

            if (WeaponComboAnimator.Instance != null)
            {
                WeaponComboAnimator.Instance.PlayStrike(comboIndex, attackDuration, PerformHitDetection);
            }
            else
            {
                PerformHitDetection();
            }
        }

        public void TriggerHitDetection() => PerformHitDetection();

        private void EndAttack()
        {
            _isAttacking = false;
            _isCarryingAttackStep = false;
            _hasBufferedNextStrike = false;
            _currentComboIndex = 0;
            _comboAvailableTime = Time.time + postComboCooldown;
            PlayerCombatIKDriver.Instance?.SetAttackState(false, 0, 0f);
            WeaponComboAnimator.Instance?.CancelOrReturnToGuard();
            if (_attackHitboxVisualizer != null)
            {
                _attackHitboxVisualizer.IsVolumeActive = false;
                _attackHitboxVisualizer.IsConnecting = false;
            }
        }
    }
}
