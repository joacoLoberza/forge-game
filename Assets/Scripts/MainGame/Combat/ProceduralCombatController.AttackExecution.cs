using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class ProceduralCombatController
    {
        /// <summary>
        /// Inicia un ataque procedural correspondiente al índice de combo solicitado (0, 1 o 2).
        /// Escala la duración de las fases mediante la inercia del arma: k = sqrt(I / Iref).
        /// </summary>
        public void ExecuteAttack(int comboIndex)
        {
            if (!_isEquipped) return;

            if (_currentComboSequence == null || _currentComboSequence.Length < 3)
            {
                _currentComboSequence = ProceduralAttackDatabase.GetComboSequence(_currentProfile);
            }

            _currentComboIndex = Mathf.Clamp(comboIndex, 0, _currentComboSequence.Length - 1);
            _currentAttackDef = _currentComboSequence[_currentComboIndex];
            if (_currentAttackDef == null) return;

            // Escala de inercia física k = sqrt(I / Iref)
            float inertia = _currentProfile.inertia > 0.01f ? _currentProfile.inertia : 0.85f;
            float refInertia = config != null ? config.referenceInertia : 0.85f;
            _inertiaScale = Mathf.Clamp(Mathf.Sqrt(inertia / refInertia), 0.60f, 1.80f);

            // Iniciar fase de Windup (Anticipación)
            float windupDur = CalculatePhaseDuration(_currentAttackDef.windupPhase.duration, AttackSubPhase.Windup);
            SetPhase(AttackSubPhase.Windup, _currentAttackDef.windupPhase, windupDur);
        }

        /// <summary>
        /// Devuelve las duraciones físicas escaladas por inercia para sincronización con AttackController.
        /// </summary>
        public (float totalDuration, float windupDuration, float strikeDuration, float recoveryDuration) GetCurrentAttackTimings()
        {
            if (_currentAttackDef == null) return (0.80f, 0.22f, 0.18f, 0.40f);

            float w = CalculatePhaseDuration(_currentAttackDef.windupPhase.duration, AttackSubPhase.Windup);
            float s = CalculatePhaseDuration(_currentAttackDef.strikePhase.duration, AttackSubPhase.Strike);
            float r = CalculatePhaseDuration(_currentAttackDef.recoveryPhase.duration, AttackSubPhase.Recovery);
            return (w + s + r, w, s, r);
        }

        private float CalculatePhaseDuration(float baseDuration, AttackSubPhase subPhase)
        {
            float dur = baseDuration * _inertiaScale;
            if (config != null)
            {
                switch (subPhase)
                {
                    case AttackSubPhase.Windup:
                        dur = Mathf.Clamp(dur, config.windupClampMin, config.windupClampMax);
                        break;
                    case AttackSubPhase.Strike:
                        dur = Mathf.Clamp(dur, config.strikeClampMin, config.strikeClampMax);
                        break;
                    case AttackSubPhase.Recovery:
                        dur = Mathf.Clamp(dur, config.recoveryClampMin, config.recoveryClampMax);
                        break;
                }
            }
            return dur;
        }

        private void SetPhase(AttackSubPhase newPhase, AttackPhase phaseData, float duration)
        {
            _currentSubPhase = newPhase;
            _phaseTime = 0f;
            _phaseDuration = Mathf.Max(0.04f, duration);
            _activePhaseData = phaseData;

            if (_weaponPivot != null)
            {
                _fromHandPose = new Pose(_weaponPivot.localPosition, _weaponPivot.localRotation);
            }
            _toHandPose = phaseData.handPose;

            // Guardar estado actual de la cadena cinética para interpolar suavemente
            _fromHipYaw = _hipsYaw;
            _toHipYaw = phaseData.hipYaw;

            float spineRatio = config != null ? config.spineContributionRatio : 0.35f;
            float chestRatio = config != null ? config.chestContributionRatio : 0.40f;
            float totalRatio = Mathf.Max(0.01f, spineRatio + chestRatio);

            _fromSpineYaw = _spineYaw;
            _toSpineYaw = phaseData.chestYaw * (spineRatio / totalRatio);
            _fromSpinePitch = _spinePitch;
            _toSpinePitch = phaseData.chestPitch * 0.45f;
            _fromSpineRoll = _spineRoll;
            _toSpineRoll = phaseData.chestRoll * 0.45f;

            _fromChestYaw = _chestYaw;
            _toChestYaw = phaseData.chestYaw * (chestRatio / totalRatio);
            _fromChestPitch = _chestPitch;
            _toChestPitch = phaseData.chestPitch * 0.55f;
            _fromChestRoll = _chestRoll;
            _toChestRoll = phaseData.chestRoll * 0.55f;

            _fromWeightShift = _weightShift;
            _toWeightShift = phaseData.weightShift;
        }

        private void UpdateAttackStateMachine()
        {
            if (_currentSubPhase == AttackSubPhase.InGuard)
            {
                // En guardia: mantener pose de reposo y relajar la columna
                Vector3 targetGuardPos = _currentGuardLocalPos + _dynamicGuardOffset;
                _weaponPivot.localPosition = Vector3.Lerp(_weaponPivot.localPosition, targetGuardPos, Time.deltaTime * 10f);
                _weaponPivot.localRotation = Quaternion.Slerp(_weaponPivot.localRotation, _currentGuardLocalRot, Time.deltaTime * 10f);

                _hipsYaw = Mathf.MoveTowards(_hipsYaw, 0f, Time.deltaTime * 180f);
                _spineYaw = Mathf.MoveTowards(_spineYaw, 0f, Time.deltaTime * 180f);
                _spinePitch = Mathf.MoveTowards(_spinePitch, 0f, Time.deltaTime * 180f);
                _spineRoll = Mathf.MoveTowards(_spineRoll, 0f, Time.deltaTime * 180f);
                _chestYaw = Mathf.MoveTowards(_chestYaw, 0f, Time.deltaTime * 180f);
                _chestPitch = Mathf.MoveTowards(_chestPitch, 0f, Time.deltaTime * 180f);
                _chestRoll = Mathf.MoveTowards(_chestRoll, 0f, Time.deltaTime * 180f);
                _weightShift = Vector3.MoveTowards(_weightShift, Vector3.zero, Time.deltaTime * 2.0f);
                return;
            }

            // En ataque activo: avanzar temporizador
            _phaseTime += Time.deltaTime;
            float lag = config != null ? config.kineticChainLagSeconds : 0.055f;

            UpdateKineticChain(lag);

            // Brazos / Arma (sigue la trayectoria en arco Bézier)
            float tWeapon = Mathf.Clamp01((_phaseTime - (2.5f * lag)) / Mathf.Max(0.02f, _phaseDuration - (2.5f * lag)));
            float uWeapon = EvaluateEase(tWeapon);
            Pose targetPose = InterpolateHandPose(_fromHandPose, _toHandPose, _activePhaseData, uWeapon);
            _weaponPivot.localPosition = targetPose.position;
            _weaponPivot.localRotation = targetPose.rotation;

            // Transición automática al finalizar la fase
            if (_phaseTime >= _phaseDuration)
            {
                AdvanceAttackPhase();
            }
        }

        private void AdvanceAttackPhase()
        {
            if (_currentAttackDef == null)
            {
                ResetToGuard();
                return;
            }

            if (_currentSubPhase == AttackSubPhase.Windup)
            {
                // Windup finalizado -> Pasar al golpe (Strike)
                float strikeDur = CalculatePhaseDuration(_currentAttackDef.strikePhase.duration, AttackSubPhase.Strike);
                SetPhase(AttackSubPhase.Strike, _currentAttackDef.strikePhase, strikeDur);

                if (attackController != null)
                {
                    attackController.TriggerHitDetection();
                }
            }
            else if (_currentSubPhase == AttackSubPhase.Strike)
            {
                // Strike finalizado -> Pasar a la recuperación (Recovery)
                float recDur = CalculatePhaseDuration(_currentAttackDef.recoveryPhase.duration, AttackSubPhase.Recovery);
                SetPhase(AttackSubPhase.Recovery, _currentAttackDef.recoveryPhase, recDur);
            }
            else if (_currentSubPhase == AttackSubPhase.Recovery)
            {
                // Recuperación completada -> Volver a guardia
                ResetToGuard();
            }
        }

        private float EvaluateEase(float t)
        {
            if (_activePhaseData.ease != null && _activePhaseData.ease.length > 0)
            {
                return Mathf.Clamp01(_activePhaseData.ease.Evaluate(t));
            }
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private static Pose InterpolateHandPose(Pose from, Pose to, AttackPhase phase, float u)
        {
            if (phase.hasIntermediatePose)
            {
                // Curva cuadrática Bézier para describir un arco tridimensional
                Vector3 p0 = from.position;
                Vector3 p1 = phase.intermediateHandPose.position;
                Vector3 p2 = to.position;
                float oneMinusU = 1f - u;
                Vector3 pos = (oneMinusU * oneMinusU * p0) + (2f * oneMinusU * u * p1) + (u * u * p2);

                Quaternion rot;
                if (u < 0.5f)
                {
                    rot = Quaternion.Slerp(from.rotation, phase.intermediateHandPose.rotation, u * 2f);
                }
                else
                {
                    rot = Quaternion.Slerp(phase.intermediateHandPose.rotation, to.rotation, (u - 0.5f) * 2f);
                }
                return new Pose(pos, rot);
            }
            else
            {
                Vector3 pos = Vector3.Lerp(from.position, to.position, u);
                Quaternion rot = Quaternion.Slerp(from.rotation, to.rotation, u);
                return new Pose(pos, rot);
            }
        }
    }
}
