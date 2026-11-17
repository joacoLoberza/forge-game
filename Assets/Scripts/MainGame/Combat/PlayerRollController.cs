using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Forge;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    public enum RollSourceState { Idle = 0, Walk = 1, Sprint = 2, Attack = 3, Block = 4 }

    /// <summary>
    /// Controla la esquiva/roll direccional [Q]: gira limpiamente hacia la dirección activa (o dirección
    /// original de marcha), rueda sobre el eje frontal X sin torsión en Y ni atravesar el suelo,
    /// adapta alcance al estado previo del jugador y otorga 1.0s de invulnerabilidad.
    /// </summary>
    public sealed class PlayerRollController : MonoBehaviour
    {
        private const float RollDuration = 0.92f;
        private const float RollInvulnDuration = 1.00f;
        private const float RollCooldown = 0.22f;

        private static readonly int HashRollState = Animator.StringToHash("Roll");
        private static readonly int HashRollSpeed = Animator.StringToHash("RollSpeed");
        private static readonly int HashIsRolling = Animator.StringToHash("IsRolling");

        private PlayerMovement _movement;
        private PlayerHealthController _health;
        private PlayerStaminaController _stamina;
        private AttackController _attack;
        private PlayerEquipmentController _equip;
        private Transform _visualChild;
        private float _visualBaseY = -1.0f;

        private bool _isRolling, _usingFallbackGraph, _restoreFacingAfterRoll;
        private float _rollStartTime, _rollEndTime, _nextRollAllowedAt, _rollPeakSpeed, _restoreFacingUntil;
        private Vector3 _rollWorldDir = Vector3.forward;
        private Quaternion _rollRotation = Quaternion.identity, _preRollRotation = Quaternion.identity;

        private PlayableGraph _fallbackGraph;
        private static AnimationClip _cachedRollClip;

        public bool IsRolling => _isRolling;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>(); _health = GetComponent<PlayerHealthController>();
            _stamina = GetComponent<PlayerStaminaController>(); _attack = GetComponent<AttackController>(); _equip = GetComponent<PlayerEquipmentController>();
        }

        private void OnDestroy() => StopFallbackGraph();

        public bool TryStartRoll()
        {
            if (_isRolling || Time.time < _nextRollAllowedAt) return false;
            if (PauseMenuPanel.IsGamePaused || UI.Core.UIPanelBase.IsAnyPanelOpen || GameLoopController.IsEndScreenOpen) return false;
            if (_health != null && !_health.IsAlive) return false;
            if (GameStateController.IsForging) return false;
            if (AnvilController.Instance != null && (AnvilController.Instance.IsForging || AnvilController.Instance.HasIngotOnAnvil || AnvilController.Instance.IsCarvingHandle)) return false;
            if (_movement == null || !_movement.IsMovementEnabled || !_movement.IsGroundedPublic) return false;
            if (_stamina == null) _stamina = GetComponent<PlayerStaminaController>();
            if (_stamina != null && !_stamina.TryConsumeForRoll()) return false;

            RollSourceState srcState = ResolveAndPrepareSourceState();
            Vector3 liveDir = _movement.GetCurrentCameraRelativeMoveDir();
            Vector3 rollDir = liveDir.sqrMagnitude > 0.01f ? liveDir.normalized
                : (_movement.OriginalWalkDirection.sqrMagnitude > 0.01f ? _movement.OriginalWalkDirection.normalized : FlattenXZ(transform.forward));
            if (rollDir.sqrMagnitude < 0.001f) rollDir = Vector3.forward;

            Vector3 preFwd = FlattenXZ(transform.forward);
            _preRollRotation = preFwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(preFwd) : Quaternion.LookRotation(rollDir);
            _restoreFacingAfterRoll = liveDir.sqrMagnitude <= 0.01f || srcState == RollSourceState.Block || srcState == RollSourceState.Attack;

            _rollWorldDir = rollDir;
            _rollRotation = Quaternion.LookRotation(rollDir);
            transform.rotation = _rollRotation;
            _rollPeakSpeed = ComputeStatePeakSpeed(srcState);
            _rollStartTime = Time.time;
            _rollEndTime = Time.time + RollDuration;
            _nextRollAllowedAt = _rollEndTime + RollCooldown;
            _isRolling = true;

            CacheVisualChild();
            _health?.GrantRollInvulnerability(RollInvulnDuration);
            SetEdgeAlignerRolling(true);
            TriggerRollAnimation();
            ForgeAudioSystem.PlayWeaponSwing(transform.position, false);
            NotifyTutorialRoll();
            return true;
        }

        private RollSourceState ResolveAndPrepareSourceState()
        {
            if (_attack != null && _attack.IsAttacking) { _attack.CancelAttackForRoll(); return RollSourceState.Attack; }
            if (_attack != null && _attack.IsBlocking) { _attack.CancelBlockForRoll(); return RollSourceState.Block; }
            if (_movement.IsSprinting) return RollSourceState.Sprint;
            return _movement.MoveInput.sqrMagnitude > 0.01f ? RollSourceState.Walk : RollSourceState.Idle;
        }

        private float ComputeStatePeakSpeed(RollSourceState src)
        {
            float baseSpd = src switch
            {
                RollSourceState.Sprint => 9.8f,
                RollSourceState.Walk => 8.2f,
                RollSourceState.Attack => 7.6f,
                RollSourceState.Block => 7.2f,
                _ => 6.9f
            };
            float stanceMult = 1.0f;
            if (_equip != null && _equip.CurrentWeapon != null)
            {
                var st = _equip.CurrentWeapon.GetCombatStance();
                stanceMult = st == WeaponCombatStance.TwoHanded ? 0.86f : (st == WeaponCombatStance.Polearm ? 0.92f : 1.0f);
            }
            return baseSpd * stanceMult * (_movement.IsWebSlowed ? 0.65f : 1.0f);
        }

        private void Update()
        {
            if (!_isRolling)
            {
                if (_restoreFacingAfterRoll && Time.time < _restoreFacingUntil && _movement.MoveInput.sqrMagnitude <= 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, _preRollRotation, Time.deltaTime * 14f);
                return;
            }
            float u = Mathf.Clamp01((Time.time - _rollStartTime) / RollDuration);
            if (_visualChild != null)
            {
                Vector3 lp = _visualChild.localPosition;
                lp.y = _visualBaseY + 0.16f * Mathf.Sin(u * Mathf.PI);
                _visualChild.localPosition = lp;
            }
            if ((_health != null && !_health.IsAlive) || PauseMenuPanel.IsGamePaused || GameStateController.IsForging || Time.time >= _rollEndTime)
                EndRoll();
        }

        public void ApplyRollFixedPhysics(Rigidbody rb)
        {
            if (!_isRolling || rb == null || rb.isKinematic) return;
            float u = Mathf.Clamp01((Time.time - _rollStartTime) / RollDuration);
            float spd = u < 0.72f
                ? Mathf.Lerp(_rollPeakSpeed, _rollPeakSpeed * 0.56f, u / 0.72f)
                : Mathf.Lerp(_rollPeakSpeed * 0.56f, _movement.WalkSpeed * 0.35f, Mathf.SmoothStep(0f, 1f, (u - 0.72f) / 0.28f));

            rb.linearVelocity = new Vector3(_rollWorldDir.x * spd, Mathf.Min(0.20f, rb.linearVelocity.y), _rollWorldDir.z * spd);
            transform.rotation = _rollRotation;
        }

        private void EndRoll()
        {
            _isRolling = false;
            _restoreFacingUntil = Time.time + 0.22f;
            if (_visualChild != null)
            {
                Vector3 lp = _visualChild.localPosition; lp.y = _visualBaseY; _visualChild.localPosition = lp;
            }
            SetEdgeAlignerRolling(false);
            Animator anim = ResolveAnimator();
            if (anim != null)
            {
                if (_usingFallbackGraph) StopFallbackGraph();
                if (anim.HasState(0, HashRollState)) { anim.SetBool(HashIsRolling, false); anim.CrossFadeInFixedTime("Locomotion", 0.14f, 0); }
            }
        }

        private void TriggerRollAnimation()
        {
            Animator anim = ResolveAnimator();
            if (anim == null) return;
            if (anim.layerCount > 1) anim.SetLayerWeight(1, 0f);

            AnimationClip clip = GetRollClip();
            float speedMult = clip != null ? Mathf.Clamp(clip.length / RollDuration, 1.0f, 2.2f) : 1.60f;

            if (anim.HasState(0, HashRollState))
            {
                _usingFallbackGraph = false;
                anim.SetFloat(HashRollSpeed, speedMult);
                anim.SetBool(HashIsRolling, true);
                anim.CrossFadeInFixedTime("Roll", 0.05f, 0, 0f);
                return;
            }

            if (clip != null) PlayClipViaFallbackGraph(anim, clip, speedMult);
        }

        private void PlayClipViaFallbackGraph(Animator anim, AnimationClip clip, float speedMult)
        {
            StopFallbackGraph();
            _fallbackGraph = PlayableGraph.Create("PlayerRollFallback");
            var output = AnimationPlayableOutput.Create(_fallbackGraph, "RollAnim", anim);
            var playable = AnimationClipPlayable.Create(_fallbackGraph, clip);
            playable.SetSpeed(speedMult);
            output.SetSourcePlayable(playable);
            _fallbackGraph.Play();
            _usingFallbackGraph = true;
        }

        private void StopFallbackGraph()
        {
            _usingFallbackGraph = false;
            if (_fallbackGraph.IsValid()) _fallbackGraph.Destroy();
        }

        private static AnimationClip GetRollClip()
        {
            if (_cachedRollClip != null) return _cachedRollClip;
            var all = UnityEngine.Resources.LoadAll<AnimationClip>("Animations/Roll/UAL1_Standard");
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && (all[i].name == "Roll" || all[i].name.EndsWith("|Roll"))) { _cachedRollClip = all[i]; return _cachedRollClip; }
            }
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name.Contains("Roll")) { _cachedRollClip = all[i]; return _cachedRollClip; }
            return null;
        }

        private void CacheVisualChild()
        {
            if (_visualChild != null) return;
            _visualChild = transform.Find("PaladinVisual");
            if (_visualChild != null) _visualBaseY = _visualChild.localPosition.y;
        }

        private void SetEdgeAlignerRolling(bool active)
        {
            if (_equip == null) _equip = GetComponent<PlayerEquipmentController>();
            if (_equip?.EdgeAligner != null) _equip.EdgeAligner.isRolling = active;
        }

        private Animator ResolveAnimator() => _movement != null && _movement.CharacterAnimator != null
            ? _movement.CharacterAnimator : (_equip != null ? _equip.PaladinAnimator : GetComponentInChildren<Animator>());

        private static void NotifyTutorialRoll()
        {
            var tut = ForgeTutorialDirector.Instance;
            if (tut == null) return;
            tut.NotifyPlayerRolled();
            var spar = ForjaGame.Environment.TrainingDummyArenaBuilder.SparringDummy;
            if (spar != null && PlayerMovement.LocalInstance != null && Vector3.Distance(PlayerMovement.LocalInstance.transform.position, spar.transform.position) <= 6.2f)
                tut.NotifyDummyRolled();
        }

        private static Vector3 FlattenXZ(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
