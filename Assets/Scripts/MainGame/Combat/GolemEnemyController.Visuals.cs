using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    public partial class GolemEnemyController
    {
        private void SetupGolemVisualModel()
        {
            Transform visual = transform.Find("Visual_Golem");
            visualScaleMultiplier = Mathf.Clamp(visualScaleMultiplier, 0.85f, 2.30f);
            float baseScale = 100.0f * visualScaleMultiplier;
            if (visual == null)
            {
                GameObject fbxPrefab = LoadCreatureAsset<GameObject>(
                    "Assets/Models/Character/golem-animation/source/Model.fbx", "Models/GolemModel");
                if (fbxPrefab != null)
                {
                    GameObject go = Instantiate(fbxPrefab, transform);
                    go.name = "Visual_Golem";
                    go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go.transform.localScale = Vector3.one * baseScale;
                    visual = go.transform;
                }
            }
            else visual.localScale = Vector3.one * baseScale;

            if (visual == null) return;

            if (!visual.TryGetComponent<Animator>(out _animator) || _animator == null)
            {
                _animator = visual.GetComponentInChildren<Animator>();
                if (_animator == null) _animator = visual.gameObject.AddComponent<Animator>();
            }
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var ctrl = LoadCreatureAsset<RuntimeAnimatorController>(
                "Assets/Resources/Animations/GolemAnimator.controller", "Animations/GolemAnimator")
                ?? LoadCreatureAsset<RuntimeAnimatorController>(
                    "Assets/Animations/GolemAnimator.controller", "AnimatorControllers/GolemAnimator");
            if (ctrl != null) { _animator.runtimeAnimatorController = ctrl; foreach (var c in ctrl.animationClips) RegisterGolemClip(c); }
            LoadFallbackGolemClips();

            Material mat = LoadCreatureAsset<Material>("Assets/Materials/Mat_GolemRock.mat", "Mat_GolemRock");
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s) { name = "Mat_GolemRock" };
                Texture2D tex = LoadCreatureAsset<Texture2D>("Assets/Models/Character/golem-animation/textures/GOLEM_SKIN_rock_12.png", "GolemTexture");
                if (tex != null) { if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex); }
            }
            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.updateWhenOffscreen = true; smr.enabled = true; smr.sharedMaterial = mat; }
            foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true)) { r.enabled = true; r.sharedMaterial = mat; }

            SetupGolemMineralCoreVisual(visual);

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0 && _col != null)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float maxAllowedHeight = 2.75f * visualScaleMultiplier;
                if (b.size.y > maxAllowedHeight) visual.localScale *= (maxAllowedHeight / b.size.y);
                if (b.size.y > 0.5f)
                {
                    _col.height = Mathf.Clamp(b.size.y, 2.2f, 5.5f);
                    _col.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.35f, 0.75f, 2.25f);
                    _col.center = new Vector3(0f, _col.height * 0.5f, 0f);
                }
            }
        }

        private Light _golemCoreLight;
        private Material _golemCrystalMat;

        private void SetupGolemMineralCoreVisual(Transform visual)
        {
            if (visual.Find("GolemMineralCoreFX") != null) return;
            Color crystalCol = GetGolemVeinCrystalColor();
            var fxRoot = new GameObject("GolemMineralCoreFX");
            fxRoot.transform.SetParent(visual, false);
            fxRoot.transform.localPosition = Vector3.zero;
            float invScale = 1f / Mathf.Max(0.01f, visual.localScale.y);

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _golemCrystalMat = new Material(s) { name = "Mat_GolemCrystalCore", color = crystalCol };
            if (_golemCrystalMat.HasProperty("_BaseColor")) _golemCrystalMat.SetColor("_BaseColor", crystalCol);
            if (_golemCrystalMat.HasProperty("_EmissionColor"))
            { _golemCrystalMat.EnableKeyword("_EMISSION"); _golemCrystalMat.SetColor("_EmissionColor", crystalCol * 1.85f); }

            // Cristales de veta incrustados en pecho/hombros + luz mineral tenue para dar silueta única en la oscuridad
            Vector3[] offsets = { new(0f, 1.45f, 0.42f), new(-0.48f, 1.95f, -0.05f), new(0.48f, 1.95f, -0.05f), new(0f, 1.85f, -0.40f) };
            Vector3[] rots = { new(15f, 0f, 12f), new(-12f, 25f, 38f), new(-12f, -25f, -38f), new(-35f, 180f, 8f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                var shard = new GameObject($"MineralShard_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                shard.transform.SetParent(fxRoot.transform, false);
                shard.transform.localPosition = offsets[i] * (visualScaleMultiplier * invScale);
                shard.transform.localRotation = Quaternion.Euler(rots[i]);
                shard.transform.localScale = Vector3.one * ((i == 0 ? 0.34f : 0.28f) * visualScaleMultiplier * invScale);
                shard.GetComponent<MeshFilter>().sharedMesh = ForjaGame.Resources.OreMeshGenerator.CreateCrystalSpireMesh(0.45f, 1.15f, 140 + i * 31);
                shard.GetComponent<MeshRenderer>().sharedMaterial = _golemCrystalMat;
            }
            var lGo = new GameObject("GolemCoreLight", typeof(Light));
            lGo.transform.SetParent(fxRoot.transform, false);
            lGo.transform.localPosition = new Vector3(0f, 1.55f, 0.45f) * (visualScaleMultiplier * invScale);
            _golemCoreLight = lGo.GetComponent<Light>();
            _golemCoreLight.type = LightType.Point; _golemCoreLight.color = crystalCol;
            _golemCoreLight.range = 4.6f * visualScaleMultiplier; _golemCoreLight.intensity = 1.45f;
        }

        private Color GetGolemVeinCrystalColor()
        {
            string oreId = _boundVein != null ? _boundVein.ItemId : string.Empty;
            if (oreId == "Coal") return new Color(0.95f, 0.38f, 0.08f);
            if (oreId == "Copper") return new Color(0.18f, 0.84f, 0.58f);
            if (oreId == "Gold") return new Color(1.0f, 0.80f, 0.20f);
            return new Color(0.32f, 0.68f, 0.96f);
        }

        private void RefreshGolemMineralCoreColor()
        {
            Color col = GetGolemVeinCrystalColor();
            if (_golemCrystalMat != null)
            {
                _golemCrystalMat.color = col;
                if (_golemCrystalMat.HasProperty("_BaseColor")) _golemCrystalMat.SetColor("_BaseColor", col);
                if (_golemCrystalMat.HasProperty("_EmissionColor")) { _golemCrystalMat.EnableKeyword("_EMISSION"); _golemCrystalMat.SetColor("_EmissionColor", col * 1.85f); }
            }
            if (_golemCoreLight != null) _golemCoreLight.color = col;
        }

        private void ExecuteGolemDeathSequence()
        {
            if (_isDead) return;
            _isDead = true; ClearKnockbackState();
            EnemyAttackTelegraph.Cancel(this);
            StopHorizontalMovement();
            if (_col != null) _col.enabled = false;
            if (_rb != null) _rb.isKinematic = true;
            if (_golemCoreLight != null) _golemCoreLight.enabled = false;
            if (_golemCrystalMat != null && _golemCrystalMat.HasProperty("_EmissionColor")) _golemCrystalMat.SetColor("_EmissionColor", Color.black);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Hurt);
            ForgeAudioSystem.PlayHitImpact(transform.position + Vector3.up, true, true);
            GetComponent<EnemyLootDropper>()?.DropLoot(transform.position);
            NotifyCreatureDiedBase();
            StartCoroutine(GolemCollapseRoutine());
        }

        private System.Collections.IEnumerator GolemCollapseRoutine()
        {
            SetAnimatorSpeed(0.65f);
            PlayGuardedAnim("roar", 0.06f, allowWhenDead: true, forceRestart: true);
            yield return new WaitForSeconds(0.22f);
            SetAnimatorSpeed(0f);

            Transform vis = transform.Find("Visual_Golem");
            Quaternion startR = vis != null ? vis.localRotation : Quaternion.identity;
            Quaternion fallR = startR * Quaternion.Euler(86f, 0f, 14f);
            Vector3 startP = vis != null ? vis.localPosition : Vector3.zero;
            for (float t = 0f; t < 0.62f; t += Time.deltaTime)
            {
                float u = t / 0.62f; float ease = u * u;
                if (vis != null) { vis.localRotation = Quaternion.Slerp(startR, fallR, ease); vis.localPosition = startP + new Vector3(0f, -0.35f * ease, 0.25f * ease); }
                yield return null;
            }
            PlayerCameraFollow.Instance?.TriggerImpulse(0.18f);
            ForgeAudioSystem.PlayGolemSound(transform.position, CreatureSoundType.Attack);
            EnemyAttackTelegraph.EmitImpactDust(transform.position + Vector3.up * 0.35f, Vector3.up, 10, 0.95f);
            SpawnShatterDebris();
            yield return new WaitForSeconds(1.25f);
            Vector3 rootStart = transform.position;
            for (float s = 0f; s < 1.25f; s += Time.deltaTime) { transform.position = rootStart - Vector3.up * (s * 1.1f); yield return null; }
            UnitySafeUtility.DestroyGameObjectSafe(gameObject);
        }

        private void SpawnShatterDebris()
        {
            var rockMat = ForjaGame.Environment.CaveDungeonBuilder.GetCaveRockMaterial();
            for (int i = 0; i < 10; i++)
            {
                var c = new GameObject($"GolemDebris_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                c.GetComponent<MeshFilter>().sharedMesh = ForjaGame.Resources.OreMeshGenerator.CreateCrystalSpireMesh(0.22f, 0.36f, 200 + i * 17);
                c.GetComponent<MeshRenderer>().sharedMaterial = i % 3 == 0 && _golemCrystalMat != null ? _golemCrystalMat : rockMat;
                c.transform.SetPositionAndRotation(transform.position + new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(0.4f, 1.4f), Random.Range(-0.7f, 0.7f)), Random.rotation);
                var rb = c.AddComponent<Rigidbody>(); rb.mass = 8f;
                rb.AddExplosionForce(180f, transform.position + Vector3.up * 0.5f, 3.0f, 1.2f);
                Destroy(c, 2.2f);
            }
        }
    }
}

