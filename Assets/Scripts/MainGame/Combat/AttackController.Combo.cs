using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Forge;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    public partial class AttackController
    {
        private bool _isBlocking;
        private float _blockStartedAt = -99f;
        private float _blockLockedUntil = -99f;
        private float _nextBashTime = 0f;
        private ParticleSystem _combatSparkVfx;
        private ParticleSystem _combatBloodVfx;

        public bool IsBlocking => _isBlocking;
        public bool IsTimedParryWindow
        {
            get
            {
                if (!_isBlocking) return false;
                float window = (ForjaGame.UI.ForgeTutorialDirector.Instance != null && ForjaGame.UI.ForgeTutorialDirector.Instance.IsDummyParryLessonActive) ? 0.44f : 0.28f;
                return (Time.time - _blockStartedAt) <= window;
            }
        }

        private void UpdateBlockingAndBash(bool isLocal)
        {
            var roll = GetComponent<PlayerRollController>();
            if (!isLocal || Mouse.current == null || UI.Core.UIPanelBase.IsAnyPanelOpen || ForjaGame.UI.PauseMenuPanel.IsGamePaused || (roll != null && roll.IsRolling))
            {
                _isBlocking = false;
                return;
            }
            if (AnvilController.Instance != null && (AnvilController.Instance.IsForging || AnvilController.Instance.HasIngotOnAnvil || AnvilController.Instance.IsCarvingHandle))
            {
                _isBlocking = false;
                return;
            }
            if (gameStateController != null && !gameStateController.IsInState(GameState.Exploration))
            {
                _isBlocking = false;
                return;
            }

            var equip = GetEquipment();
            if (equip == null || !equip.HasWeaponEquipped)
            {
                _isBlocking = false;
                if (equip?.EdgeAligner != null) equip.EdgeAligner.isBlocking = false;
                return;
            }

            bool rightHeld = Mouse.current.rightButton.isPressed && !_isAttacking && !HasPickaxeEquipped && Time.time >= _blockLockedUntil;
            if (rightHeld && !_isBlocking)
            {
                _isBlocking = true;
                _blockStartedAt = Time.time;
                if (equip.PaladinAnimator != null)
                {
                    int layer = equip.PaladinAnimator.layerCount > 1 ? 1 : 0;
                    if (layer == 1) equip.PaladinAnimator.SetLayerWeight(1, 1f);
                    equip.PaladinAnimator.SetBool("IsBlocking", true);
                    equip.PaladinAnimator.CrossFadeInFixedTime("Block", 0.10f, layer);
                }
                ForjaGame.Audio.ForgeAudioSystem.PlayWeaponSwing(transform.position, false);
            }
            else if (!rightHeld && _isBlocking)
            {
                _isBlocking = false;
                if (equip.PaladinAnimator != null)
                {
                    equip.PaladinAnimator.SetBool("IsBlocking", false);
                    int layer = equip.PaladinAnimator.layerCount > 1 ? 1 : 0;
                    equip.PaladinAnimator.CrossFadeInFixedTime(layer == 1 ? "CombatStance" : "Locomotion", 0.12f, layer);
                }
            }

            if (equip.EdgeAligner != null)
            {
                equip.EdgeAligner.weaponArchetype = _equippedWeapon != null ? _equippedWeapon.weaponType : WeaponType.Sword;
                equip.EdgeAligner.isBlocking = _isBlocking;
            }

            if (_isBlocking)
            {
                equip.EdgeAligner?.NotifyCombatAlert(1.5f);
                if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= _nextBashTime)
                {
                    _nextBashTime = Time.time + 0.85f;
                    ExecuteSkyrimWeaponBash();
                }
            }
        }

        private void ExecuteSkyrimWeaponBash()
        {
            Vector3 attackDir = GetPlayerAttackInputDirection();
            if (attackDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(attackDir);
            if (_rb != null && !_rb.isKinematic)
                _rb.linearVelocity = new Vector3(transform.forward.x * 3.2f, Mathf.Min(0f, _rb.linearVelocity.y), transform.forward.z * 3.2f);

            var equip = GetEquipment();
            equip?.EdgeAligner?.TriggerBashAnimation();
            if (equip?.PaladinAnimator != null)
            {
                int layer = equip.PaladinAnimator.layerCount > 1 ? 1 : 0;
                equip.PaladinAnimator.SetFloat("AttackSpeed", 1.45f);
                equip.PaladinAnimator.CrossFadeInFixedTime("Attack1", 0.08f, layer);
            }
            ForjaGame.Audio.ForgeAudioSystem.PlayWeaponSwing(transform.position, true);
            Vector3 bashCenter = transform.position + transform.forward * 1.25f + Vector3.up * 0.9f;
            AlloyPassiveCombatResolver.OnWeaponBash(this, _equippedWeapon, bashCenter);
            Collider[] hits = Physics.OverlapSphere(bashCenter, 1.45f, targetLayers, QueryTriggerInteraction.Collide);
            bool hitAny = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null || c.gameObject == gameObject || c.transform.IsChildOf(transform)) continue;
                if (!allowFriendlyFire && c.GetComponentInParent<PlayerMovement>() != null) continue;
                IDamageable d = c.GetComponentInParent<IDamageable>() ?? c.GetComponentInChildren<IDamageable>();
                if (d == null || !d.IsAlive || d is ForjaGame.Resources.ResourceNodeController || d is IDestructibleTunnelWall) continue;

                hitAny = true;
                int bashDmg = Mathf.Max(2, Mathf.RoundToInt(damage * 0.45f));
                d.TakeDamage(bashDmg);
                if (d is CaveCreatureBase creature) creature.ApplyKnockback(c.transform.position - transform.position, 11.5f);
                SpawnVisceralCombatImpact(c.ClosestPoint(bashCenter), d is GolemEnemyController or TitanBossController, true);
                ForjaGame.Audio.ForgeAudioSystem.PlayHitImpact(c.transform.position, true, true);
            }

            if (hitAny)
            {
                TriggerImpulseOnPlayerCamera(0.08f);
                TriggerHitStop(0.075f);
            }
        }

        public void SpawnVisceralCombatImpact(Vector3 worldPos, bool isStoneOrArmor, bool isCritical)
        {
            EnsureCombatVfx();
            if (_combatSparkVfx != null)
            {
                _combatSparkVfx.transform.position = worldPos;
                _combatSparkVfx.Emit(isCritical ? 34 : (isStoneOrArmor ? 24 : 12));
            }
            if (!isStoneOrArmor && _combatBloodVfx != null)
            {
                _combatBloodVfx.transform.position = worldPos;
                _combatBloodVfx.Emit(isCritical ? 26 : 16);
            }
        }

        private void EnsureCombatVfx()
        {
            if (_combatSparkVfx == null)
                _combatSparkVfx = CreateImpactParticle("CombatHitSparks", new Color(1f, 0.88f, 0.32f, 1f), new Color(1f, 0.38f, 0.05f, 1f), true);
            if (_combatBloodVfx == null)
                _combatBloodVfx = CreateImpactParticle("CombatHitBlood", new Color(0.75f, 0.08f, 0.06f, 0.95f), new Color(0.32f, 0.03f, 0.02f, 0.90f), false);
        }

        private ParticleSystem CreateImpactParticle(string name, Color c1, Color c2, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.052f);
            main.gravityModifier = 1.8f;
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.08f;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = additive ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            psr.lengthScale = additive ? 2.2f : 1.0f;
            psr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_" + name, additive);
            return ps;
        }

        private void EnsureComboMoves()
        {
            if (_currentComboMoves == null || _currentComboMoves.Length != 5)
            {
                _currentAnalysis = WeaponAnalyzer.Analyze(_equippedWeapon);
                _currentComboMoves = ComboCatalog.GetCombo(_currentAnalysis.archetype);
                _maxComboSteps = _currentAnalysis.archetype == WeaponArchetype.Pickaxe ? 1 : 5;
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
            bool wasUpper = equip?.EdgeAligner != null && equip.EdgeAligner.isUpperBodyAttack;
            if (equip?.EdgeAligner != null) { equip.EdgeAligner.isAttacking = false; equip.EdgeAligner.isUpperBodyAttack = false; }

            var paladinAnim = equip?.PaladinAnimator;
            if (paladinAnim != null)
            {
                paladinAnim.SetBool("IsAttacking", false);
                paladinAnim.ResetTrigger("Attack");
                if (paladinAnim.layerCount > 1) paladinAnim.CrossFadeInFixedTime("CombatStance", 0.14f, 1);
                if (!wasUpper) paladinAnim.CrossFadeInFixedTime("Locomotion", 0.14f, 0);
            }

            if (_attackHitboxVisualizer != null)
            {
                _attackHitboxVisualizer.IsVolumeActive = false;
                _attackHitboxVisualizer.IsConnecting = false;
            }

            OnAttackEnded?.Invoke();
        }

        public void CancelBlockForRoll()
        {
            _isBlocking = false;
            var eq = GetEquipment();
            if (eq?.EdgeAligner != null) eq.EdgeAligner.isBlocking = false;
            if (eq?.PaladinAnimator != null) eq.PaladinAnimator.SetBool("IsBlocking", false);
        }

        public void TriggerGuardBreak(float duration = 1.15f)
        {
            if (PlayerHealthController.IsGodModeActive) return;
            _blockLockedUntil = Mathf.Max(_blockLockedUntil, Time.time + duration);
            CancelBlockForRoll();
        }

        public void ClearStunAndGuardBreak() => _blockLockedUntil = -99f;

        public void CancelAttackForRoll()
        {
            if (!_isAttacking && !_hasBufferedNextStrike) return;
            EndAttack(); _comboAvailableTime = Time.time;
        }
    }
}
