using System;
using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Animador procedural cinemático para armas cuerpo a cuerpo.
    /// Soporta combos fluidos por postura (Espada 5 hits, Martillo, Mandoble 2H, Polearm)
    /// y adapta la velocidad e inercia según el peso del arma y composición del mineral.
    /// </summary>
    public partial class WeaponComboAnimator : MonoBehaviour
    {
        public static WeaponComboAnimator Instance { get; private set; }

        [Header("Oscilación en Guardia (Idle Sway)")]
        [SerializeField] private float idleSwaySpeed = 2.0f;
        [SerializeField] private float idleSwayAmount = 0.008f;
        [SerializeField] private float idleRotSwayAmount = 1.0f;

        [Header("Anti-Clipping y Separación")]
        [SerializeField] private float minOrbitalRadius = 0.40f;
        [SerializeField] private float slashArcBulge = 0.08f;

        private PlayerEquipmentController _equipment;
        private Transform _holder;
        private Vector3 _baseGuardPos;
        private Vector3 _baseGuardRot;

        private bool _isAttacking = false;
        private int _currentComboIndex = 0;
        private float _attackStartTime = 0f;
        private float _attackDuration = 0.6f;
        private Action _onHitCallback;
        private bool _hasTriggeredHit = false;

        private Vector3 _strikeStartPos;
        private Quaternion _strikeStartRot;

        public bool IsAttacking => _isAttacking;
        public int CurrentComboIndex => _currentComboIndex;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _equipment = GetComponent<PlayerEquipmentController>();
        }

        private void Update()
        {
            if (_equipment == null) _equipment = GetComponent<PlayerEquipmentController>();
            if (_equipment == null || !_equipment.HasWeaponEquipped) return;

            _holder = _equipment.WeaponHolder;
            if (_holder == null) return;

            var stance = _equipment.CurrentWeapon.GetCombatStance();
            (_baseGuardPos, _baseGuardRot, _) = _equipment.GetStanceCalibration(_equipment.CurrentWeapon, stance);

            if (!_isAttacking)
            {
                UpdateIdleSway();
            }
            else
            {
                UpdateAttackMotion();
            }
        }

        private void UpdateIdleSway()
        {
            float weight = _equipment?.CurrentWeapon != null ? _equipment.CurrentWeapon.GetNormalizedWeight() : 0.5f;
            float swaySpeed = idleSwaySpeed * Mathf.Lerp(1.3f, 0.7f, weight);
            float yWeightDip = Mathf.Lerp(0f, -0.035f, weight);

            float t = Time.time * swaySpeed;
            float yOffset = Mathf.Sin(t) * idleSwayAmount + yWeightDip;
            float xOffset = Mathf.Cos(t * 0.5f) * (idleSwayAmount * 0.5f);
            float rotZ = Mathf.Sin(t) * (idleRotSwayAmount * Mathf.Lerp(1.2f, 0.6f, weight));

            Vector3 targetPos = _baseGuardPos + new Vector3(xOffset, yOffset, 0f);
            Quaternion targetRot = Quaternion.Euler(_baseGuardRot.x, _baseGuardRot.y, _baseGuardRot.z + rotZ);

            _holder.localPosition = Vector3.Lerp(_holder.localPosition, targetPos, Time.deltaTime * 6f);
            _holder.localRotation = Quaternion.Slerp(_holder.localRotation, targetRot, Time.deltaTime * 6f);
        }

        public void PlayStrike(int comboIndex, float duration, Action onHitCallback)
        {
            if (_holder == null && _equipment != null) _holder = _equipment.WeaponHolder;
            if (_holder == null) return;

            _isAttacking = true;
            _currentComboIndex = comboIndex;
            _attackDuration = Mathf.Max(0.20f, duration);
            _attackStartTime = Time.time;
            _onHitCallback = onHitCallback;
            _hasTriggeredHit = false;

            _strikeStartPos = _holder.localPosition;
            _strikeStartRot = _holder.localRotation;
        }

        private void UpdateAttackMotion()
        {
            float elapsed = Time.time - _attackStartTime;
            float normTime = Mathf.Clamp01(elapsed / _attackDuration);

            GetPoseForKeyframe(_currentComboIndex, normTime, out Vector3 targetPos, out Quaternion targetRot, out float hitApex);

            if (normTime >= hitApex && !_hasTriggeredHit)
            {
                _hasTriggeredHit = true;
                _onHitCallback?.Invoke();
            }

            _holder.localPosition = targetPos;
            _holder.localRotation = targetRot;

            if (normTime >= 1.0f) _isAttacking = false;
        }

        private void GetPoseForKeyframe(int comboIndex, float t, out Vector3 targetPos, out Quaternion targetRot, out float hitApex)
        {
            Vector3 guardPos = _baseGuardPos;
            Quaternion guardRot = Quaternion.Euler(_baseGuardRot);

            var style = _equipment?.CurrentWeapon != null ? _equipment.CurrentWeapon.GetAnimationStyle() : WeaponAnimationStyle.Sword1H;

            switch (style)
            {
                case WeaponAnimationStyle.Axe1H:
                    GetAxeKeyframe(comboIndex, t, guardPos, guardRot, out targetPos, out targetRot, out hitApex);
                    break;
                case WeaponAnimationStyle.Dagger1H:
                    GetDaggerKeyframe(comboIndex, t, guardPos, guardRot, out targetPos, out targetRot, out hitApex);
                    break;
                case WeaponAnimationStyle.Hammer1H:
                    GetHammerKeyframe(comboIndex, t, guardPos, guardRot, out targetPos, out targetRot, out hitApex);
                    break;
                case WeaponAnimationStyle.Heavy2H:
                    GetHeavy2HKeyframe(comboIndex, t, guardPos, guardRot, out targetPos, out targetRot, out hitApex);
                    break;
                case WeaponAnimationStyle.Polearm:
                    GetPolearmKeyframe(comboIndex, t, guardPos, guardRot, out targetPos, out targetRot, out hitApex);
                    break;
                default:
                    GetSwordKeyframe(comboIndex, t, guardPos, guardRot, out targetPos, out targetRot, out hitApex);
                    break;
            }
        }

        private Vector3 ComputeOrbitalArc(Vector3 p0, Vector3 p1, float u)
        {
            Vector3 mid = (p0 + p1) * 0.5f;
            Vector3 outward = new Vector3(mid.x, 0f, Mathf.Max(0.42f, mid.z)).normalized;
            Vector3 controlPoint = mid + outward * slashArcBulge;

            float rad = Mathf.Sqrt(controlPoint.x * controlPoint.x + controlPoint.z * controlPoint.z);
            if (rad < minOrbitalRadius)
            {
                controlPoint.x *= (minOrbitalRadius / rad);
                controlPoint.z *= (minOrbitalRadius / rad);
            }

            float omt = 1f - u;
            return omt * omt * p0 + 2f * omt * u * controlPoint + u * u * p1;
        }

        public void CancelOrReturnToGuard() => _isAttacking = false;

        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseOutExpo(float t) => Mathf.Approximately(t, 1f) ? 1f : 1f - Mathf.Pow(2f, -10f * t);
    }
}
