using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Core;
using ForjaGame.Resources;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public class AttackController : MonoBehaviour
    {
        [Header("Combat Settings")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float attackRange = 2.2f;
        [SerializeField] private float attackCooldown = 0.8f;
        [SerializeField] private float attackAngleCos = 0.35f;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField] private GameStateController gameStateController;
        [SerializeField] private Animator characterAnimator;

        [Header("Procedural Combat Settings")]
        [SerializeField] private bool useProceduralCombat = true;
        [SerializeField] private ProceduralCombatController proceduralCombatController;

        [Header("Combo Configuration")]
        [SerializeField] private float comboTransitionTime = 0.55f;
        [SerializeField] private float attackDuration = 0.85f;
        [SerializeField] private float postComboCooldown = 0.40f;

        private Rigidbody _rb;
        private WeaponStats _equippedWeapon;
        private float _lastAttackTime = -1f, _currentAttackStartTime = -1f, _comboAvailableTime = -1f;
        private int _currentComboIndex = 0, _maxComboSteps = 0, _lastAttackInputFrame = -1;
        private bool _isAttacking = false, _isCarryingAttackStep = false, _hasBufferedNextStrike = false;
        public bool HasPickaxeEquipped { get; set; } = false;
        public bool UseProceduralCombat
        {
            get => useProceduralCombat;
            set => useProceduralCombat = value;
        }
        public ProceduralCombatController ProceduralCombat => proceduralCombatController;
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
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (characterAnimator != null) characterAnimator.SetFloat("AttackSpeed", 1.0f);
            if (proceduralCombatController == null)
                proceduralCombatController = GetComponent<ProceduralCombatController>() ?? GetComponentInChildren<ProceduralCombatController>();
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

            if (!_isAttacking) return;
            float elapsed = Time.time - _currentAttackStartTime;
            if (_isCarryingAttackStep && elapsed > 0.22f) _isCarryingAttackStep = false;

            // Transición estricta: sólo avanza si el jugador hizo click durante la ventana del combo
            if (elapsed >= comboTransitionTime)
            {
                if (_hasBufferedNextStrike && _currentComboIndex + 1 < _maxComboSteps)
                {
                    _hasBufferedNextStrike = false;
                    ExecuteComboStrike(_currentComboIndex + 1);
                    return;
                }
            }

            // Fin del ataque al completar la recuperación total del golpe
            if (elapsed >= attackDuration) { EndAttack(); return; }

            // Cancelación suave por movimiento (WASD) en la fase final de recuperación si no hay golpe en cola
            if (!_hasBufferedNextStrike && elapsed >= attackDuration * 0.85f && HasMovementInput())
            {
                EndAttack();
            }
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.started && (UnityEngine.EventSystems.EventSystem.current == null ||
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) HandleAttackInput();
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
                // Buffer del siguiente golpe: sólo dentro de la ventana de enlace y si no excede el combo
                if (elapsed >= comboTransitionTime * 0.35f && _currentComboIndex + 1 < _maxComboSteps)
                {
                    _hasBufferedNextStrike = true;
                }
                return;
            }

            // Retardo / delay antes de permitir iniciar una nueva secuencia
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

            if (useProceduralCombat && proceduralCombatController != null && proceduralCombatController.IsEquipped)
            {
                proceduralCombatController.ExecuteAttack(comboIndex);
                var timings = proceduralCombatController.GetCurrentAttackTimings();
                attackDuration = timings.totalDuration;
                comboTransitionTime = timings.windupDuration + (timings.strikeDuration * 0.85f);
                attackCooldown = attackDuration;

                if (characterAnimator != null)
                {
                    characterAnimator.SetInteger("ComboIndex", comboIndex);
                    characterAnimator.SetBool("IsAttacking", true);
                }
                return;
            }

            // Fallback a clips fijos de legado
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                float impulse = 2.0f;
                if (_equippedWeapon != null)
                {
                    var stance = _equippedWeapon.GetCombatStance();
                    impulse = stance == WeaponCombatStance.TwoHanded ? 2.8f : (stance == WeaponCombatStance.Polearm ? 3.0f : 2.0f);
                }
                float stepMult = comboIndex == 0 ? 1.0f : (comboIndex == 1 ? 1.25f : 1.65f);
                Vector3 stepVec = transform.forward * (impulse * stepMult);
                _rb.linearVelocity = new Vector3(stepVec.x, _rb.linearVelocity.y, stepVec.z);
            }
            _isCarryingAttackStep = true;

            if (characterAnimator != null)
            {
                float speedRatio = _equippedWeapon != null ? Mathf.Clamp(_equippedWeapon.GetSpeedMultiplier(), 0.70f, 1.30f) : 1.0f;
                characterAnimator.SetFloat("AttackSpeed", speedRatio);
                characterAnimator.SetInteger("ComboIndex", comboIndex);
                characterAnimator.ResetTrigger("Attack");
                characterAnimator.SetTrigger("Attack");
                characterAnimator.SetBool("IsAttacking", true);
            }

            PerformHitDetection();
        }

        /// <summary>
        /// Invocado por ProceduralCombatController en el momento en que se inicia el barrido de impacto (Strike).
        /// </summary>
        public void TriggerHitDetection()
        {
            PerformHitDetection();
        }

        private void EndAttack()
        {
            _isAttacking = false;
            _isCarryingAttackStep = false;
            _hasBufferedNextStrike = false;
            _currentComboIndex = 0;
            _comboAvailableTime = Time.time + postComboCooldown;

            if (useProceduralCombat && proceduralCombatController != null)
            {
                proceduralCombatController.ResetToGuard();
            }

            if (characterAnimator != null)
            {
                characterAnimator.SetBool("IsAttacking", false);
                characterAnimator.ResetTrigger("Attack");
                characterAnimator.CrossFadeInFixedTime("Locomotion", 0.12f);
            }
        }

        public void ApplyWeaponStats(WeaponStats weapon)
        {
            if (weapon == null) { ResetToDefaultStats(); return; }
            _equippedWeapon = weapon;
            damage = Mathf.Max(1, Mathf.RoundToInt(weapon.EffectiveDamage));
            attackRange = Mathf.Clamp(weapon.attackRange, 1.6f, 4.5f);
            float speedRatio = Mathf.Clamp(weapon.GetSpeedMultiplier(), 0.70f, 1.30f);
            weapon.attackSpeed = speedRatio;
            _maxComboSteps = Mathf.Clamp(weapon.MaxComboCount, 1, 5);
            float baseDuration = weapon.GetBaseAttackDuration();
            attackDuration = baseDuration / speedRatio;
            comboTransitionTime = (baseDuration * weapon.GetComboLinkRatio()) / speedRatio;
            attackCooldown = attackDuration;

            if (characterAnimator != null) characterAnimator.SetFloat("AttackSpeed", speedRatio);
        }

        public void ResetToDefaultStats()
        {
            _equippedWeapon = null; damage = 0; attackRange = 1.0f; attackCooldown = 0.5f;
            attackDuration = 0.80f; comboTransitionTime = 0.55f; _maxComboSteps = 0;
            _hasBufferedNextStrike = false; _isAttacking = false; _isCarryingAttackStep = false; _comboAvailableTime = -1f;
            if (characterAnimator != null) { characterAnimator.SetFloat("AttackSpeed", 1.0f); characterAnimator.SetBool("IsAttacking", false); }
        }

        private void PerformHitDetection()
        {
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            Vector3 forward = transform.forward;
            Collider[] targets = Physics.OverlapSphere(origin, attackRange, targetLayers, QueryTriggerInteraction.Ignore);
            IDamageable closestTarget = null;
            float closestDist = float.PositiveInfinity;

            foreach (Collider target in targets)
            {
                if (target.gameObject == gameObject || target.transform.IsChildOf(transform)) continue;
                IDamageable d = target.GetComponentInParent<IDamageable>() ?? target.GetComponentInChildren<IDamageable>();
                if (d == null || !d.IsAlive) continue;

                Vector3 toTarget = target.bounds.center - origin;
                float dist = toTarget.magnitude;
                if (dist > attackRange || Vector3.Dot(forward, toTarget / Mathf.Max(0.001f, dist)) < attackAngleCos) continue;

                if (dist < closestDist) { closestTarget = d; closestDist = dist; }
            }

            if (closestTarget != null)
            {
                float comboMult = _currentComboIndex == 2 ? 1.55f : (_currentComboIndex == 1 ? 1.15f : 1.0f);
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * comboMult));
                if (closestTarget is ResourceNodeController node) node.TakeDamageFrom(finalDamage, gameObject);
                else closestTarget.TakeDamage(finalDamage);

                if (_currentComboIndex == 2) PlayerCameraFollow.Instance?.TriggerImpulse(0.24f);

                if (_equippedWeapon != null)
                {
                    _equippedWeapon.ConsumeSharpness(1.0f);
                    damage = Mathf.Max(1, Mathf.RoundToInt(_equippedWeapon.EffectiveDamage));
                }
            }
        }

        private bool HasMovementInput()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed);
        }
    }
}
