using System.Collections;
using UnityEngine;
using ForjaGame.Audio;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Modelo 3D e IA ecológica del Golem: vigilancia pacífica si el jugador pasa de largo,
    /// defensa feroz si pican su mena, resonancia lítica (hambre), letargo pétreo (descanso) y patrulla.
    /// </summary>
    public partial class GolemEnemyController
    {
        private void SetupGolemVisualModel()
        {
            Transform visual = transform.Find("Visual_Golem");
            if (visual == null)
            {
                GameObject fbxPrefab = LoadCreatureAsset<GameObject>(
                    "Assets/Models/Character/golem-animation/source/Model.fbx", "Models/GolemModel");
                if (fbxPrefab != null)
                {
                    GameObject go = Instantiate(fbxPrefab, transform);
                    go.name = "Visual_Golem";
                    go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go.transform.localScale = Vector3.one * (100.0f * visualScaleMultiplier);
                    visual = go.transform;
                }
            }
            else visual.localScale = Vector3.one * (100.0f * visualScaleMultiplier);

            if (visual == null) return;

            if (!visual.TryGetComponent<Animator>(out _animator))
                _animator = visual.GetComponentInChildren<Animator>() ?? visual.gameObject.AddComponent<Animator>();

            var ctrl = LoadCreatureAsset<RuntimeAnimatorController>("Assets/Animations/GolemAnimator.controller", "Animations/GolemAnimator");
            if (ctrl != null && _animator != null) _animator.runtimeAnimatorController = ctrl;

            Material mat = LoadCreatureAsset<Material>("Assets/Materials/Mat_GolemRock.mat", "Mat_GolemRock");
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s) { name = "Mat_GolemRock" };
                Texture2D tex = LoadCreatureAsset<Texture2D>("Assets/Models/Character/golem-animation/textures/GOLEM_SKIN_rock_12.png", "GolemTexture");
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }
            }

            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                smr.enabled = true;
                smr.sharedMaterial = mat;
            }
            foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true)) { r.enabled = true; r.sharedMaterial = mat; }

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0 && _col != null)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                if (b.size.y > 0.5f)
                {
                    _col.height = b.size.y;
                    _col.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.35f, 0.6f, 1.1f);
                    _col.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                }
            }
        }

        private void Update()
        {
            if (!IsAlive) return;
            _playerTransform = FindClosestLivingPlayer(transform.position);
            Vector3 toPlayer = _playerTransform != null ? (_playerTransform.position - transform.position) : Vector3.zero;
            toPlayer.y = 0f;
            float dist = _playerTransform != null ? toPlayer.magnitude : 999f;

            if (_isRoaring)
            {
                StopHorizontalMovement();
                if (_playerTransform != null) FaceDirection(toPlayer, 7f);
                if (Time.time >= _stateEndTime) _isRoaring = false;
                return;
            }

            if (_isAttacking)
            {
                StopHorizontalMovement();
                if (_playerTransform != null) FaceDirection(toPlayer, 6f);
                if (Time.time >= _stateEndTime)
                {
                    _isAttacking = false;
                    _nextAttackCooldown = Time.time + 1.05f;
                    _currentAttackIndex = (_currentAttackIndex + 1) % _attackSequence.Length;
                }
                return;
            }

            float hpRatio = (float)currentHealth / Mathf.Max(1, maxHealth);
            EcologicalIntent intent = _ecoBrain.Evaluate(transform.position, hpRatio, _playerTransform, Time.deltaTime);

            switch (intent)
            {
                case EcologicalIntent.SelfDefense:
                case EcologicalIntent.TerritorialDefense:
                    SetAnimatorSpeed(1.0f);
                    ExecuteGuardianDefense(toPlayer, dist);
                    break;

                case EcologicalIntent.WatchfulVigil:
                    _hasRoaredThisEngagement = false;
                    StopHorizontalMovement();
                    SetAnimatorSpeed(0.55f);
                    if (_playerTransform != null) FaceDirection(toPlayer, 3.8f);
                    PlayGuardedAnim("idle");
                    break;

                case EcologicalIntent.FeedingOnVein:
                    _hasRoaredThisEngagement = false;
                    SetAnimatorSpeed(1.0f);
                    ExecuteVeinChanneling();
                    break;

                case EcologicalIntent.Resting:
                    _hasRoaredThisEngagement = false;
                    StopHorizontalMovement();
                    SetAnimatorSpeed(0.16f);
                    PlayGuardedAnim("idle", 0.35f);
                    break;

                default:
                    _hasRoaredThisEngagement = false;
                    SetAnimatorSpeed(0.88f);
                    ExecuteOrganicPatrol();
                    break;
            }
        }

        private void ExecuteGuardianDefense(Vector3 toPlayer, float dist)
        {
            if (!_hasRoaredThisEngagement)
            {
                _hasRoaredThisEngagement = true;
                _isRoaring = true;
                _stateEndTime = Time.time + 1.65f;
                StopHorizontalMovement();
                ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Aggro);
                PlayGuardedAnim("roar");
                return;
            }

            if (dist <= attackRange)
            {
                StopHorizontalMovement();
                FaceDirection(toPlayer, 8f);
                if (Time.time >= _nextAttackCooldown) ExecuteSequentialAttack();
                else PlayGuardedAnim("idle");
            }
            else if (_playerTransform != null)
            {
                MoveAlongCave(_playerTransform.position, moveSpeed, 7.5f);
                PlayGuardedAnim("walkNormal");
            }
        }

        private void ExecuteVeinChanneling()
        {
            var vein = _ecoBrain.TargetVein;
            if (vein == null || !vein.IsAlive) { ExecuteOrganicPatrol(); return; }

            if (_ecoBrain.TryPerformFeedBite(transform.position, out bool channeled))
            {
                StopHorizontalMovement();
                FaceDirection(vein.transform.position - transform.position, 6f);
                PlayGuardedAnim("attack3");
                if (channeled)
                {
                    currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.RoundToInt(maxHealth * 0.08f));
                    ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Feed);
                }
            }
            else
            {
                MoveAlongCave(vein.transform.position, moveSpeed * 0.68f, 6f);
                PlayGuardedAnim("walkNormal");
            }
        }

        private void ExecuteOrganicPatrol()
        {
            if (_ecoBrain.IsPatrolMoving)
            {
                MoveAlongCave(_ecoBrain.PatrolTarget, moveSpeed * 0.56f, 5.5f);
                PlayGuardedAnim("walkNormal");
            }
            else { StopHorizontalMovement(); PlayGuardedAnim("idle"); }
        }

        private void ExecuteSequentialAttack()
        {
            _isAttacking = true;
            _stateEndTime = Time.time + 1.38f;
            _activeAnimKey = string.Empty;
            PlayGuardedAnim(_attackSequence[_currentAttackIndex], 0.10f);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Attack);
            StartCoroutine(ApplyStrikeDelayed(0.54f));
        }

        private IEnumerator ApplyStrikeDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            TryDealConeDamageToPlayer(attackRange + 0.50f, 72f, 1.45f, attackDamage);
        }
    }
}
