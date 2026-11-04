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
        private bool _attackInputRequested;
        public bool IsAttacking => _isAttacking;
        public int CurrentComboIndex => _currentComboIndex;
        public int MaxComboSteps => _maxComboSteps;
        public AttackMove ActiveMove => _activeMove;
        public bool IsHitStopped => Time.time < _hitStopUntil;
        public bool HasPickaxeEquipped => _equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.Pickaxe;
        public event System.Action<int> OnAttackTriggered;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (gameStateController == null) gameStateController = FindFirstObjectByType<GameStateController>();
            if (GetComponent<PlayerHealthController>() == null) gameObject.AddComponent<PlayerHealthController>();

            _playerInput = GetComponent<PlayerInput>();
            _equipmentCtrl = GetComponent<PlayerEquipmentController>();
            _movementCtrl = GetComponent<PlayerMovement>();

            SetupAttackHitboxVisualizer();
        }

        private void Update()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            bool isLocal = netObj == null || netObj.IsOwner;
            bool mouseTriggered = isLocal && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
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

            float baseDur = _activeMove != null ? _activeMove.baseDuration : (_equippedWeapon != null ? _equippedWeapon.GetBaseAttackDuration() : 1.05f);
            float speedMult = _equippedWeapon != null ? Mathf.Max(0.5f, _equippedWeapon.attackSpeed) : 1.0f;
            attackDuration = baseDur / speedMult;
            float linkRatio = _activeMove != null ? Mathf.Clamp(_activeMove.hitWindowEnd + 0.20f, 0.82f, 0.88f) : (_equippedWeapon != null ? _equippedWeapon.GetComboLinkRatio() : 0.85f);
            comboTransitionTime = attackDuration * linkRatio;

            // Reorientar al iniciar el golpe hacia la dirección de ataque/cámara
            Vector3 attackDir = GetPlayerAttackInputDirection();
            if (attackDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(attackDir);
            }

            // Zancada física sutil y controlada (paso marcial plantado, sin impulsos exagerados)
            float lungeBase = _activeMove != null ? _activeMove.lungeDistance * 1.0f : 0.35f;
            if (comboIndex == _maxComboSteps - 1) lungeBase *= 1.25f;
            Vector3 forwardLunge = transform.forward * lungeBase;
            if (_rb != null)
            {
                _rb.linearVelocity = new Vector3(forwardLunge.x, Mathf.Min(0f, _rb.linearVelocity.y), forwardLunge.z);
            }

            // Disparar animación correspondiente del combo en Mecanim
            var equip = GetEquipment();
            var paladinAnim = equip?.PaladinAnimator;
            if (paladinAnim != null)
            {
                paladinAnim.SetBool("MirrorAttack", false);
                paladinAnim.SetFloat("AttackSpeed", speedMult);
                paladinAnim.SetInteger("ComboIndex", comboIndex);
                paladinAnim.SetBool("IsAttacking", true);
                if (_equippedWeapon != null && _equippedWeapon.weaponType == WeaponType.Pickaxe)
                {
                    paladinAnim.CrossFadeInFixedTime("Attack1", 0.10f, 0);
                }
                else
                {
                    int combatLayer = (paladinAnim.layerCount > 1) ? 1 : 0;
                    if (combatLayer == 1) paladinAnim.SetLayerWeight(1, 1f);
                    paladinAnim.CrossFadeInFixedTime($"Attack{comboIndex + 1}", 0.12f, combatLayer);
                }
            }
            if (equip?.EdgeAligner != null) { equip.EdgeAligner.isAttacking = true; equip.EdgeAligner.NotifyCombatAlert(5.0f); }
            bool isHeavy2H = _equippedWeapon != null && _equippedWeapon.GetCombatStance() != WeaponCombatStance.OneHanded;
            ForjaGame.Audio.ForgeAudioSystem.PlayWeaponSwing(transform.position, isHeavy2H);
            OnAttackTriggered?.Invoke(comboIndex);
        }
    }
}
