using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Core;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    [RequireComponent(typeof(Rigidbody))]
    public partial class AttackController : MonoBehaviour
    {
        [Header("Configuración de Ataque")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float attackRange = 1.4f;
        [SerializeField] private float attackCooldown = 0.5f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private GameStateController gameStateController;

        [Header("Sistema de Combo Táctil Fluido")]
        [SerializeField] private float comboTransitionTime = 0.18f;
        [SerializeField] private float attackDuration = 0.36f;
        [SerializeField] private float postComboCooldown = 0.22f;
        [SerializeField] private bool allowFriendlyFire = false;

        private Rigidbody _rb;
        private HitboxVisualizer _attackHitboxVisualizer;
        private float _hitBlackTime = -1f;

        private bool _isAttacking = false;
        private int _currentComboIndex = 0;
        private int _maxComboSteps = 5;
        private float _currentAttackStartTime = 0f;
        private float _comboAvailableTime = 0f;
        private bool _hasBufferedNextStrike = false;

        private WeaponStats _equippedWeapon;
        private WeaponAnalysis _currentAnalysis;
        private AttackMove[] _currentComboMoves;
        private AttackMove _activeMove;

        private float _hitStopUntil = 0f;
        private readonly HashSet<IDamageable> _hitTargetsThisSwing = new();
        private int _lastAttackInputFrame = -1;

        private PlayerInput _playerInput;
        private PlayerEquipmentController _equipmentCtrl;
        private PlayerMovement _movementCtrl;
        private PlayerStaminaController _staminaCtrl;
        private bool _attackInputRequested;
        public bool IsAttacking => _isAttacking;
        public int CurrentComboIndex => _currentComboIndex;
        public int MaxComboSteps => _maxComboSteps;
        public AttackMove ActiveMove => _activeMove;
        public bool IsHitStopped => Time.time < _hitStopUntil;
        public float AttackProgress => _isAttacking && attackDuration > 0.01f ? Mathf.Clamp01((Time.time - _currentAttackStartTime) / attackDuration) : 0f;
        public bool HasPickaxeEquipped => _equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.Pickaxe;
        public event System.Action<int, int, float> OnAttackTriggered;
        public event System.Action OnAttackEnded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (gameStateController == null) gameStateController = FindFirstObjectByType<GameStateController>();
            if (GetComponent<PlayerHealthController>() == null) gameObject.AddComponent<PlayerHealthController>();

            _playerInput = GetComponent<PlayerInput>();
            _equipmentCtrl = GetComponent<PlayerEquipmentController>();
            _movementCtrl = GetComponent<PlayerMovement>();
            _staminaCtrl = GetComponent<PlayerStaminaController>() ?? gameObject.AddComponent<PlayerStaminaController>();

            SetupAttackHitboxVisualizer();
        }

        private void Update()
        {
            if (ForjaGame.UI.PauseMenuPanel.IsGamePaused) return;
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            bool isLocal = netObj == null || netObj.IsOwner;
            UpdateBlockingAndBash(isLocal);
            bool mouseTriggered = isLocal && !_isBlocking && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (mouseTriggered || _attackInputRequested)
            {
                _attackInputRequested = false;
                if (UnityEngine.EventSystems.EventSystem.current == null ||
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    HandleAttackInput();
                }
            }

            UpdateHitboxVisualizer();

            if (!_isAttacking || _activeMove == null) return;

            // Pausa por Hit-Stop en impacto
            if (Time.time < _hitStopUntil) return;

            float elapsed = Time.time - _currentAttackStartTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attackDuration));

            // Barrido de hitbox durante la ventana activa de contacto
            if (progress >= _activeMove.hitWindowStart && progress <= _activeMove.hitWindowEnd)
            {
                PerformSweepHitDetection();
            }

            // Ventana de buffer para encadenar fluidamente el siguiente golpe del combo
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

        private PlayerEquipmentController GetEquipment()
        {
            if (_equipmentCtrl == null) _equipmentCtrl = GetComponent<PlayerEquipmentController>();
            return _equipmentCtrl ?? PlayerEquipmentController.Instance;
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.started) _attackInputRequested = true;
        }

        public void OnAttack(InputValue value)
        {
            if (value.isPressed) _attackInputRequested = true;
        }

        private void HandleAttackInput()
        {
            if (Time.frameCount == _lastAttackInputFrame) return;
            _lastAttackInputFrame = Time.frameCount;
            Attack();
        }

        public void Attack()
        {
            if (UI.Core.UIPanelBase.IsAnyPanelOpen) return;
            var roll = GetComponent<PlayerRollController>();
            if (roll != null && roll.IsRolling) return;
            if (AnvilController.Instance != null && (AnvilController.Instance.IsForging || AnvilController.Instance.HasIngotOnAnvil || AnvilController.Instance.IsCarvingHandle)) return;
            if (gameStateController != null && !gameStateController.IsInState(GameState.Exploration)) return;

            var equipment = GetEquipment();
            if (equipment == null || !equipment.HasWeaponEquipped) return;

            if (_isAttacking)
            {
                float elapsed = Time.time - _currentAttackStartTime;
                if (elapsed >= attackDuration * 0.15f && _currentComboIndex + 1 < _maxComboSteps)
                {
                    _hasBufferedNextStrike = true;
                }
                return;
            }

            if (Time.time < _comboAvailableTime) return;
            EnsureComboMoves();

            _hasBufferedNextStrike = false;
            ExecuteComboStrike(0);
        }

        private void ExecuteComboStrike(int comboIndex)
        {
            bool isPick = _equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.Pickaxe;
            if (isPick) comboIndex = 0;

            _isAttacking = true;
            _currentComboIndex = comboIndex;
            _currentAttackStartTime = Time.time;
            _hitTargetsThisSwing.Clear();

            EnsureComboMoves();

            AttackMove desired = _currentComboMoves != null && comboIndex < _currentComboMoves.Length
                ? _currentComboMoves[comboIndex]
                : null;

            _activeMove = WeaponAnalyzer.ResolveMove(desired, _currentAnalysis);

            float jitter = isPick ? UnityEngine.Random.Range(0.95f, 1.06f) : 1.0f;
            float baseDur = _activeMove != null ? _activeMove.baseDuration : (_equippedWeapon != null ? _equippedWeapon.GetBaseAttackDuration() : 1.05f);
            float speedMult = (_equippedWeapon != null ? Mathf.Max(0.5f, _equippedWeapon.attackSpeed) : 1.0f) * jitter;
            attackDuration = baseDur / speedMult;
            float linkRatio = _activeMove != null ? Mathf.Clamp(_activeMove.hitWindowEnd + 0.14f, 0.72f, 0.84f) : (_equippedWeapon != null ? _equippedWeapon.GetComboLinkRatio() : 0.78f);
            comboTransitionTime = attackDuration * linkRatio;

            Vector3 attackDir = GetPlayerAttackInputDirection();
            if (attackDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(attackDir);

            var equip = GetEquipment();
            var paladinAnim = equip?.PaladinAnimator;
            bool isMoving = (_movementCtrl != null && _movementCtrl.MoveInput.sqrMagnitude > 0.04f)
                || (paladinAnim != null && paladinAnim.GetFloat("Speed") > 0.25f);

            if (!isMoving)
            {
                float lungeBase = _activeMove != null ? _activeMove.lungeDistance * 0.65f : 0.26f;
                if (isPick) lungeBase = 0.14f;
                else if (comboIndex == _maxComboSteps - 1) lungeBase *= 1.18f;
                Vector3 forwardLunge = transform.forward * lungeBase;
                if (_rb != null && !_rb.isKinematic)
                    _rb.linearVelocity = new Vector3(forwardLunge.x, Mathf.Min(0f, _rb.linearVelocity.y), forwardLunge.z);
            }

            if (paladinAnim != null)
            {
                float clipLen = DynamicEdgeAligner.ResolveAttackClipLength(paladinAnim, comboIndex, 1.15f);
                float clipPlaySpeed = Mathf.Clamp(clipLen / Mathf.Max(0.28f, attackDuration * 0.96f), 0.48f, 2.35f);
                bool useUpperBody = isMoving && paladinAnim.layerCount > 1;
                if (equip?.EdgeAligner != null) equip.EdgeAligner.isUpperBodyAttack = useUpperBody;
                if (paladinAnim.layerCount > 1) paladinAnim.SetLayerWeight(1, useUpperBody ? 1f : 0f);
                if (paladinAnim.layerCount > 2) paladinAnim.SetLayerWeight(2, useUpperBody ? 0.45f : 0f);
                paladinAnim.SetBool("MirrorAttack", false);
                paladinAnim.SetFloat("AttackSpeed", clipPlaySpeed);
                paladinAnim.SetInteger("ComboIndex", comboIndex);
                paladinAnim.SetBool("IsAttacking", true);
                paladinAnim.CrossFadeInFixedTime($"Attack{comboIndex + 1}", 0.08f, useUpperBody ? 1 : 0, 0f);
            }
            if (equip?.EdgeAligner != null) { equip.EdgeAligner.isAttacking = true; equip.EdgeAligner.NotifyCombatAlert(5.0f); }
            bool isHeavy2H = _equippedWeapon != null && _equippedWeapon.GetCombatStance() != WeaponCombatStance.OneHanded;
            ForjaGame.Audio.ForgeAudioSystem.PlayWeaponSwing(transform.position, isHeavy2H, isPick);
            int wType = _equippedWeapon != null ? (int)_equippedWeapon.weaponType : (int)WeaponType.Sword;
            OnAttackTriggered?.Invoke(comboIndex, wType, speedMult);
        }
    }
}
