using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ForjaGame.Core;
using ForjaGame.Forge;

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
        public static bool IsGodModeActive { get; set; }

        [Header("Configuración de Salud")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth = 100;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Rigidbody _rb;
        private HitboxVisualizer _bodyHurtbox;
        private float _hurtboxBlackUntil = -1f;
        private float _lastDamageTime = -999f, _nextRegenTime = 0f, _rollInvulnerableUntil = -10f;
        private int _consecutiveBlockedHits = 0;
        private float _lastBlockHitTime = -99f;
        private bool _isDefeated = false;
        private PlayerMovement _playerMovement;
        private PlayableGraph _deathGraph;
        private static AnimationClip _cachedDeathClip;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0 && !_isDefeated;
        public bool IsDefeated => _isDefeated;
        public bool IsRollInvulnerable => Time.time < _rollInvulnerableUntil;
        public void GrantRollInvulnerability(float duration = 1.0f) => _rollInvulnerableUntil = Mathf.Max(_rollInvulnerableUntil, Time.time + Mathf.Max(0.1f, duration));

        private void OnEnable() { if (!AllPlayers.Contains(this)) AllPlayers.Add(this); }
        private void OnDisable() { AllPlayers.Remove(this); if (LocalInstance == this) LocalInstance = null; }
        private void OnDestroy() => StopDeathAnimation();

        private void Awake()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || netObj.IsOwner) LocalInstance = this;
            _fallbackInstance = this;
            _rb = GetComponent<Rigidbody>(); _playerMovement = GetComponent<PlayerMovement>();
            currentHealth = maxHealth; _spawnPosition = transform.position; _spawnRotation = transform.rotation;
            if (GetComponent<PlayerStaminaController>() == null) gameObject.AddComponent<PlayerStaminaController>();
            if (GetComponent<PlayerScreenEffectsHUD>() == null) gameObject.AddComponent<PlayerScreenEffectsHUD>();
            SetupBodyHurtbox();
        }

        private void Start() { _spawnPosition = transform.position; _spawnRotation = transform.rotation; }

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
            if (_bodyHurtbox != null && Time.time > _hurtboxBlackUntil)
                _bodyHurtbox.IsConnecting = false;

            // Regeneración pasiva suave (2 HP por segundo) tras 10 segundos sin recibir daño
            if (IsAlive && currentHealth < maxHealth && Time.time - _lastDamageTime >= 10.0f)
            {
                if (Time.time >= _nextRegenTime)
                {
                    _nextRegenTime = Time.time + 1.0f;
                    currentHealth = Mathf.Min(maxHealth, currentHealth + 2);
                }
            }
        }

        public void TakeDamage(int amount) => TakeDamageFrom(amount, null, false, false);

        public bool TakeDamageFrom(int amount, CaveCreatureBase attacker = null, bool isRanged = false, bool unparriable = false)
        {
            if (!IsAlive || _isDefeated || IsGodModeActive || GameStateController.State == GameState.Victory || GameLoopController.IsEndScreenOpen) return false;
            if (IsRollInvulnerable) { if (attacker is TrainingDummyController) ForjaGame.UI.ForgeTutorialDirector.Instance?.NotifyDummyRolled(); return false; }
            bool wasBlocked = false, wasGuardBroken = false;
            var atk = GetComponent<AttackController>();
            if (atk != null && atk.IsBlocking)
            {
                bool timedParry = atk.IsTimedParryWindow && !unparriable;
                var eq = GetComponent<PlayerEquipmentController>();
                WeaponStats curWep = eq != null ? eq.CurrentWeapon : null;
                GetComponentInChildren<DynamicEdgeAligner>()?.TriggerParryAnimation();
                atk.SpawnVisceralCombatImpact(transform.position + transform.forward * 0.75f + Vector3.up * 1.05f, true, timedParry);
                AlloyPassiveCombatResolver.OnDefenseTriggered(gameObject, curWep, attacker, timedParry);
                if (timedParry)
                {
                    _consecutiveBlockedHits = 0;
                    ForjaGame.Audio.ForgeAudioSystem.PlayChiselCut(transform.position);
                    if (!isRanged && attacker != null && attacker.IsAlive)
                    {
                        if (attacker is TitanBossController boss) boss.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(amount * 0.10f)));
                        else attacker.ApplyParryStun(1.05f, ignoreImmunity: true);
                    }
                    return true;
                }
                if (Time.time - _lastBlockHitTime > 3.6f) _consecutiveBlockedHits = 0;
                _lastBlockHitTime = Time.time;
                _consecutiveBlockedHits += (unparriable || amount >= 18) ? 2 : 1;

                var passiveKind = AlloyPassiveResolver.Resolve(curWep).Kind;
                int breakThreshold = passiveKind == AlloyPassiveKind.FerricBronze ? 4 : 2;
                float blockMult = curWep != null ? curWep.GetPassiveBlockDamageMultiplier() : 0.68f;
                bool guardBroken = !(attacker is TrainingDummyController) && _consecutiveBlockedHits >= breakThreshold;
                if (guardBroken)
                {
                    _consecutiveBlockedHits = 0;
                    atk.TriggerGuardBreak(1.15f);
                    blockMult = Mathf.Max(blockMult, 0.62f);
                    ForjaGame.Audio.ForgeAudioSystem.PlayChiselCut(transform.position);
                }
                if (_rb != null && !_rb.isKinematic && attacker != null)
                {
                    Vector3 push = transform.position - attacker.transform.position; push.y = 0f;
                    if (push.sqrMagnitude > 0.01f) _rb.AddForce(push.normalized * (guardBroken ? 5.6f : 3.8f), ForceMode.VelocityChange);
                }
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * blockMult));
                wasBlocked = !guardBroken; wasGuardBroken = guardBroken;
                ForjaGame.Audio.ForgeAudioSystem.PlayHitImpact(transform.position, true, guardBroken);
                if (attacker is TrainingDummyController) ForjaGame.UI.ForgeTutorialDirector.Instance?.NotifyDummyBlocked();
            }
            if (attacker is TrainingDummyController) { amount = Mathf.Min(amount, Mathf.Max(0, currentHealth - 25)); if (amount == 0) return false; }

            _lastDamageTime = Time.time; currentHealth = Mathf.Max(0, currentHealth - amount);
            ForjaGame.Audio.ForgeAudioSystem.PlayPlayerHurt(transform.position);
            GetComponentInChildren<DynamicEdgeAligner>()?.NotifyCombatAlert();
            if (_bodyHurtbox != null) { _bodyHurtbox.IsConnecting = true; _hurtboxBlackUntil = Time.time + 0.30f; }

            bool isLocal = (LocalInstance == this) || (GetComponent<Unity.Netcode.NetworkObject>()?.IsOwner ?? true);
            if (isLocal)
            {
                CombatDamageNumberHUD.SpawnPlayerBarDamage(amount, wasBlocked, wasGuardBroken);
                if (currentHealth > 0)
                {
                    float hpRatio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
                    ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzDanioSegunVida(hpRatio);
                    GetComponent<PlayerScreenEffectsHUD>()?.TriggerDamageHit(amount, maxHealth);
                    var cam = _playerMovement != null && _playerMovement.PlayerCamera != null ? _playerMovement.PlayerCamera.GetComponent<PlayerCameraFollow>() : PlayerCameraFollow.Instance;
                    cam?.TriggerImpulse(0.12f);
                }
            }
            if (currentHealth <= 0) TriggerPlayerDefeat();
            return false;
        }

        private void TriggerPlayerDefeat()
        {
            if (_isDefeated) return;
            _isDefeated = true;
            PlayerCameraFollow.Instance?.ResetShake();
            ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzMuerteRojoTotal();
            int pNum = (_playerMovement != null ? _playerMovement.PlayerIndex : 0) + 1;
            Debug.Log($"<color=red><b>[Jugador {pNum}] ¡HAS SIDO DERROTADO!</b></color>");

            if (_rb != null && !_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            _playerMovement?.SetMovementEnabled(false);
            PlayDeathAnimation();

            bool anyOtherAlive = false;
            for (int i = 0; i < AllPlayers.Count; i++)
            {
                var p = AllPlayers[i];
                if (p != null && p != this && p.gameObject.activeInHierarchy && p.IsAlive) { anyOtherAlive = true; break; }
            }
            if (!anyOtherAlive)
            {
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                FindFirstObjectByType<GameLoopController>(FindObjectsInactive.Include)?.TriggerDefeat();
            }
        }

        private void PlayDeathAnimation()
        {
            var atk = GetComponent<AttackController>(); atk?.CancelAttackForRoll(); atk?.CancelBlockForRoll();
            var eq = GetComponent<PlayerEquipmentController>();
            Animator anim = eq != null && eq.PaladinAnimator != null ? eq.PaladinAnimator : GetComponentInChildren<Animator>();
            if (eq?.EdgeAligner != null) eq.EdgeAligner.isRolling = true;
            if (anim == null) return;
            if (anim.layerCount > 1) anim.SetLayerWeight(1, 0f);
            if (anim.layerCount > 2) anim.SetLayerWeight(2, 0f);
            AnimationClip clip = GetDeathClip();
            if (clip == null) return;
            StopDeathAnimation();
            clip.wrapMode = WrapMode.ClampForever;
            _deathGraph = PlayableGraph.Create("PlayerDeath");
            var output = AnimationPlayableOutput.Create(_deathGraph, "Death", anim);
            var pl = AnimationClipPlayable.Create(_deathGraph, clip);
            pl.SetSpeed(0.85f); output.SetSourcePlayable(pl); _deathGraph.Play();
        }

        private void StopDeathAnimation() { if (_deathGraph.IsValid()) _deathGraph.Destroy(); }

        private static AnimationClip GetDeathClip()
        {
            if (_cachedDeathClip != null) return _cachedDeathClip;
            foreach (var res in new[] { "Animations/Combat/PlayerDeath", "Animations/Roll/UAL1_Standard" })
            {
                var all = UnityEngine.Resources.LoadAll<AnimationClip>(res);
                for (int i = 0; all != null && i < all.Length; i++)
                    if (all[i] != null && !all[i].name.StartsWith("__preview__") && all[i].name.Contains("Death")) { _cachedDeathClip = all[i]; return _cachedDeathClip; }
            }
            return null;
        }

        public void Heal(int amount) { if (IsAlive && amount > 0) currentHealth = Mathf.Min(maxHealth, currentHealth + amount); }

        public void RestoreHealthFull()
        {
            StopDeathAnimation();
            var eq = GetComponent<PlayerEquipmentController>();
            if (eq?.EdgeAligner != null) eq.EdgeAligner.isRolling = false;
            if (eq?.PaladinAnimator != null) eq.PaladinAnimator.CrossFadeInFixedTime("Locomotion", 0.1f, 0);
            currentHealth = maxHealth; _isDefeated = false; Time.timeScale = 1f;
            GetComponent<PlayerStaminaController>()?.RestoreFull();
            _playerMovement?.SetMovementEnabled(true);
            if (!ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen && !GameLoopController.IsEndScreenOpen)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
        public void RespawnPlayer()
        {
            transform.position = _spawnPosition; transform.rotation = _spawnRotation;
            if (_rb != null && !_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            RestoreHealthFull();
        }
    }
}
