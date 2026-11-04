using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador en el mismo GameObject del Animator (PaladinVisual) que gestiona:
    /// 1) Postura creíble de torso superior (mezcla orgánica entre reposo natural al caminar/respirar y guardia activa tras combatir)
    /// 2) Agarre real a dos manos con IK sobre el mango tallado (OnAnimatorIK)
    /// 3) Alineación dinámica del filo (+X) durante la ventana de corte (LateUpdate)
    /// </summary>
    public class DynamicEdgeAligner : MonoBehaviour
    {
        [Header("Configuración de Filo (+X) y Mango (+Y)")]
        public Transform weaponSocket;
        public Transform frontGripSocket;
        public float weaponLengthY = 0.85f;
        public bool enableDynamicAlignment = true;

        [Header("Agarre y Postura (1H / 2H / Pico)")]
        public bool isTwoHandedWeapon = false;
        public bool isPickaxeOrTool = false;
        public float secondaryGripDistanceOnShaft = 0.34f;

        [HideInInspector] public bool isAttacking = false;

        private Animator _anim;
        private Transform _hipsBone;
        private Transform _chestBone;
        private Vector3 _lastTipWorldPos;
        private Quaternion _calibratedLocalRotation;
        private bool _hasCalibratedBase = false;

        private float _currentSmoothTwist = 0f;
        private float _twistVelocity = 0f;
        private float _attackTimer = 0f;
        private float _combatAlertUntil = -10f;
        private float _smoothedUpperWeight = 0.25f;

        private void Awake()
        {
            BindAnimator();
        }

        public void BindAnimator()
        {
            if (_anim == null)
                _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>() ?? GetComponentInParent<Animator>();
            if (_anim != null && _anim.isHuman)
            {
                _hipsBone = _anim.GetBoneTransform(HumanBodyBones.Hips);
                _chestBone = _anim.GetBoneTransform(HumanBodyBones.Chest);
            }
        }

        public void NotifyCombatAlert(float duration = 5.0f)
        {
            _combatAlertUntil = Mathf.Max(_combatAlertUntil, Time.time + duration);
        }

        public void CaptureCalibratedRotation()
        {
            if (weaponSocket != null)
            {
                _calibratedLocalRotation = weaponSocket.localRotation;
                _hasCalibratedBase = true;
                _currentSmoothTwist = 0f;
            }
        }

        private void Update()
        {
            BindAnimator();
            if (_anim == null || _anim.layerCount <= 1) return;

            if (weaponSocket == null)
            {
                _anim.SetLayerWeight(1, 0f);
                return;
            }

            if (isAttacking)
            {
                NotifyCombatAlert(5.0f);
                // Para armas de combate el golpe se reproduce en capa 1; para el pico en capa 0
                float atkWeight = isPickaxeOrTool ? 0f : 1.0f;
                _smoothedUpperWeight = Mathf.MoveTowards(_smoothedUpperWeight, atkWeight, Time.deltaTime * 12f);
                _anim.SetLayerWeight(1, _smoothedUpperWeight);
                return;
            }

            float locoSpeed = _anim.GetFloat("Speed");
            bool isMoving = locoSpeed > 0.15f;
            bool inCombatAlert = !isPickaxeOrTool && Time.time < _combatAlertUntil;

            float targetUpperWeight;
            if (isPickaxeOrTool)
            {
                targetUpperWeight = isMoving ? 0.12f : 0.18f;
            }
            else if (isTwoHandedWeapon)
            {
                // En reposo deja respirar y caminar naturalmente al torso mientras el IK sostiene el arma a 2 manos
                targetUpperWeight = inCombatAlert ? (isMoving ? 0.56f : 0.78f) : (isMoving ? 0.28f : 0.42f);
            }
            else
            {
                // Arma 1H: postura relajada natural fuera de combate; guardia firme tras atacar/recibir daño
                targetUpperWeight = inCombatAlert ? (isMoving ? 0.44f : 0.72f) : (isMoving ? 0.15f : 0.22f);
            }

            _smoothedUpperWeight = Mathf.Lerp(_smoothedUpperWeight, targetUpperWeight, Time.deltaTime * 5.5f);
            _anim.SetLayerWeight(1, _smoothedUpperWeight);
        }

        private void LateUpdate()
        {
            if (weaponSocket == null || !enableDynamicAlignment) return;
            if (!_hasCalibratedBase) CaptureCalibratedRotation();

            _attackTimer = isAttacking ? (_attackTimer + Time.deltaTime) : 0f;
            weaponSocket.localRotation = _calibratedLocalRotation;

            Vector3 currentTipPos = weaponSocket.position + (weaponSocket.up * weaponLengthY);
            Vector3 tipVelocity = (currentTipPos - _lastTipWorldPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            _lastTipWorldPos = currentTipPos;

            float targetTwist = 0f;
            if (isAttacking && _attackTimer > 0.12f && _attackTimer < 0.55f && tipVelocity.sqrMagnitude > 2.0f)
            {
                if (Vector3.Dot(tipVelocity.normalized, transform.forward) > -0.25f)
                {
                    Vector3 handleAxis = weaponSocket.up;
                    Vector3 swingDirOnPlane = Vector3.ProjectOnPlane(tipVelocity.normalized, handleAxis).normalized;
                    if (swingDirOnPlane.sqrMagnitude > 0.01f)
                    {
                        float rawAngle = Vector3.SignedAngle(weaponSocket.right, swingDirOnPlane, handleAxis);
                        if (rawAngle > 90f) rawAngle -= 180f;
                        if (rawAngle < -90f) rawAngle += 180f;
                        targetTwist = Mathf.Clamp(rawAngle * 0.65f, -26f, 26f);
                    }
                }
            }

            _currentSmoothTwist = Mathf.SmoothDampAngle(_currentSmoothTwist, targetTwist, ref _twistVelocity, 0.09f);
            weaponSocket.localRotation = _calibratedLocalRotation * Quaternion.AngleAxis(_currentSmoothTwist, Vector3.up);

            if (isAttacking && _hipsBone != null && _chestBone != null)
            {
                float attackPhase = Mathf.Sin(Mathf.Clamp01(_attackTimer / 0.65f) * Mathf.PI);
                _hipsBone.rotation = Quaternion.Slerp(_hipsBone.rotation, _chestBone.rotation, attackPhase * 0.35f);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_anim == null || weaponSocket == null || !isTwoHandedWeapon || isPickaxeOrTool)
            {
                if (_anim != null)
                {
                    _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                    _anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                }
                return;
            }

            float ikWeight = isAttacking ? 0.90f : 0.84f;
            Vector3 gripPos = frontGripSocket != null
                ? frontGripSocket.position
                : (weaponSocket.position + weaponSocket.up * secondaryGripDistanceOnShaft);

            _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, ikWeight);
            _anim.SetIKPosition(AvatarIKGoal.LeftHand, gripPos);

            Quaternion gripRot = Quaternion.LookRotation(weaponSocket.up, -weaponSocket.forward);
            _anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, ikWeight * 0.55f);
            _anim.SetIKRotation(AvatarIKGoal.LeftHand, gripRot);
        }
    }
}
