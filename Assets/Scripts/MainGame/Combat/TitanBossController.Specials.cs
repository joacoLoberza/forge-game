using System.Collections;
using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Ataques especiales procedurales y gesti&oacute;n de peligros de arena del Tit&aacute;n Abisal.
    /// </summary>
    public partial class TitanBossController
    {
        private void TriggerNextSpecialAttack(Vector3 toPlayer)
        {
            FaceDirection(toPlayer, 6.5f);
            int mode = _specialAttackIndex++ % 3;
            _nextSpecialTime = Time.time + 10.0f;

            if (mode == 0) StartCoroutine(ExecuteShockwaveRingRoutine());
            else if (mode == 1) StartCoroutine(ExecuteStalactiteBarrageRoutine());
            else StartCoroutine(ExecuteAbyssalSpireLineRoutine());
        }

        private IEnumerator WaitTitanClipTarget(float targetClipTime, float maxWait)
        {
            float elapsed = 0f;
            while (IsAlive && !_isStaggered && elapsed < maxWait)
            {
                if (GetPlayableClipTime(_currentClipPlayable, _animator, elapsed) >= targetClipTime) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void FinishSpecialRecovery()
        {
            bool phase2 = currentHealth <= maxHealth * 0.55f;
            SetAnimatorSpeed(1.0f);
            _isPerformingSpecial = false;
            _nextSpecialTime = Time.time + (phase2 ? 6.0f : 7.8f);
            _nextMeleeTime = Mathf.Max(_nextMeleeTime, Time.time + 1.40f);
        }

        private void ApplyTitanVisualMaterials(Transform visual)
        {
            Texture2D tex = LoadCreatureAsset<Texture2D>("Assets/Models/Character/titan-animation/textures/Leviathan_ColorAlpha.png", "TitanColorAlphaTexture")
                ?? LoadCreatureAsset<Texture2D>("Assets/Resources/TitanColorAlphaTexture.png", "TitanColorAlphaTexture");
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material bodyMat = new Material(s) { name = "Mat_TitanBody", color = Color.white };
            Material crestMat = new Material(s) { name = "Mat_TitanCrestAlpha", color = Color.white };
            if (tex != null)
            {
                if (bodyMat.HasProperty("_BaseMap")) bodyMat.SetTexture("_BaseMap", tex);
                if (bodyMat.HasProperty("_MainTex")) bodyMat.SetTexture("_MainTex", tex);
                if (crestMat.HasProperty("_BaseMap")) crestMat.SetTexture("_BaseMap", tex);
                if (crestMat.HasProperty("_MainTex")) crestMat.SetTexture("_MainTex", tex);
            }
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.32f);
            if (bodyMat.HasProperty("_Metallic")) bodyMat.SetFloat("_Metallic", 0.16f);
            if (crestMat.HasProperty("_AlphaClip")) crestMat.SetFloat("_AlphaClip", 1f);
            if (crestMat.HasProperty("_Cutoff")) crestMat.SetFloat("_Cutoff", 0.40f);
            if (crestMat.HasProperty("_Cull")) crestMat.SetFloat("_Cull", 0f);
            crestMat.EnableKeyword("_ALPHATEST_ON");

            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true; smr.enabled = true;
                bool isCrest = smr.name.Contains("Crest") || smr.name.Contains("Tail");
                smr.sharedMaterial = isCrest ? crestMat : bodyMat;
            }
            foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
            { r.enabled = true; r.sharedMaterial = bodyMat; }
        }

        private IEnumerator ExecuteShockwaveRingRoutine()
        {
            _isPerformingSpecial = true;
            bool phase2 = currentHealth <= maxHealth * 0.55f;
            float windSpd = phase2 ? 0.64f : 0.56f;
            SetAnimatorSpeed(windSpd);
            PlayGuardedAnim("AttackHard", 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Aggro);
            TitanAttackTelegraphBuilder.SpawnShockwaveChargeTelegraph(this, GetArenaFxRoot(), transform.position, 1.16f / windSpd);
            yield return WaitTitanClipTarget(0.92f, 2.2f);
            if (!IsAlive || _isStaggered) { FinishSpecialRecovery(); yield break; }
            SetAnimatorSpeed(0.92f);
            yield return WaitTitanClipTarget(1.16f, 0.8f);
            if (!IsAlive || _isStaggered) { FinishSpecialRecovery(); yield break; }

            PlayerCameraFollow.Instance?.TriggerImpulse(0.28f);
            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Attack);
            TitanArenaHazardBuilder.SpawnExpandingShockwave(GetArenaFxRoot(), transform.position, 24.5f, 1.05f, 54);
            yield return WaitTitanClipTarget(2.10f, 1.5f);
            FinishSpecialRecovery();
        }

        private IEnumerator ExecuteStalactiteBarrageRoutine()
        {
            // Usa rugido/convocatoria mirando al techo (GetHit2) en lugar de AttackHard para no confundir con el golpe al suelo de la Onda de Choque
            _isPerformingSpecial = true;
            bool phase2 = currentHealth <= maxHealth * 0.55f;
            float windSpd = phase2 ? 0.68f : 0.58f;
            SetAnimatorSpeed(windSpd);
            PlayGuardedAnim("GetHit2", 0.10f, forceRestart: true);
            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Aggro);
            Vector3 center = _playerTransform != null ? _playerTransform.position : transform.position + transform.forward * 7f;
            if (_playerTransform != null && _playerTransform.TryGetComponent<Rigidbody>(out var prb))
                center += new Vector3(prb.linearVelocity.x, 0f, prb.linearVelocity.z) * 0.40f;
            yield return WaitTitanClipTarget(0.85f, 1.8f);
            if (!IsAlive || _isStaggered) { FinishSpecialRecovery(); yield break; }

            PlayerCameraFollow.Instance?.TriggerImpulse(0.22f);
            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Attack);
            TitanArenaHazardBuilder.SpawnStalactiteBarrage(this, GetArenaFxRoot(), center, 7, 54);
            yield return WaitTitanClipTarget(1.55f, 1.2f);
            FinishSpecialRecovery();
        }

        private IEnumerator ExecuteAbyssalSpireLineRoutine()
        {
            _isPerformingSpecial = true;
            bool phase2 = currentHealth <= maxHealth * 0.55f;
            float windSpd = phase2 ? 0.64f : 0.56f;
            SetAnimatorSpeed(windSpd);
            PlayGuardedAnim("Attack2", 0.08f, forceRestart: true);
            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Aggro);

            Vector3 dir = _playerTransform != null ? (_playerTransform.position - transform.position) : transform.forward;
            dir.y = 0f; if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            Vector3 leftDir = Quaternion.Euler(0f, -22f, 0f) * dir;
            Vector3 rightDir = Quaternion.Euler(0f, 22f, 0f) * dir;
            float warnDur = 1.22f / windSpd;
            TitanAttackTelegraphBuilder.SpawnSpireWarningLine(this, GetArenaFxRoot(), transform.position + dir * 1.5f, dir, 19f, warnDur);
            TitanAttackTelegraphBuilder.SpawnSpireWarningLine(this, GetArenaFxRoot(), transform.position + leftDir * 1.5f, leftDir, 19f, warnDur);
            TitanAttackTelegraphBuilder.SpawnSpireWarningLine(this, GetArenaFxRoot(), transform.position + rightDir * 1.5f, rightDir, 19f, warnDur);

            yield return WaitTitanClipTarget(0.95f, 2.2f);
            if (!IsAlive || _isStaggered) { FinishSpecialRecovery(); yield break; }
            SetAnimatorSpeed(0.94f);
            yield return WaitTitanClipTarget(1.22f, 0.8f);
            if (!IsAlive || _isStaggered) { FinishSpecialRecovery(); yield break; }

            ForgeAudioSystem.PlayTitanSound(transform.position, CreatureSoundType.Attack);
            TitanArenaHazardBuilder.SpawnAbyssalSpireLine(this, GetArenaFxRoot(), transform.position + dir * 2.2f, dir, 6, 52);
            TitanArenaHazardBuilder.SpawnAbyssalSpireLine(this, GetArenaFxRoot(), transform.position + leftDir * 2.2f, leftDir, 5, 52);
            TitanArenaHazardBuilder.SpawnAbyssalSpireLine(this, GetArenaFxRoot(), transform.position + rightDir * 2.2f, rightDir, 5, 52);
            yield return WaitTitanClipTarget(2.08f, 1.5f);
            FinishSpecialRecovery();
        }

        private Transform GetArenaFxRoot() => _arenaFxRoot != null ? _arenaFxRoot : (_arenaFxRoot = new GameObject("[TITAN_ARENA_FX]").transform);

        private void ClearActiveArenaHazards()
        {
            if (_arenaFxRoot != null) UnitySafeUtility.DestroyGameObjectSafe(_arenaFxRoot.gameObject);
        }
    }
}

