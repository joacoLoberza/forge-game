using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Configuración visual 3D, auto-escalado de malla y reproducción de animaciones FBX
    /// con mapeo canónico ("Armature|walk" -> "walk", etc.) y bucle garantizado para SpiderEnemyController.
    /// </summary>
    public partial class SpiderEnemyController
    {
        private static bool IsLoopingSpiderClip(string key) =>
            key == "walk" || key == "run" || key == "largeWalk" || key == "walkBack" || key == "idle";

        protected override void OnAnimatorSpeedChanged(float speed)
        {
            if (_currentClipPlayable.IsValid()) _currentClipPlayable.SetSpeed(Mathf.Max(0.05f, speed));
        }

        protected override void PlayGuardedAnim(string stateName, float fade = 0.12f, bool allowWhenDead = false, bool forceRestart = false)
        {
            if ((_isDead && !allowWhenDead) || _animator == null) return;
            bool isLoop = IsLoopingSpiderClip(stateName);

            if (!forceRestart && _activeAnimKey == stateName)
            {
                if (_currentClipPlayable.IsValid())
                {
                    _currentClipPlayable.SetSpeed(Mathf.Max(0.05f, _animator.speed));
                    var activeClip = _currentClipPlayable.GetAnimationClip();
                    if (isLoop && activeClip != null && activeClip.length > 0.05f
                        && _currentClipPlayable.GetTime() >= activeClip.length - 0.03f)
                    {
                        _currentClipPlayable.SetTime(0);
                    }
                }
                else if (isLoop && _animator.runtimeAnimatorController != null && !_animator.IsInTransition(0)
                         && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.96f)
                {
                    _animator.CrossFadeInFixedTime(stateName, 0.06f, 0, 0f);
                }
                return;
            }

            _activeAnimKey = stateName;
            if (_fallbackClips.TryGetValue(stateName, out var clip) && clip != null)
            {
                if (!_playableGraph.IsValid())
                {
                    _playableGraph = PlayableGraph.Create($"SpiderAnim_{GetInstanceID()}");
                    _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    _playableOutput = AnimationPlayableOutput.Create(_playableGraph, "SpiderAnimOut", _animator);
                }
                if (_currentClipPlayable.IsValid()) _currentClipPlayable.Destroy();
                _currentClipPlayable = AnimationClipPlayable.Create(_playableGraph, clip);
                _currentClipPlayable.SetTime(0);
                _currentClipPlayable.SetSpeed(Mathf.Max(0.05f, _animator.speed));
                _playableOutput.SetSourcePlayable(_currentClipPlayable);
                _playableGraph.Play();
                return;
            }

            if (_animator.runtimeAnimatorController != null)
                _animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
        }

        private void SetupSpiderModelVisual()
        {
            Transform visual = transform.Find("Visual_Spider");
            if (visual == null)
            {
                GameObject fbxPrefab = LoadCreatureAsset<GameObject>(
                    "Assets/Models/Character/animated-spider/source/Model.fbx", "Models/SpiderModel");
                if (fbxPrefab != null)
                {
                    GameObject go = Instantiate(fbxPrefab, transform);
                    go.name = "Visual_Spider";
                    go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go.transform.localScale = Vector3.one;
                    visual = go.transform;
                }
            }

            if (visual == null) return;

            if (!visual.TryGetComponent<Animator>(out _animator) || _animator == null)
            {
                _animator = visual.GetComponentInChildren<Animator>();
                if (_animator == null) _animator = visual.gameObject.AddComponent<Animator>();
            }
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.applyRootMotion = false;

            var ctrl = LoadCreatureAsset<RuntimeAnimatorController>(
                "Assets/Resources/Animations/SpiderAnimator.controller", "Animations/SpiderAnimator")
                ?? LoadCreatureAsset<RuntimeAnimatorController>(
                    "Assets/Animations/SpiderAnimator.controller", "AnimatorControllers/SpiderAnimator");
            if (ctrl != null)
            {
                _animator.runtimeAnimatorController = ctrl;
                foreach (var c in ctrl.animationClips) RegisterSpiderClip(c);
            }
            LoadFallbackSpiderClips();

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material bodyMat = new Material(s) { name = "Mat_SpiderChitin" };
            Texture2D bodyTex = LoadCreatureAsset<Texture2D>(
                "Assets/Models/Character/animated-spider/textures/Spinnen_Bein_tex_COLOR_.jpg", "Textures/SpiderBodyTex");
            if (bodyTex != null)
            {
                if (bodyMat.HasProperty("_BaseMap")) bodyMat.SetTexture("_BaseMap", bodyTex);
                if (bodyMat.HasProperty("_MainTex")) bodyMat.SetTexture("_MainTex", bodyTex);
            }
            else bodyMat.color = new Color(0.18f, 0.14f, 0.11f);
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.38f);

            Material hairMat = new Material(s) { name = "Mat_SpiderHair" };
            Texture2D hairTex = LoadCreatureAsset<Texture2D>(
                "Assets/Models/Character/animated-spider/textures/haar_detail_N.jpg", "Textures/SpiderHairTex");
            if (hairTex != null)
            {
                if (hairMat.HasProperty("_BaseMap")) hairMat.SetTexture("_BaseMap", hairTex);
                if (hairMat.HasProperty("_MainTex")) hairMat.SetTexture("_MainTex", hairTex);
            }
            hairMat.color = new Color(0.24f, 0.19f, 0.15f);

            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                smr.enabled = true;
                var shared = smr.sharedMaterials;
                if (shared == null || shared.Length <= 1) smr.sharedMaterial = bodyMat;
                else
                {
                    for (int i = 0; i < shared.Length; i++) shared[i] = (i == 0) ? hairMat : bodyMat;
                    smr.sharedMaterials = shared;
                }
            }
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.enabled = true;
                mr.sharedMaterial = bodyMat;
            }

            NormalizeSpiderBounds(visual);
            PlayGuardedAnim("idle", 0.1f, forceRestart: true);
        }

        private void NormalizeSpiderBounds(Transform visual)
        {
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float maxSpan = Mathf.Max(b.size.x, b.size.z);
            float scale = maxSpan > 0.001f ? (visualLegSpan / maxSpan) : 1.0f;
            visual.localScale = Vector3.one * scale;

            b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float bottomOffset = transform.position.y - b.min.y;
            visual.localPosition = new Vector3(0f, bottomOffset, 0f);
        }

        private void LoadFallbackSpiderClips()
        {
            foreach (var c in UnityEngine.Resources.LoadAll<AnimationClip>("Models/SpiderModel"))
                RegisterSpiderClip(c);
#if UNITY_EDITOR
            foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Character/animated-spider/source/Model.fbx"))
                if (o is AnimationClip c) RegisterSpiderClip(c);
#endif
            if (!_fallbackClips.ContainsKey("idle") && _fallbackClips.TryGetValue("walk", out var w))
                _fallbackClips["idle"] = w;
        }

        private void RegisterSpiderClip(AnimationClip c)
        {
            if (c == null || c.name.StartsWith("__preview__")) return;
            string n = c.name.ToLowerInvariant();
            string key = null;
            if (n.Contains("walk tight") || n == "idle") key = "idle";
            else if (n.Contains("large walk") || n.Contains("largewalk")) key = "largeWalk";
            else if (n.Contains("walk back") || n.Contains("walkback")) key = "walkBack";
            else if (n.Contains("walk")) key = "walk";
            else if (n.Contains("run")) key = "run";
            else if (n.Contains("attack")) key = "attack";
            else if (n.Contains("die") || n.Contains("death")) key = "die";

            if (key != null)
            {
                if (IsLoopingSpiderClip(key)) c.wrapMode = WrapMode.Loop;
                _fallbackClips[key] = c;
            }
            _fallbackClips[c.name] = c;
        }
    }
}
