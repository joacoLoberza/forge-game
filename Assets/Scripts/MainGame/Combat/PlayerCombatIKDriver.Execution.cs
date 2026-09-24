using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public partial class PlayerCombatIKDriver
    {
        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || _currentIkWeight <= 0.001f) return;
            if (!_isEquipped || _chestBone == null || _rightHand == null) return;

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

                Vector3 currentHandChest = _chestBone.InverseTransformPoint(_rightHand.position);
                targetGripChest = MoveSolver.SpringTowards(currentHandChest, _guardGripChest, ref _springVelocity, returnToGuardTime, dt);
                targetRotChest = _guardRotChest;
            }

            // 1. APLICAR TORSO PRIMERO (Caderas 20%, Columna 35%, Pecho 45%)
            _torsoDriver?.ApplyTorsoPose(targetTorso, _analysis.inertia, dt, playerTransform);

            // 2. OBJETIVO DE LA MANO DERECHA EN MUNDO
            Vector3 worldGrip = _chestBone.TransformPoint(targetGripChest);
            Quaternion worldRot = _chestBone.rotation * targetRotChest;

            Vector3 targetR = worldGrip;
            if (_rightUpperArm != null)
            {
                ArmIK.ApplyReachAssistance(
                    _rightShoulder, _spineBone, _hipsBone,
                    ref targetR, _rightUpperArm.position, _rightArmLength, null);
            }

            // 3. POLE Y RESOLUCIÓN TWO-BONE IK DE BRAZO DOMINANTE
            Vector3 rPole = ArmIK.CalculateElbowPoleWithTorsoCapsule(_chestBone, _rightUpperArm.position, targetR, true);
            ArmIK.SolveTwoBone(_rightUpperArm, _rightForearm, _rightHand, targetR, rPole, _currentIkWeight);

            // 4. ORIENTACIÓN DE LA MUÑECA PARA ALINEAR EL ARMA
            Quaternion rOffsetRot = Quaternion.Euler(rightHandOffsetRot);
            Quaternion targetHandRotR = worldRot * Quaternion.Inverse(rOffsetRot);
            ArmIK.ApplyWristAndPronationLimits(_rightForearm, _rightHand, targetHandRotR, _currentIkWeight);

            // 5. BRAZO SECUNDARIO (IZQUIERDO)
            bool isPolearm = _currentStance == WeaponCombatStance.Polearm;
            bool isBimanual = _currentStance == WeaponCombatStance.TwoHanded || isPolearm;

            if (_leftUpperArm != null && _leftForearm != null && _leftHand != null)
            {
                if (!isBimanual)
                {
                    // 1H: Mano izquierda en guardia atlética defensiva junto a las costillas (sin cruzar el pecho)
                    Vector3 leftGuardChest = new Vector3(-0.20f, -0.06f, 0.26f);
                    Vector3 worldLeftHand = _chestBone.TransformPoint(leftGuardChest);
                    Vector3 lPole = _chestBone.TransformPoint(new Vector3(-0.35f, -0.50f, -0.15f));
                    ArmIK.SolveTwoBone(_leftUpperArm, _leftForearm, _leftHand, worldLeftHand, lPole, _currentIkWeight);
                }
                else
                {
                    // 2H: Agarre secundario con relajación de peso en caso de sobre-extensión
                    float leftArmWeight = _currentIkWeight;
                    Vector3 targetL = _gripSocketFront != null ? _gripSocketFront.position : targetR;

                    ArmIK.EnsureBimanualReachable(
                        _rightUpperArm.position, _leftUpperArm.position,
                        ref targetR, ref targetL,
                        _rightArmLength, _leftArmLength,
                        ref leftArmWeight);

                    Vector3 lPole = ArmIK.CalculateElbowPoleWithTorsoCapsule(_chestBone, _leftUpperArm.position, targetL, false);
                    ArmIK.SolveTwoBone(_leftUpperArm, _leftForearm, _leftHand, targetL, lPole, leftArmWeight);

                    if (_gripSocketFront != null)
                    {
                        Quaternion lOffsetRot = Quaternion.Euler(leftHandOffsetRot);
                        Quaternion targetHandRotL = _gripSocketFront.rotation * Quaternion.Inverse(lOffsetRot);
                        ArmIK.ApplyWristAndPronationLimits(_leftForearm, _leftHand, targetHandRotL, leftArmWeight);
                    }
                }
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
