using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// PlayerCombatIKDriver - Submódulo de Cinemática Inversa de Brazos y LookAt.
    /// Resuelve Two-Bone IK analítico para evitar desconexiones, quiebres de codos o deformaciones.
    /// </summary>
    public partial class PlayerCombatIKDriver
    {
        private void ApplyTorsoAndHipsKinematics(Transform playerTransform)
        {
            float weight = _currentIkWeight;
            if (weight <= 0.001f) return;

            Vector3 charUp = playerTransform.up;
            Vector3 charRight = playerTransform.right;
            Vector3 charForward = playerTransform.forward;

            if (_hipsBone != null)
            {
                Quaternion hYaw = Quaternion.AngleAxis(_currentHipsYaw * weight, charUp);
                Quaternion hPitch = Quaternion.AngleAxis(_currentHipsPitch * weight, charRight);
                Quaternion hRoll = Quaternion.AngleAxis(_currentHipsRoll * weight, charForward);
                _hipsBone.rotation = hYaw * hPitch * hRoll * _hipsBone.rotation;
            }

            if (_spineBone != null)
            {
                Quaternion sYaw = Quaternion.AngleAxis(_currentSpineYaw * weight, charUp);
                Quaternion sPitch = Quaternion.AngleAxis(_currentSpinePitch * weight, charRight);
                Quaternion sRoll = Quaternion.AngleAxis(_currentSpineRoll * weight, charForward);
                _spineBone.rotation = sYaw * sPitch * sRoll * _spineBone.rotation;
            }

            if (_chestBone != null && _chestBone != _spineBone)
            {
                Quaternion cYaw = Quaternion.AngleAxis(_currentChestYaw * weight, charUp);
                Quaternion cPitch = Quaternion.AngleAxis(_currentChestPitch * weight, charRight);
                Quaternion cRoll = Quaternion.AngleAxis(_currentChestRoll * weight, charForward);
                _chestBone.rotation = cYaw * cPitch * cRoll * _chestBone.rotation;
            }
        }

        private void SolveArmKinematics(Transform playerTransform)
        {
            if (!_isEquipped || _gripSocketRear == null) return;
            if (_rightUpperArm == null || _rightForearm == null || _rightHand == null) return;

            // 1. Orientación y posición del agarre trasero (mano derecha dominante)
            Quaternion rOffsetRot = Quaternion.Euler(rightHandOffsetRot);
            Quaternion targetHandRotR = _gripSocketRear.rotation * Quaternion.Inverse(rOffsetRot);
            Vector3 targetHandPosR = _gripSocketRear.position - (targetHandRotR * rightHandOffsetPos);

            // 2. Control de alcance seguro respecto al hombro
            Vector3 rShoulderPos = _rightUpperArm.position;
            Vector3 toTargetR = targetHandPosR - rShoulderPos;
            float distR = toTargetR.magnitude;
            float maxReachR = _rightArmLength * maxReachRatio;

            if (distR > maxReachR && _weaponHolder != null)
            {
                Vector3 excess = toTargetR.normalized * (distR - maxReachR);
                _weaponHolder.position -= excess;
                targetHandPosR = _gripSocketRear.position - (targetHandRotR * rightHandOffsetPos);
                toTargetR = targetHandPosR - rShoulderPos;
                distR = toTargetR.magnitude;
            }

            // 3. Dirección dinámica del codo derecho (Pole Target)
            Vector3 charRight = playerTransform.right;
            Vector3 charDown = -playerTransform.up;
            Vector3 charFwd = playerTransform.forward;
            float crossBody = Mathf.Clamp01(-Vector3.Dot(toTargetR.normalized, charRight));
            Vector3 rPoleDir = Vector3.Lerp(charRight * 0.40f + charDown * 0.25f - charFwd * 0.10f,
                                            charDown * 0.45f + charFwd * 0.15f + charRight * 0.15f,
                                            crossBody);
            Vector3 rPolePos = rShoulderPos + rPoleDir;

            // 4. Resolver Two-Bone IK para el brazo derecho
            SolveTwoBoneIK(_rightUpperArm, _rightForearm, _rightHand, targetHandPosR, rPolePos, _currentIkWeight);

            // 5. Orientación de la muñeca derecha fijada al mango
            if (Quaternion.Dot(_rightHand.rotation, targetHandRotR) < 0f)
            {
                targetHandRotR = new Quaternion(-targetHandRotR.x, -targetHandRotR.y, -targetHandRotR.z, -targetHandRotR.w);
            }
            _rightHand.rotation = Quaternion.Slerp(_rightHand.rotation, targetHandRotR, _currentIkWeight);

            // 6. Brazo izquierdo para armas a dos manos o asta
            bool isBimanual = _currentStance == WeaponCombatStance.TwoHanded || _currentStance == WeaponCombatStance.Polearm;
            if (isBimanual && _gripSocketFront != null && _leftUpperArm != null && _leftForearm != null && _leftHand != null)
            {
                Quaternion lOffsetRot = Quaternion.Euler(leftHandOffsetRot);
                Quaternion targetHandRotL = _gripSocketFront.rotation * Quaternion.Inverse(lOffsetRot);
                Vector3 targetHandPosL = _gripSocketFront.position - (targetHandRotL * leftHandOffsetPos);

                Vector3 lShoulderPos = _leftUpperArm.position;
                Vector3 toTargetL = targetHandPosL - lShoulderPos;
                float distL = toTargetL.magnitude;
                float maxReachL = _leftArmLength * maxReachRatio;
                if (distL > maxReachL)
                {
                    targetHandPosL = lShoulderPos + toTargetL.normalized * maxReachL;
                }

                Vector3 lPoleDir = -charRight * 0.40f + charDown * 0.25f - charFwd * 0.10f;
                Vector3 lPolePos = lShoulderPos + lPoleDir;

                SolveTwoBoneIK(_leftUpperArm, _leftForearm, _leftHand, targetHandPosL, lPolePos, _currentIkWeight);

                if (Quaternion.Dot(_leftHand.rotation, targetHandRotL) < 0f)
                {
                    targetHandRotL = new Quaternion(-targetHandRotL.x, -targetHandRotL.y, -targetHandRotL.z, -targetHandRotL.w);
                }
                _leftHand.rotation = Quaternion.Slerp(_leftHand.rotation, targetHandRotL, _currentIkWeight);
            }
        }

        private static void SolveTwoBoneIK(Transform root, Transform mid, Transform end, Vector3 targetPos, Vector3 polePos, float weight)
        {
            if (weight <= 0.001f || root == null || mid == null || end == null) return;

            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = end.position;

            float lenAB = Vector3.Distance(a, b);
            float lenBC = Vector3.Distance(b, c);
            if (lenAB < 0.01f || lenBC < 0.01f) return;

            float maxDist = (lenAB + lenBC) * 0.999f;
            float minDist = Mathf.Abs(lenAB - lenBC) * 1.001f;

            Vector3 toTarget = targetPos - a;
            float dist = toTarget.magnitude;
            if (dist < 0.001f) return;

            float clampedDist = Mathf.Clamp(dist, minDist, maxDist);
            Vector3 dir = toTarget / dist;

            float cosAlpha = Mathf.Clamp((lenAB * lenAB + clampedDist * clampedDist - lenBC * lenBC) / (2f * lenAB * clampedDist), -1f, 1f);
            float sinAlpha = Mathf.Sqrt(Mathf.Max(0f, 1f - cosAlpha * cosAlpha));

            Vector3 poleDir = (polePos - a).normalized;
            Vector3 planeNormal = Vector3.Cross(dir, poleDir);
            if (planeNormal.sqrMagnitude < 0.0001f) planeNormal = Vector3.up;
            planeNormal.Normalize();

            Vector3 tangent = Vector3.Cross(planeNormal, dir).normalized;
            Vector3 bDesired = a + dir * (lenAB * cosAlpha) + tangent * (lenAB * sinAlpha);

            Quaternion origRootRot = root.rotation;
            Quaternion origMidRot = mid.rotation;

            Vector3 currentAB = b - a;
            Vector3 desiredAB = bDesired - a;
            if (currentAB.sqrMagnitude > 0.0001f && desiredAB.sqrMagnitude > 0.0001f)
            {
                Quaternion rotRoot = Quaternion.FromToRotation(currentAB, desiredAB);
                root.rotation = rotRoot * root.rotation;
            }

            b = mid.position;
            c = end.position;
            Vector3 currentBC = c - b;
            Vector3 desiredBC = (a + dir * clampedDist) - b;
            if (currentBC.sqrMagnitude > 0.0001f && desiredBC.sqrMagnitude > 0.0001f)
            {
                Quaternion rotMid = Quaternion.FromToRotation(currentBC, desiredBC);
                mid.rotation = rotMid * mid.rotation;
            }

            if (weight < 0.999f)
            {
                root.rotation = Quaternion.Slerp(origRootRot, root.rotation, weight);
                mid.rotation = Quaternion.Slerp(origMidRot, mid.rotation, weight);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null) return;

            Transform playerTransform = transform.parent != null ? transform.parent : transform;

            if (_currentIkWeight > 0.001f)
            {
                Vector3 lookTarget = playerTransform.position + playerTransform.forward * 6.0f + Vector3.up * 1.50f;
                _animator.SetLookAtPosition(lookTarget);
                _animator.SetLookAtWeight(_currentIkWeight * 0.60f, 0.20f, 0.80f, 0.30f, 0.50f);
            }

            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);

            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
        }
    }
}
