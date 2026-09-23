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
    public class AttackController : MonoBehaviour
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

        private void UpdateHitboxVisualizer()
        {
            if (_attackHitboxVisualizer == null) return;

            _attackHitboxVisualizer.IsVolumeActive = _isAttacking;
            if (_isAttacking)
            {
                GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize);
                _attackHitboxVisualizer.UpdateBox(localCenter, boxSize);
                if (Time.time > _hitBlackTime)
                {
                    _attackHitboxVisualizer.IsConnecting = false;
                }
            }
        }

        public void GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize)
        {
            var style = _equippedWeapon != null ? _equippedWeapon.GetAnimationStyle() : WeaponAnimationStyle.Sword1H;
            AttackMotionType motion = WeaponComboAnimator.GetMotionType(style, _currentComboIndex);
            float range = Mathf.Max(1.6f, attackRange);

            switch (motion)
            {
                case AttackMotionType.Thrust:
                    // Estocada: Forma rectangular alargada desde el centro del personaje hasta el alcance máximo
                    boxSize = new Vector3(0.75f, 0.80f, range);
                    localCenter = new Vector3(0f, 0.90f, range * 0.5f);
                    break;

                case AttackMotionType.OverheadSlam:
                    // Aplastamiento vertical: Volumen vertical enfocado al frente
                    boxSize = new Vector3(0.95f, 1.50f, range * 0.85f);
                    localCenter = new Vector3(0f, 0.75f, (range * 0.85f) * 0.5f);
                    break;

                default: // AttackMotionType.HorizontalSlash
                    // Tajo horizontal: Mayor área lateral pero menor alcance frontal que la estocada
                    float lateralWidth = range * 1.40f;
                    float forwardDepth = range * 0.65f;
                    boxSize = new Vector3(lateralWidth, 0.90f, forwardDepth);
                    localCenter = new Vector3(0f, 0.95f, forwardDepth * 0.5f);
                    break;
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
        }

        public void ResetToDefaultStats()
        {
            _equippedWeapon = null;
            damage = 0;
            attackRange = 1.0f;
            attackCooldown = 0.5f;
            attackDuration = 0.75f;
            comboTransitionTime = 0.52f;
            _maxComboSteps = 0;
            _hasBufferedNextStrike = false;
            _isAttacking = false;
            _isCarryingAttackStep = false;
            _comboAvailableTime = -1f;
        }

        private void PerformHitDetection()
        {
            GetAttackHitboxParams(out Vector3 localCenter, out Vector3 boxSize);
            Vector3 worldCenter = transform.TransformPoint(localCenter);
            Vector3 halfExtents = boxSize * 0.5f;
            Quaternion orientation = transform.rotation;

            Collider[] targets = Physics.OverlapBox(worldCenter, halfExtents, orientation, targetLayers, QueryTriggerInteraction.Ignore);
            IDamageable closestTarget = null;
            float closestDist = float.PositiveInfinity;
            bool hitConnected = false;

            foreach (Collider target in targets)
            {
                if (target.gameObject == gameObject || target.transform.IsChildOf(transform)) continue;
                IDamageable d = target.GetComponentInParent<IDamageable>() ?? target.GetComponentInChildren<IDamageable>();
                if (d == null || !d.IsAlive) continue;

                hitConnected = true;
                Vector3 toTarget = target.bounds.center - worldCenter;
                float dist = toTarget.magnitude;
                if (dist < closestDist) { closestTarget = d; closestDist = dist; }
            }

            if (_attackHitboxVisualizer != null)
            {
                _attackHitboxVisualizer.IsConnecting = hitConnected;
                if (hitConnected)
                {
                    _hitBlackTime = Time.time + 0.35f;
                }
            }

            if (closestTarget != null)
            {
                bool isFinisher = _currentComboIndex >= Mathf.Max(1, _maxComboSteps - 1);
                float comboMult = isFinisher ? 1.65f : (1.0f + _currentComboIndex * 0.12f);
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * comboMult));
                if (closestTarget is ResourceNodeController node) node.TakeDamageFrom(finalDamage, gameObject);
                else closestTarget.TakeDamage(finalDamage);

                if (isFinisher) PlayerCameraFollow.Instance?.TriggerImpulse(0.08f);

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

        public Vector3 GetPlayerAttackInputDirection()
        {
            var kb = Keyboard.current;
            if (kb == null) return transform.forward;

            float x = 0f, y = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;

            Vector2 input = new Vector2(x, y);
            if (input.sqrMagnitude < 0.01f) return transform.forward;

            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 camFwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 camRgt = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;

            Vector3 dir = (camFwd * input.y + camRgt * input.x).normalized;
            return dir.sqrMagnitude > 0.01f ? dir : transform.forward;
        }
    }
}
