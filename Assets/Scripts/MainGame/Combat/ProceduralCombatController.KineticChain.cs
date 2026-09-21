using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class ProceduralCombatController
    {
        private void UpdateKineticChain(float lag)
        {
            // Evaluación de retardos secuenciales de la cadena cinética (Efecto Látigo):
            // 1. Cadera (reacciona de inmediato)
            float tHips = Mathf.Clamp01(_phaseTime / _phaseDuration);
            float uHips = EvaluateEase(tHips);
            _hipsYaw = Mathf.Lerp(_fromHipYaw, _toHipYaw, uHips);

            // 2. Columna (reacciona con retraso lag)
            float tSpine = Mathf.Clamp01((_phaseTime - lag) / _phaseDuration);
            float uSpine = EvaluateEase(tSpine);
            _spineYaw = Mathf.Lerp(_fromSpineYaw, _toSpineYaw, uSpine);
            _spinePitch = Mathf.Lerp(_fromSpinePitch, _toSpinePitch, uSpine);
            _spineRoll = Mathf.Lerp(_fromSpineRoll, _toSpineRoll, uSpine);

            // 3. Pecho (reacciona con retraso 2*lag)
            float tChest = Mathf.Clamp01((_phaseTime - (2f * lag)) / _phaseDuration);
            float uChest = EvaluateEase(tChest);
            _chestYaw = Mathf.Lerp(_fromChestYaw, _toChestYaw, uChest);
            _chestPitch = Mathf.Lerp(_fromChestPitch, _toChestPitch, uChest);
            _chestRoll = Mathf.Lerp(_fromChestRoll, _toChestRoll, uChest);
            _weightShift = Vector3.Lerp(_fromWeightShift, _toWeightShift, uChest);

            // 4. Aplicar impulso de Lunge al Rigidbody (físico sin atravesar paredes)
            ApplyLunge(tHips);
        }

        private void ApplyLunge(float tHips)
        {
            if (playerRigidbody != null && !playerRigidbody.isKinematic && _activePhaseData.lunge > 0.01f)
            {
                float stepVelocity = (_activePhaseData.lunge / _phaseDuration) * (1.0f - tHips);
                Vector3 lungeDir = transform.forward;
                Vector3 currentVel = playerRigidbody.linearVelocity;
                playerRigidbody.linearVelocity = new Vector3(lungeDir.x * stepVelocity, currentVel.y, lungeDir.z * stepVelocity);
            }
        }

        private void ApplyKineticChainTorso()
        {
            // 1. Cadera (Hips) Yaw
            if (_hipsBone != null && Mathf.Abs(_hipsYaw) > 0.01f)
            {
                float maxAngle = config != null ? config.hipYawMaxAngle : 35f;
                float clampedHipYaw = Mathf.Clamp(_hipsYaw, -maxAngle, maxAngle);
                _hipsBone.rotation = Quaternion.AngleAxis(clampedHipYaw, transform.up) * _hipsBone.rotation;
            }

            // Desplazamiento de centro de masa (Weight Shift) sobre las caderas
            if (_hipsBone != null && _weightShift.sqrMagnitude > 0.0001f)
            {
                _hipsBone.position += transform.TransformDirection(_weightShift);
            }

            // 2. Columna (Spine) Yaw, Pitch, Roll
            if (_spineBone != null)
            {
                Quaternion sYaw = Quaternion.AngleAxis(_spineYaw, transform.up);
                Quaternion sPitch = Quaternion.AngleAxis(_spinePitch, transform.right);
                Quaternion sRoll = Quaternion.AngleAxis(_spineRoll, transform.forward);
                _spineBone.rotation = sYaw * sPitch * sRoll * _spineBone.rotation;
            }

            // 3. Pecho (Chest) Yaw, Pitch, Roll
            if (_chestBone != null && _chestBone != _spineBone)
            {
                Quaternion cYaw = Quaternion.AngleAxis(_chestYaw, transform.up);
                Quaternion cPitch = Quaternion.AngleAxis(_chestPitch, transform.right);
                Quaternion cRoll = Quaternion.AngleAxis(_chestRoll, transform.forward);
                _chestBone.rotation = cYaw * cPitch * cRoll * _chestBone.rotation;
            }
        }
    }
}
