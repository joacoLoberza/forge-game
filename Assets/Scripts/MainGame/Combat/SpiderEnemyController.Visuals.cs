using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Carga visual 3D, material texturizado y controlador/clips PlayableGraph de respaldo de la Araña.
    /// </summary>
    public partial class SpiderEnemyController
    {
        private void SetupSpiderModelVisual()
        {
            Transform visual = transform.Find("Visual_Spider");
            if (visual == null)
            {
                GameObject fbx = LoadCreatureAsset<GameObject>(
                    "Assets/Models/Character/animated-spider/source/Model.fbx", "Models/SpiderModel");
                if (fbx != null)
                {
                    GameObject go = Instantiate(fbx, transform);
                    go.name = "Visual_Spider";
                    go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    go.transform.localScale = Vector3.one;
                    visual = go.transform;
                }
            }
            if (visual == null) return;

            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float currentSpan = Mathf.Max(b.size.x, b.size.z);
                if (currentSpan > 0.01f) visual.localScale = Vector3.one * (visualLegSpan / currentSpan);

                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                visual.position += new Vector3(0f, transform.position.y - b.min.y, 0f);

                if (_col != null)
                {
                    _col.height = Mathf.Max(0.38f, b.size.y);
                    _col.radius = Mathf.Clamp(visualLegSpan * 0.30f, 0.45f, 0.65f);
                    _col.center = new Vector3(0f, _col.height * 0.5f, 0f);
                }
            }

            Material mat = LoadCreatureAsset<Material>("Assets/Materials/Mat_Spider.mat", "Mat_Spider");
            if (mat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s) { name = "Mat_Spider" };
                Texture2D tex = LoadCreatureAsset<Texture2D>(
                    "Assets/Models/Character/animated-spider/textures/Texture.png", "SpiderTexture");
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            }

            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                smr.enabled = true;
                smr.sharedMaterial = mat;
            }
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true)) { mr.enabled = true; mr.sharedMaterial = mat; }

            if (!visual.TryGetComponent<Animator>(out _animator))
                _animator = visual.GetComponentInChildren<Animator>() ?? visual.gameObject.AddComponent<Animator>();

            var ctrl = LoadCreatureAsset<RuntimeAnimatorController>("Assets/Animations/SpiderAnimator.controller", "Animations/SpiderAnimator");
            if (ctrl != null && _animator != null) _animator.runtimeAnimatorController = ctrl;
            CacheFallbackClips();
        }

        private void CacheFallbackClips()
        {
            _fallbackClips.Clear();
            AnimationClip[] clips = UnityEngine.Resources.LoadAll<AnimationClip>("Models/SpiderModel");
#if UNITY_EDITOR
            if (clips == null || clips.Length == 0)
            {
                var objs = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Character/animated-spider/source/Model.fbx");
                var list = new List<AnimationClip>();
                foreach (var o in objs)
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) list.Add(c);
                clips = list.ToArray();
            }
#endif
            if (clips == null) return;
            foreach (var c in clips)
            {
                if (c == null || c.name.StartsWith("__preview__")) continue;
                string n = c.name.ToLowerInvariant();
                if (n.Contains("attack")) _fallbackClips["attack"] = c;
                else if (n.Contains("die")) _fallbackClips["die"] = c;
                else if (n.Contains("run")) _fallbackClips["run"] = c;
                else if (n.Contains("large walk")) _fallbackClips["largeWalk"] = c;
                else if (n.Contains("walk back")) _fallbackClips["walkBack"] = c;
                else if (n.Contains("walk tight")) _fallbackClips["idle"] = c;
                else if (n.Contains("walk")) _fallbackClips["walk"] = c;
            }
        }

        protected override void PlayGuardedAnim(string key, float fade = 0.12f, bool allowWhenDead = false)
        {
            if ((_isDead && !allowWhenDead) || _activeAnimKey == key) return;
            if (_animator == null)
            {
                Transform v = transform.Find("Visual_Spider");
                if (v != null && !v.TryGetComponent<Animator>(out _animator))
                    _animator = v.GetComponentInChildren<Animator>();
            }
            if (_animator == null) return;

            _activeAnimKey = key;
            if (_animator.runtimeAnimatorController != null)
            {
                _animator.CrossFade(key, fade);
                return;
            }

            if (_fallbackClips.Count == 0) CacheFallbackClips();
            if (_fallbackClips.TryGetValue(key, out AnimationClip clip) && clip != null)
            {
                if (_playableGraph.IsValid()) _playableGraph.Destroy();
                _playableGraph = PlayableGraph.Create($"SpiderGraph_{GetInstanceID()}");
                var output = AnimationPlayableOutput.Create(_playableGraph, "Anim", _animator);
                _currentClipPlayable = AnimationClipPlayable.Create(_playableGraph, clip);
                if (key == "idle") _currentClipPlayable.SetSpeed(0.18);
                output.SetSourcePlayable(_currentClipPlayable);
                _playableGraph.Play();
            }
        }
    }
}
