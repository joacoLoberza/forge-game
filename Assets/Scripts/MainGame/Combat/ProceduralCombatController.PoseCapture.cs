using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class ProceduralCombatController
    {
        // =========================================================================
        // HERRAMIENTA DE CAPTURA DE POSES (Editor / Play Mode)
        // =========================================================================

        /// <summary>
        /// Captura la pose actual del WeaponPivot y los ángulos del torso en una fase del AttackDefinitionSO.
        /// </summary>
        public void CaptureCurrentPoseTo(AttackDefinitionSO attack, AttackSubPhase subPhase)
        {
            if (attack == null || _weaponPivot == null) return;

            AttackPhase phase = new AttackPhase
            {
                handPose = new Pose(_weaponPivot.localPosition, _weaponPivot.localRotation),
                hipYaw = _hipsYaw,
                chestYaw = _chestYaw + _spineYaw,
                chestPitch = _chestPitch + _spinePitch,
                chestRoll = _chestRoll + _spineRoll,
                weightShift = _weightShift,
                lunge = _activePhaseData.lunge,
                duration = _activePhaseData.duration > 0.05f ? _activePhaseData.duration : 0.20f,
                ease = _activePhaseData.ease ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            switch (subPhase)
            {
                case AttackSubPhase.InGuard:
                    attack.guardPhase = phase;
                    break;
                case AttackSubPhase.Windup:
                    attack.windupPhase = phase;
                    break;
                case AttackSubPhase.Strike:
                    attack.strikePhase = phase;
                    break;
                case AttackSubPhase.Recovery:
                    attack.recoveryPhase = phase;
                    break;
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(attack);
#endif
            Debug.Log($"[ProceduralCombat] Pose capturada con éxito en {attack.name} -> {subPhase}!");
        }

        /// <summary>
        /// Aplica directamente una pose de prueba al pivot y a la cadena cinética para previsualización.
        /// </summary>
        public void PreviewPose(AttackPhase phase)
        {
            if (_weaponPivot != null)
            {
                _weaponPivot.localPosition = phase.handPose.position;
                _weaponPivot.localRotation = phase.handPose.rotation;
            }

            _hipsYaw = phase.hipYaw;
            _spineYaw = phase.chestYaw * 0.4f;
            _chestYaw = phase.chestYaw * 0.6f;
            _chestPitch = phase.chestPitch;
            _chestRoll = phase.chestRoll;
            _weightShift = phase.weightShift;
        }
    }
}
