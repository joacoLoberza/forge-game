using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public partial class PlayerCombatIKDriver
    {
        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || _currentIkWeight <= 0.001f) return;
            if (!_isEquipped || _weaponHolder == null || _chestBone == null) return;

            Transform playerTransform = transform.parent != null ? transform.parent : transform;
            float dt = Time.deltaTime;

            Vector3 targetGripChest;
            Quaternion targetRotChest;
            TorsoAngles targetTorso;
            Vector3 velChest;

            if (_isAttacking && _currentMove != null)
            {
                MoveSolver.EvaluateMove(
                    _currentMove,
                    _attackProgress,
                    _rightArmLength,
                    _analysis.headType,
                    out targetGripChest,
                    out targetRotChest,
                    out targetTorso,
                    out velChest);
            }
            else
            {
                // Retorno armónico hacia la guardia (Critically Damped Spring)
                targetTorso = TorsoAngles.Identity;
                velChest = Vector3.zero;

                Vector3 currentLocal = _chestBone.InverseTransformPoint(_weaponHolder.position);
                targetGripChest = MoveSolver.SpringTowards(currentLocal, _guardGripChest, ref _springVelocity, returnToGuardTime, dt);
                targetRotChest = Quaternion.Slerp(Quaternion.Inverse(_chestBone.rotation) * _weaponHolder.rotation, _guardRotChest, dt * 8f);
            }

            // 1. APLICAR TORSO PRIMERO (Caderas 20%, Columna 35%, Pecho 45%)
            _torsoDriver?.ApplyTorsoPose(targetTorso, _analysis.inertia, dt, playerTransform);

            // 2. POSE DEL ARMA EN ESPACIO DEL PECHO -> MUNDO
            Vector3 worldGrip = _chestBone.TransformPoint(targetGripChest);
            Quaternion worldRot = _chestBone.rotation * targetRotChest;

            // Filtro SmoothDamp final según inercia (0.03s ligera, 0.08s pesada)
            float filterTime = Mathf.Lerp(0.03f, 0.08f, Mathf.Clamp01(_analysis.inertia / 3.5f));
            _weaponHolder.position = Vector3.Lerp(_weaponHolder.position, worldGrip, dt / filterTime);
            _weaponHolder.rotation = Quaternion.Slerp(_weaponHolder.rotation, worldRot, dt / filterTime);

            // 3. DESLIZAMIENTO DE AGARRE EN ASTA
            bool isPolearm = _currentStance == WeaponCombatStance.Polearm;
            if (isPolearm && _gripSocketFront != null)
            {
                float slide = TorsoDriver.CalculatePolearmGripSlide(
                    _attackProgress,
                    _currentMove != null ? _currentMove.motionType : AttackMotionType.Thrust,
                    0.35f);
                _gripSocketFront.localPosition = new Vector3(0f, slide, 0f);
            }

            // 4. ALCANCE Y ASISTENCIA ESCALONADA (Hombro -> Torso -> Caderas)
            Vector3 targetR = _gripSocketRear != null ? _gripSocketRear.position : _weaponHolder.position;
            if (_rightUpperArm != null)
            {
                ArmIK.ApplyReachAssistance(
                    _rightShoulder, _spineBone, _hipsBone,
                    ref targetR, _rightUpperArm.position, _rightArmLength, _weaponHolder);
            }

            // 5. VERIFICACIÓN BIMANUAL EN 2H
            bool isBimanual = _currentStance == WeaponCombatStance.TwoHanded || isPolearm;
            Vector3 targetL = _gripSocketFront != null ? _gripSocketFront.position : targetR;
            if (isBimanual && _rightUpperArm != null && _leftUpperArm != null)
            {
                ArmIK.EnsureBimanualReachable(
                    _rightUpperArm.position, _leftUpperArm.position,
                    ref targetR, ref targetL,
                    _rightArmLength, _leftArmLength, _weaponHolder);
            }

            // 6. POLE DE CODOS CON CÁPSULA ANTI-PENETRACIÓN DEL TORSO
            Vector3 rPole = ArmIK.CalculateElbowPoleWithTorsoCapsule(_chestBone, _rightUpperArm.position, targetR, true);
            Vector3 lPole = ArmIK.CalculateElbowPoleWithTorsoCapsule(_chestBone, _leftUpperArm.position, targetL, false);

            // 7. TWO-BONE IK DE BRAZOS
            ArmIK.SolveTwoBone(_rightUpperArm, _rightForearm, _rightHand, targetR, rPole, _currentIkWeight);

            if (isBimanual)
            {
                ArmIK.SolveTwoBone(_leftUpperArm, _leftForearm, _leftHand, targetL, lPole, _currentIkWeight);
            }
            else
            {
                // Mano libre en 1H con pose de contrapeso atlético
                Vector3 freeHandChest = TorsoDriver.CalculateFreeHandCounterweight(
                    _attackProgress,
                    _currentMove != null ? _currentMove.motionType : AttackMotionType.HorizontalSlash,
                    _leftArmLength);
                Vector3 worldFreeHand = _chestBone.TransformPoint(freeHandChest);
                ArmIK.SolveTwoBone(_leftUpperArm, _leftForearm, _leftHand, worldFreeHand, lPole, _currentIkWeight);
            }

            // 8. LÍMITES DE MUÑECA Y REPARTO DE PRONACIÓN
            Quaternion rOffsetRot = Quaternion.Euler(rightHandOffsetRot);
            Quaternion targetHandRotR = _weaponHolder.rotation * Quaternion.Inverse(rOffsetRot);
            ArmIK.ApplyWristAndPronationLimits(_rightForearm, _rightHand, targetHandRotR, _currentIkWeight);

            if (isBimanual && _gripSocketFront != null)
            {
                Quaternion lOffsetRot = Quaternion.Euler(leftHandOffsetRot);
                Quaternion targetHandRotL = _gripSocketFront.rotation * Quaternion.Inverse(lOffsetRot);
                ArmIK.ApplyWristAndPronationLimits(_leftForearm, _leftHand, targetHandRotL, _currentIkWeight);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null) return;

            // Desactivar IK de pies nativo en Mecanim para que la locomoción gobierne las piernas firmes
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);

            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        }
    }
}
