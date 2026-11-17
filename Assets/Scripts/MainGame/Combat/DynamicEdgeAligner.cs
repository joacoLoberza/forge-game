using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador en el mismo GameObject del Animator (PaladinVisual) que gestiona:
    /// 1) Postura ponderada de transporte y combate (evitando agitar armas como papel y clipping de brazos al caminar/correr).
    /// 2) Agarre real a dos manos con IK sobre el mango tallado tanto en transporte como durante todos los ataques 2H.
    /// 3) Alineación dinámica del filo (+X) y barrido de colisión sobre la hoja real durante el golpe.
    /// </summary>
    public partial class DynamicEdgeAligner : MonoBehaviour
    {
        [Header("Configuración de Filo (+X) y Mango (+Y)")]
        public Transform weaponSocket;
        public Transform frontGripSocket;
        public float weaponLengthY = 0.85f;
        public bool enableDynamicAlignment = true;

        [Header("Agarre y Postura (1H / 2H / Pico)")]
        public bool isTwoHandedWeapon = false;
        public bool isPickaxeOrTool = false;
        public WeaponType weaponArchetype = WeaponType.Sword;
        public bool isFrontFaceDominant = true;
        public float secondaryGripDistanceOnShaft = 0.34f;

        [HideInInspector] public bool isAttacking = false;
        [HideInInspector] public bool isUpperBodyAttack = false;
        [HideInInspector] public bool isBlocking = false;
        [HideInInspector] public bool isRolling = false;

        private Animator _anim;
        private AttackController _attackCtrl;
        private Transform _hipsBone, _chestBone, _rightHandBone, _leftHandBone;
        private Transform _rightUpperArm, _rightLowerArm, _leftUpperArm, _leftLowerArm;
        private Vector3 _lastTipWorldPos;
        private Quaternion _calibratedLocalRotation;
        private bool _hasCalibratedBase = false;

        private float _currentSmoothTwist, _twistVelocity, _attackTimer;
        private float _combatAlertUntil = -10f;
        private float _smoothedUpperWeight = 0.25f;
        private float _blockBlend, _bashTimer, _parryTimer;

        private void Awake() => BindAnimator();

        public void BindAnimator()
        {
            if (_anim == null)
                _anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>() ?? GetComponentInParent<Animator>();
            if (_anim != null && _anim.isHuman && _hipsBone == null)
            {
                _hipsBone = _anim.GetBoneTransform(HumanBodyBones.Hips);
                _chestBone = _anim.GetBoneTransform(HumanBodyBones.Chest);
                _rightHandBone = _anim.GetBoneTransform(HumanBodyBones.RightHand);
                _leftHandBone = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
                _rightUpperArm = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _rightLowerArm = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _leftUpperArm = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _leftLowerArm = _anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            }
        }

        public void NotifyCombatAlert(float duration = 5.0f) => _combatAlertUntil = Mathf.Max(_combatAlertUntil, Time.time + duration);
        public void TriggerBashAnimation() { _bashTimer = 0.38f; NotifyCombatAlert(5.0f); }
        public void TriggerParryAnimation() { _parryTimer = 0.32f; NotifyCombatAlert(5.0f); }

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
            if (_bashTimer > 0f) _bashTimer = Mathf.Max(0f, _bashTimer - Time.deltaTime);
            if (_parryTimer > 0f) _parryTimer = Mathf.Max(0f, _parryTimer - Time.deltaTime);
            _blockBlend = Mathf.MoveTowards(_blockBlend, (isBlocking && !isAttacking && !isPickaxeOrTool) ? 1f : 0f, Time.deltaTime * 11f);

            if (_anim == null || _anim.layerCount <= 1) return;
            if (weaponSocket == null) { _anim.SetLayerWeight(1, 0f); return; }

            if (isRolling)
            {
                _blockBlend = 0f; _bashTimer = 0f; _parryTimer = 0f; isAttacking = false;
                _smoothedUpperWeight = 0f; _anim.SetLayerWeight(1, 0f);
                if (_anim.layerCount > 2) _anim.SetLayerWeight(2, 0f);
                return;
            }
            if (isAttacking)
            {
                NotifyCombatAlert(5.0f);
                float targetLayer1 = isUpperBodyAttack ? 1.0f : 0f;
                _smoothedUpperWeight = targetLayer1;
                _anim.SetLayerWeight(1, targetLayer1);
                if (_anim.layerCount > 2) _anim.SetLayerWeight(2, isUpperBodyAttack ? 0.45f : 0f);
                return;
            }
            if (_bashTimer > 0f || _blockBlend > 0.05f)
            {
                NotifyCombatAlert(5.0f);
                _smoothedUpperWeight = Mathf.MoveTowards(_smoothedUpperWeight, 1.0f, Time.deltaTime * 12f);
                _anim.SetLayerWeight(1, _smoothedUpperWeight);
                if (_anim.layerCount > 2) _anim.SetLayerWeight(2, 0.72f);
                return;
            }

            float locoSpeed = _anim.GetFloat("Speed");
            bool isMoving = locoSpeed > 0.15f;
            bool inCombatAlert = !isPickaxeOrTool && Time.time < _combatAlertUntil;

            // Mantiene postura de transporte ponderada al caminar/correr para evitar agitación exagerada y clipping de brazos
            float targetUpperWeight = isMoving
                ? (isTwoHandedWeapon ? (isPickaxeOrTool ? 0.74f : 0.84f) : 0.66f)
                : (isTwoHandedWeapon ? 0.68f : (inCombatAlert ? 0.45f : 0.26f));
            _smoothedUpperWeight = Mathf.Lerp(_smoothedUpperWeight, targetUpperWeight, Time.deltaTime * 8.5f);
            _anim.SetLayerWeight(1, _smoothedUpperWeight);
            if (_anim.layerCount > 2) _anim.SetLayerWeight(2, isMoving ? 0.72f : 0.76f);
        }

        private void LateUpdate()
        {
            if (isRolling || weaponSocket == null || !enableDynamicAlignment) return;
            if (!_hasCalibratedBase) CaptureCalibratedRotation();
            if (_attackCtrl == null) _attackCtrl = GetComponentInParent<AttackController>();

            _attackTimer = isAttacking ? (_attackTimer + Time.deltaTime) : 0f;
            weaponSocket.localRotation = _calibratedLocalRotation;

            if (!isAttacking && _blockBlend <= 0.02f)
                ApplyLocomotionArmClearanceAndWeight();
            else if (isAttacking && _attackCtrl != null && _attackCtrl.IsAttacking)
                ApplyAttackSwingArcEnhancement(_attackCtrl.ActiveMove, _attackCtrl.AttackProgress);

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
                        float rawAngle = Vector3.SignedAngle(weaponSocket.forward, swingDirOnPlane, handleAxis);
                        if (rawAngle > 90f) rawAngle -= 180f;
                        if (rawAngle < -90f) rawAngle += 180f;
                        targetTwist = Mathf.Clamp(rawAngle * 0.55f, -22f, 22f);
                    }
                }
            }

            _currentSmoothTwist = Mathf.SmoothDampAngle(_currentSmoothTwist, targetTwist, ref _twistVelocity, 0.09f);
            Quaternion finalSocketRot = _calibratedLocalRotation * Quaternion.AngleAxis(_currentSmoothTwist, Vector3.up);

            // Animación procedural de Guardia / Parry / Empujón adaptada al tipo de arma (Espada 1H vs Hacha/Martillo vs 2H)
            if (_blockBlend > 0.01f || _bashTimer > 0f || _parryTimer > 0f)
            {
                ApplyGuardParryAndBashPose(ref finalSocketRot);
            }

            weaponSocket.localRotation = finalSocketRot;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_anim == null || weaponSocket == null || isRolling)
            {
                if (_anim != null) { ClearHandIK(AvatarIKGoal.RightHand); ClearHandIK(AvatarIKGoal.LeftHand); }
                return;
            }

            if (isAttacking)
            {
                ClearHandIK(AvatarIKGoal.RightHand);
                if (isTwoHandedWeapon && !isPickaxeOrTool && (layerIndex == 1 || (layerIndex == 0 && !isUpperBodyAttack)))
                    ApplyLeftHandGripIK(0.90f);
                else
                    ClearHandIK(AvatarIKGoal.LeftHand);
                return;
            }

            if (!isPickaxeOrTool && _blockBlend > 0.02f && _rightHandBone != null)
            {
                if (layerIndex != 1) return;
                Vector3 guardHandPos = transform.position + transform.up * 1.18f + transform.right * (isTwoHandedWeapon ? 0.14f : 0.20f) + transform.forward * 0.42f;
                Vector3 bladeDir = (-0.66f * transform.right + 0.70f * transform.up + 0.28f * transform.forward).normalized;
                Vector3 faceDir = (transform.forward * 0.90f + transform.up * 0.30f + transform.right * 0.28f).normalized;
                Vector3 orthF = Vector3.Cross(Vector3.Cross(bladeDir, faceDir).normalized, bladeDir).normalized;
                Quaternion targetSocketWorldRot = Quaternion.LookRotation(orthF, bladeDir);
                Quaternion handToSocket = Quaternion.Inverse(_rightHandBone.rotation) * weaponSocket.rotation;
                Quaternion targetHandRot = targetSocketWorldRot * Quaternion.Inverse(handToSocket);

                float rw = _blockBlend * 0.90f;
                _anim.SetIKPositionWeight(AvatarIKGoal.RightHand, rw);
                _anim.SetIKPosition(AvatarIKGoal.RightHand, guardHandPos);
                _anim.SetIKRotationWeight(AvatarIKGoal.RightHand, rw);
                _anim.SetIKRotation(AvatarIKGoal.RightHand, targetHandRot);

                bool twoHandBrace = isTwoHandedWeapon || weaponArchetype == WeaponType.Axe || weaponArchetype == WeaponType.Hammer;
                Vector3 bracePos = twoHandBrace && frontGripSocket != null ? frontGripSocket.position
                    : (guardHandPos + bladeDir * (twoHandBrace ? 0.26f : 0.18f) - transform.forward * 0.04f);
                _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, _blockBlend * 0.85f);
                _anim.SetIKPosition(AvatarIKGoal.LeftHand, bracePos);
                _anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, twoHandBrace ? _blockBlend * 0.45f : 0f);
                if (twoHandBrace) _anim.SetIKRotation(AvatarIKGoal.LeftHand, Quaternion.LookRotation(bladeDir, -transform.forward));
                return;
            }

            ClearHandIK(AvatarIKGoal.RightHand);
            if (layerIndex != 0 && layerIndex != 1) return;
            if (!isTwoHandedWeapon) { ClearHandIK(AvatarIKGoal.LeftHand); return; }

            ApplyLeftHandGripIK(0.86f);
        }

        private void ApplyLeftHandGripIK(float ikWeight)
        {
            Vector3 gripPos = frontGripSocket != null ? frontGripSocket.position : (weaponSocket.position + weaponSocket.up * secondaryGripDistanceOnShaft);
            _anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, ikWeight);
            _anim.SetIKPosition(AvatarIKGoal.LeftHand, gripPos);
            _anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, ikWeight * 0.55f);
            _anim.SetIKRotation(AvatarIKGoal.LeftHand, Quaternion.LookRotation(weaponSocket.up, -weaponSocket.forward));
        }

        private void ClearHandIK(AvatarIKGoal goal) { _anim.SetIKPositionWeight(goal, 0f); _anim.SetIKRotationWeight(goal, 0f); }
    }
}
