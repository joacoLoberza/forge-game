using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class ProceduralCombatController
    {
        public void OnAnimatorIKCallback(int layerIndex)
        {
            // Ejecutar exclusivamente en el primer pase para evitar doble rotación
            if (layerIndex != 0 || characterAnimator == null || _ikWeight <= 0.001f || _gripSocketR == null || _gripSocketL == null) return;

            // 1. Aplicar la torsión de la cadena cinética sobre los huesos del torso
            ApplyKineticChainTorso();

            // 2. LookAt suave de la cabeza manteniendo el objetivo al frente
            Vector3 lookTarget = transform.position + Vector3.up * 1.55f + transform.forward * 12.0f;
            characterAnimator.SetLookAtPosition(lookTarget);
            characterAnimator.SetLookAtWeight(_ikWeight * 0.85f, 0.20f, 0.80f, 0.40f, 0.50f);

            Transform chest = _chestBone != null ? _chestBone : transform;
            float minReachRatio = config != null ? config.armReachMinRatio : 0.25f;
            float maxReachRatio = config != null ? config.armReachMaxRatio : 0.95f;

            // 3. MANO DERECHA (Mano trasera en el pomo)
            Vector3 rSocketPos = _gripSocketR.position;
            Quaternion rSocketRot = _gripSocketR.rotation;

            Vector3 rOffsetPos = config != null ? config.rightHandOffsetPos : new Vector3(0.035f, 0.075f, -0.015f);
            Vector3 rOffsetRotEuler = config != null ? config.rightHandOffsetRot : new Vector3(-90f, 0f, 90f);
            Quaternion rOffsetRot = Quaternion.Euler(rOffsetRotEuler);

            Quaternion targetHandRotR = rSocketRot * Quaternion.Inverse(rOffsetRot);
            Vector3 targetHandPosR = rSocketPos - (targetHandRotR * rOffsetPos);

            if (_rightShoulder != null)
            {
                _currentDistR = Vector3.Distance(_rightShoulder.position, targetHandPosR);
                targetHandPosR = ClampArmReach(_rightShoulder.position, targetHandPosR, _rightArmLength, minReachRatio, maxReachRatio);
            }

            // Clamp anatómico de torsión de muñeca derecha
            targetHandRotR = ClampWristTwist(HumanBodyBones.RightLowerArm, targetHandRotR);

            characterAnimator.SetIKPosition(AvatarIKGoal.RightHand, targetHandPosR);
            characterAnimator.SetIKRotation(AvatarIKGoal.RightHand, targetHandRotR);
            characterAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, _ikWeight);
            characterAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, _ikWeight);

            // Hint de codo derecho
            if (_rightShoulder != null)
            {
                Vector3 rElbowOffset = config != null ? config.rightElbowHintOffset : new Vector3(0.22f, -0.26f, -0.10f);
                Vector3 rHint = (_rightShoulder.position + targetHandPosR) * 0.5f + chest.TransformDirection(rElbowOffset);
                characterAnimator.SetIKHintPosition(AvatarIKHint.RightElbow, rHint);
                characterAnimator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _ikWeight);
            }

            // 4. MANO IZQUIERDA (Mano delantera para TwoHand y Pole)
            bool isBimanual = _currentProfile.grip == GripMode.TwoHand || _currentProfile.grip == GripMode.Pole;
            if (isBimanual)
            {
                // Resolución geométrica para que ambas manos alcancen el mango en posiciones extremas
                if (_leftShoulder != null)
                {
                    float maxReachL = _leftArmLength * maxReachRatio;
                    float distToLeftSocket = Vector3.Distance(_leftShoulder.position, _gripSocketL.position);
                    if (distToLeftSocket > maxReachL)
                    {
                        Vector3 pull = (_leftShoulder.position - _gripSocketL.position).normalized * (distToLeftSocket - maxReachL);
                        _weaponPivot.position += pull * 0.45f;
                    }
                }

                Vector3 lSocketPos = _gripSocketL.position;
                Quaternion lSocketRot = _gripSocketL.rotation;

                Vector3 lOffsetPos = config != null ? config.leftHandOffsetPos : new Vector3(-0.035f, 0.075f, -0.015f);
                Vector3 lOffsetRotEuler = config != null ? config.leftHandOffsetRot : new Vector3(-90f, 0f, -90f);
                Quaternion lOffsetRot = Quaternion.Euler(lOffsetRotEuler);

                Quaternion targetHandRotL = lSocketRot * Quaternion.Inverse(lOffsetRot);
                Vector3 targetHandPosL = lSocketPos - (targetHandRotL * lOffsetPos);

                if (_leftShoulder != null)
                {
                    _currentDistL = Vector3.Distance(_leftShoulder.position, targetHandPosL);
                    targetHandPosL = ClampArmReach(_leftShoulder.position, targetHandPosL, _leftArmLength, minReachRatio, maxReachRatio);
                }

                // Clamp anatómico de torsión de muñeca izquierda
                targetHandRotL = ClampWristTwist(HumanBodyBones.LeftLowerArm, targetHandRotL);

                characterAnimator.SetIKPosition(AvatarIKGoal.LeftHand, targetHandPosL);
                characterAnimator.SetIKRotation(AvatarIKGoal.LeftHand, targetHandRotL);
                characterAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _ikWeight);
                characterAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _ikWeight);

                if (_leftShoulder != null)
                {
                    Vector3 lElbowOffset = config != null ? config.leftElbowHintOffset : new Vector3(-0.22f, -0.26f, -0.10f);
                    Vector3 lHint = (_leftShoulder.position + targetHandPosL) * 0.5f + chest.TransformDirection(lElbowOffset);
                    characterAnimator.SetIKHintPosition(AvatarIKHint.LeftElbow, lHint);
                    characterAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _ikWeight);
                }
            }
            else
            {
                // 1 Mano: liberar completamente la mano izquierda para que acompañe la locomoción libre
                _currentDistL = 0f;
                characterAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0.0f);
                characterAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0.0f);
                characterAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0.0f);
            }
        }

        private Quaternion ClampWristTwist(HumanBodyBones forearmBone, Quaternion targetHandRot)
        {
            if (config == null || characterAnimator == null) return targetHandRot;

            Transform forearm = characterAnimator.GetBoneTransform(forearmBone);
            if (forearm == null) return targetHandRot;

            Quaternion relRot = Quaternion.Inverse(forearm.rotation) * targetHandRot;
            relRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            if (Mathf.Abs(angle) > config.wristTwistMaxAngle)
            {
                float clampedAngle = Mathf.Sign(angle) * config.wristTwistMaxAngle;
                return forearm.rotation * Quaternion.AngleAxis(clampedAngle, axis);
            }

            return targetHandRot;
        }

        private static Vector3 ClampArmReach(Vector3 shoulderPos, Vector3 targetPos, float armLength, float minRatio, float maxRatio)
        {
            Vector3 toTarget = targetPos - shoulderPos;
            float dist = toTarget.magnitude;
            if (dist < 0.001f) return shoulderPos + Vector3.forward * (armLength * minRatio);

            float minReach = armLength * minRatio;
            float maxReach = armLength * maxRatio;

            if (dist > maxReach)
            {
                return shoulderPos + toTarget.normalized * maxReach;
            }
            else if (dist < minReach)
            {
                return shoulderPos + toTarget.normalized * minReach;
            }

            return targetPos;
        }
    }
}
