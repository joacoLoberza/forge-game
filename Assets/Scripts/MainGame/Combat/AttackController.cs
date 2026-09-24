using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Core;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona los ataques del jugador basados en datos de movimiento (AttackMove) y combos de 5 golpes.
    /// Fuente de verdad unificada en espacio del pecho gobernando el IK y las fases marciales.
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
        private WeaponAnalysis _currentAnalysis;
        private AttackMove[] _currentComboMoves;
        private AttackMove _activeMove;

        private float _currentAttackStartTime = -1f;
        private float _comboAvailableTime = -1f;
        private float _hitStopUntil = -1f;
        private int _currentComboIndex = 0;
        private int _maxComboSteps = 5;
        private int _lastAttackInputFrame = -1;
        private bool _isAttacking = false;
        private bool _hasBufferedNextStrike = false;
        private readonly HashSet<IDamageable> _hitTargetsThisSwing = new();

        private HitboxVisualizer _attackHitboxVisualizer;
        private float _hitBlackTime = -1f;

        public bool HasPickaxeEquipped { get; set; } = false;
        public float AttackCooldown => attackCooldown;
        public bool IsAttacking => _isAttacking;
        public int CurrentComboIndex => _currentComboIndex;
        public int MaxComboSteps => _maxComboSteps;
        public AttackMove ActiveMove => _activeMove;
        public bool IsHitStopped => Time.time < _hitStopUntil;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (gameStateController == null) gameStateController = FindFirstObjectByType<GameStateController>();
            if (GetComponent<PlayerHealthController>() == null) gameObject.AddComponent<PlayerHealthController>();
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

            if (!_isAttacking || _activeMove == null) return;

            // Pausa por Hit-Stop
            if (Time.time < _hitStopUntil) return;

            float elapsed = Time.time - _currentAttackStartTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attackDuration));

            PlayerCombatIKDriver.Instance?.UpdateAttackProgress(progress);

            // Barrido de hitbox durante la ventana activa
            if (progress >= _activeMove.hitWindowStart && progress <= _activeMove.hitWindowEnd)
            {
                PerformSweepHitDetection();
            }

            // Buffer de combo para transicionar al siguiente golpe
            if (elapsed >= comboTransitionTime)
            {
                if (_hasBufferedNextStrike && _currentComboIndex + 1 < _maxComboSteps)
                {
                    _hasBufferedNextStrike = false;
                    ExecuteComboStrike(_currentComboIndex + 1);
                    return;
                }
            }

            if (elapsed >= attackDuration)
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
            if (equipment == null || !equipment.HasWeaponEquipped) return;

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
            _hitTargetsThisSwing.Clear();

            EnsureComboMoves();

            AttackMove desired = _currentComboMoves != null && comboIndex < _currentComboMoves.Length
                ? _currentComboMoves[comboIndex]
                : null;

            _activeMove = WeaponAnalyzer.ResolveMove(desired, _currentAnalysis);

            float speedMult = _equippedWeapon != null ? Mathf.Max(0.5f, _equippedWeapon.attackSpeed) : 1.0f;
            attackDuration = (_activeMove != null ? _activeMove.baseDuration : 0.65f) / speedMult;
            comboTransitionTime = attackDuration * 0.70f;

            // Reorientar al iniciar el golpe
            Vector3 attackDir = GetPlayerAttackInputDirection();
            if (attackDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(attackDir);
            }

            PlayerCombatIKDriver.Instance?.StartAttackMove(_activeMove, attackDuration);
        }

        private void EnsureComboMoves()
        {
            if (_currentComboMoves == null || _currentComboMoves.Length != 5)
            {
                _currentAnalysis = WeaponAnalyzer.Analyze(_equippedWeapon);
                _currentComboMoves = ComboCatalog.GetCombo(_currentAnalysis.archetype);
                _maxComboSteps = 5;
            }
        }

        public void TriggerHitStop(float duration)
        {
            _hitStopUntil = Time.time + Mathf.Clamp(duration, 0.035f, 0.075f);
        }

        private void EndAttack()
        {
            _isAttacking = false;
            _hasBufferedNextStrike = false;
            _currentComboIndex = 0;
            _activeMove = null;
            _hitTargetsThisSwing.Clear();
            _comboAvailableTime = Time.time + postComboCooldown;

            PlayerCombatIKDriver.Instance?.EndAttack();

            if (_attackHitboxVisualizer != null)
            {
                _attackHitboxVisualizer.IsVolumeActive = false;
                _attackHitboxVisualizer.IsConnecting = false;
            }
        }
    }
}
