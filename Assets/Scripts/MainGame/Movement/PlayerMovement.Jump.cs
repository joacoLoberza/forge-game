using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ForjaGame.Core
{
    public partial class PlayerMovement
    {
        private const float JumpUpwardSpeed = 6.9f;
        private const float ExtraAirGravity = 13.5f;
        private static readonly int HashJumpStart = Animator.StringToHash("Jump_Start");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");

        private int _lastJumpFrame = -1;
        private float _lastJumpTime = -10f;
        private bool _isAirborneJump, _jumpFallbackActive;
        private PlayableGraph _jumpFallbackGraph;
        private static AnimationClip _cachedJumpStartClip, _cachedJumpLoopClip;
        private static readonly RaycastHit[] _groundHits = new RaycastHit[8];

        private bool IsGrounded()
        {
            int count = Physics.RaycastNonAlloc(transform.position, Vector3.down, _groundHits, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _groundHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<ForjaGame.Combat.CaveCreatureBase>() != null) continue;
                return true;
            }
            return false;
        }

        private void ExecuteJump()
        {
            if (!_movementEnabled || _rb == null || _rb.isKinematic) return;
            if (_rollCtrl != null && _rollCtrl.IsRolling) return;
            if (Time.frameCount == _lastJumpFrame || Time.time < _lastJumpTime + 0.30f) return;
            if (!IsGrounded() || _rb.linearVelocity.y > 1.25f) return;

            _lastJumpFrame = Time.frameCount;
            _lastJumpTime = Time.time;
            _isAirborneJump = true;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, JumpUpwardSpeed, _rb.linearVelocity.z);
            TriggerJumpAnimation();
        }

        private void UpdateJumpPhysics()
        {
            if (_rb == null || _rb.isKinematic) return;
            bool airborne = !IsGrounded() || _rb.linearVelocity.y > 0.25f;
            if (airborne)
            {
                float gMult = _rb.linearVelocity.y < 0f ? 1.22f : 1.0f;
                _rb.linearVelocity += Vector3.up * (-ExtraAirGravity * gMult * Time.fixedDeltaTime);
            }
        }

        private void UpdateJumpAnimation()
        {
            if (animator == null) return;
            bool grounded = IsGrounded() && (_rb == null || _rb.linearVelocity.y <= 1.0f);
            if (HasAnimatorParam(animator, HashIsGrounded)) animator.SetBool(HashIsGrounded, grounded);

            if (!_isAirborneJump) return;
            float airAge = Time.time - _lastJumpTime;
            if ((grounded && airAge > 0.14f) || airAge > 1.25f)
            {
                _isAirborneJump = false;
                if (_jumpFallbackActive) StopJumpFallbackGraph();
                if (animator.HasState(0, Animator.StringToHash("Locomotion")))
                    animator.CrossFadeInFixedTime("Locomotion", 0.14f, 0);
            }
        }

        private void TriggerJumpAnimation()
        {
            if (animator == null) return;
            if (animator.layerCount > 1 && _attackCtrl != null && !_attackCtrl.IsAttacking)
                animator.SetLayerWeight(1, 0f);

            bool running = _moveInput.sqrMagnitude > 0.05f;
            float normStart = running ? 0.26f : 0.14f;
            if (animator.HasState(0, HashJumpStart))
            {
                if (_jumpFallbackActive) StopJumpFallbackGraph();
                if (HasAnimatorParam(animator, HashIsGrounded)) animator.SetBool(HashIsGrounded, false);
                animator.CrossFadeInFixedTime("Jump_Start", 0.08f, 0, normStart);
                return;
            }

            AnimationClip clip = GetJumpClip(running);
            if (clip != null) PlayJumpViaFallbackGraph(clip, running ? 2.1f : 1.85f, normStart);
        }

        private void PlayJumpViaFallbackGraph(AnimationClip clip, float speedMult, float normStart)
        {
            StopJumpFallbackGraph();
            _jumpFallbackGraph = PlayableGraph.Create("PlayerJumpFallback");
            var output = AnimationPlayableOutput.Create(_jumpFallbackGraph, "JumpAnim", animator);
            var playable = AnimationClipPlayable.Create(_jumpFallbackGraph, clip);
            playable.SetTime(clip.length * normStart);
            playable.SetSpeed(speedMult);
            output.SetSourcePlayable(playable);
            _jumpFallbackGraph.Play();
            _jumpFallbackActive = true;
        }

        private void StopJumpFallbackGraph()
        {
            _jumpFallbackActive = false;
            if (_jumpFallbackGraph.IsValid()) _jumpFallbackGraph.Destroy();
        }

        private static AnimationClip GetJumpClip(bool preferLoop)
        {
            if (preferLoop && _cachedJumpLoopClip != null) return _cachedJumpLoopClip;
            if (!preferLoop && _cachedJumpStartClip != null) return _cachedJumpStartClip;
            var all = UnityEngine.Resources.LoadAll<AnimationClip>("Animations/Roll/UAL1_Standard");
            if (all == null) return null;
            foreach (var c in all)
            {
                if (c == null) continue;
                if (c.name.Contains("Jump_Start")) _cachedJumpStartClip = c;
                else if (c.name.Contains("Jump_Loop")) _cachedJumpLoopClip = c;
            }
            return preferLoop ? (_cachedJumpLoopClip ?? _cachedJumpStartClip) : (_cachedJumpStartClip ?? _cachedJumpLoopClip);
        }

        private static bool HasAnimatorParam(Animator a, int hash)
        {
            if (a == null || a.runtimeAnimatorController == null) return false;
            var ps = a.parameters;
            for (int i = 0; i < ps.Length; i++) if (ps[i].nameHash == hash) return true;
            return false;
        }
    }
}
