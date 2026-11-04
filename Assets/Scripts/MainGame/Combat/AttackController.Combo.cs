using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public partial class AttackController
    {
        private void EnsureComboMoves()
        {
            if (_currentComboMoves == null || _currentComboMoves.Length != 5)
            {
                _currentAnalysis = WeaponAnalyzer.Analyze(_equippedWeapon);
                _currentComboMoves = ComboCatalog.GetCombo(_currentAnalysis.archetype);
                _maxComboSteps = 5;
            }
        }

        private void EndAttack()
        {
            _isAttacking = false;
            _hasBufferedNextStrike = false;
            _currentComboIndex = 0;
            _activeMove = null;
            _hitTargetsThisSwing.Clear();
            _comboAvailableTime = Time.time + postComboCooldown;

            var equip = GetEquipment();
            if (equip?.EdgeAligner != null) equip.EdgeAligner.isAttacking = false;

            var paladinAnim = equip?.PaladinAnimator;
            if (paladinAnim != null)
            {
                paladinAnim.SetBool("IsAttacking", false);
                paladinAnim.ResetTrigger("Attack");

                int combatLayer = (paladinAnim.layerCount > 1) ? 1 : 0;
                if (combatLayer == 1)
                    paladinAnim.CrossFadeInFixedTime("CombatStance", 0.20f, 1);
                else
                    paladinAnim.CrossFadeInFixedTime("Locomotion", 0.18f, 0);
            }

            if (_attackHitboxVisualizer != null)
            {
                _attackHitboxVisualizer.IsVolumeActive = false;
                _attackHitboxVisualizer.IsConnecting = false;
            }
        }
    }
}
