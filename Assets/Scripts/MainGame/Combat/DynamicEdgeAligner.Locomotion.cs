using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Estabilización post-Animator para transporte con arma al caminar/correr (peso realista y despeje anatómico
    /// para evitar que brazos/armas atraviesen el torso), utilidades de guardia y detección en cápsula sobre la hoja real.
    /// </summary>
    public partial class DynamicEdgeAligner
    {
        private readonly Collider[] _bladeSweepCache = new Collider[24];
        private float _stridePhase;

        private void ApplyLocomotionArmClearanceAndWeight()
        {
            if (_anim == null) return;
            float locoSpeed = _anim.GetFloat("Speed");
            float moveT = Mathf.Clamp01(locoSpeed / 1.75f);
            if (moveT <= 0.02f) return;

            _stridePhase += Time.deltaTime * Mathf.Lerp(5.4f, 8.8f, moveT);
            Vector3 chestRef = _chestBone != null ? _chestBone.position : (transform.position + Vector3.up * 1.15f);

            // Despeje lateral para 1H (evita clipping con torso y cadencia exagerada); en 2H preserva el agarre IK sin separarlo del mango
            ApplyArmClearance(_rightUpperArm, _rightHandBone, chestRef, +1f, moveT, isWeaponArm: true);

            if (!isTwoHandedWeapon)
                ApplyArmClearance(_leftUpperArm, _leftHandBone, chestRef, -1f, moveT, isWeaponArm: false);
            else
                ApplyArmClearance(_leftUpperArm, _leftLowerArm ?? _leftHandBone, chestRef, -1f, moveT * 0.45f, isWeaponArm: false);
        }

        private void ApplyArmClearance(Transform upperArm, Transform distalBone, Vector3 chestRef, float sideSign, float moveT, bool isWeaponArm)
        {
            if (upperArm == null || distalBone == null) return;
            Vector3 armVec = distalBone.position - upperArm.position;
            if (armVec.sqrMagnitude < 0.002f) return;
            Vector3 armDir = armVec.normalized;

            Vector3 sideOut = transform.right * sideSign;
            Vector3 targetOutDir = (sideOut * 0.85f + transform.forward * 0.42f).normalized;
            Vector3 abductAxis = Vector3.Cross(armDir, targetOutDir);
            if (abductAxis.sqrMagnitude < 0.0001f) abductAxis = transform.forward * sideSign;
            abductAxis.Normalize();

            float baseAbduct = (isTwoHandedWeapon ? (isWeaponArm ? 3.5f : 5.5f) : 12.5f) * moveT;
            float weightCadence = isWeaponArm
                ? (Mathf.Sin(_stridePhase) * (isTwoHandedWeapon ? 0.8f : 2.6f) * moveT)
                : (Mathf.Sin(_stridePhase + Mathf.PI) * (isTwoHandedWeapon ? 0.6f : 2.2f) * moveT);

            Vector3 relFlat = Vector3.ProjectOnPlane(distalBone.position - chestRef, Vector3.up);
            float minLat = isTwoHandedWeapon ? 0.14f : 0.25f;
            float latFromCenter = Vector3.Dot(relFlat, sideOut);
            float fwdRel = Vector3.Dot(relFlat, transform.forward);
            float torsoPush = (fwdRel < 0.26f && latFromCenter < minLat)
                ? Mathf.Clamp((minLat - latFromCenter) * 145f, 0f, isTwoHandedWeapon ? 12f : 30f) : 0f;

            upperArm.rotation = Quaternion.AngleAxis(baseAbduct + torsoPush, abductAxis)
                              * Quaternion.AngleAxis(weightCadence, transform.right)
                              * upperArm.rotation;
        }

        public static float ResolveAttackClipLength(Animator anim, int comboIdx, float fallback)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return fallback;
            string slotKey = comboIdx switch { 0 => "Attack1H01_R", 1 => "Attack1H01_L", 2 => "AttackShield01", 3 => "AttackPolearm01", _ => "Attack2H01" };
            if (anim.runtimeAnimatorController is AnimatorOverrideController aoc)
            {
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                aoc.GetOverrides(pairs);
                for (int i = 0; i < pairs.Count; i++)
                    if (pairs[i].Key != null && pairs[i].Key.name.Contains(slotKey))
                        return Mathf.Max(0.35f, (pairs[i].Value != null ? pairs[i].Value : pairs[i].Key).length);
            }
            var clips = anim.runtimeAnimatorController.animationClips;
            for (int i = 0; clips != null && i < clips.Length; i++)
                if (clips[i] != null && clips[i].name.Contains(slotKey)) return Mathf.Max(0.35f, clips[i].length);
            return fallback;
        }

        private void ApplyGuardParryAndBashPose(ref Quaternion socketRot)
        {
            float bashPhase = _bashTimer > 0f ? Mathf.Sin((1f - _bashTimer / 0.38f) * Mathf.PI) : 0f;
            float parryPhase = _parryTimer > 0f ? Mathf.Sin((1f - _parryTimer / 0.32f) * Mathf.PI) : 0f;
            if (_chestBone != null)
                _chestBone.localRotation *= Quaternion.Euler(bashPhase * 16f - parryPhase * 10f, -14f * _blockBlend, 0f);
            socketRot *= Quaternion.Euler(-16f * parryPhase, 18f * parryPhase, 12f * parryPhase) * Quaternion.Euler(20f * bashPhase, -10f * bashPhase, 0f);
        }

        private void ApplyAttackSwingArcEnhancement(AttackMove move, float progress01)
        {
            if (_chestBone == null || move == null) return;
            float t = Mathf.Clamp01(progress01);
            // Arco progresivo: preparación abierta (-12°) -> barrido amplio de tajo (+20°) -> regreso suave
            float arc = t < 0.28f
                ? -Mathf.Sin((t / 0.28f) * Mathf.PI * 0.5f) * 12f
                : (t < 0.72f ? Mathf.Lerp(-12f, 20f, Mathf.SmoothStep(0f, 1f, (t - 0.28f) / 0.44f))
                             : Mathf.Lerp(20f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.72f) / 0.28f)));

            if (move.motionType == AttackMotionType.OverheadSlam)
            {
                _chestBone.rotation = Quaternion.AngleAxis(arc * 0.7f, transform.right) * _chestBone.rotation;
                if (!isTwoHandedWeapon && _rightUpperArm != null)
                    _rightUpperArm.rotation = Quaternion.AngleAxis(arc * 0.9f, transform.right) * _rightUpperArm.rotation;
            }
            else if (move.motionType == AttackMotionType.HorizontalSlash)
            {
                _chestBone.rotation = Quaternion.AngleAxis(-arc * 0.85f, transform.up) * _chestBone.rotation;
                if (!isTwoHandedWeapon && _rightUpperArm != null)
                    _rightUpperArm.rotation = Quaternion.AngleAxis(-arc * 1.1f, transform.up) * _rightUpperArm.rotation;
            }
            else
            {
                float lean = t < 0.65f ? Mathf.Sin((t / 0.65f) * Mathf.PI) * 11f : 0f;
                _chestBone.rotation = Quaternion.AngleAxis(lean, transform.right) * _chestBone.rotation;
            }
        }

        public void AppendBladeHits(LayerMask targetLayers, List<Collider> results)
        {
            if (weaponSocket == null || results == null) return;
            Vector3 rootPos = weaponSocket.position;
            float reach = Mathf.Max(0.55f, weaponLengthY);
            Vector3 tipPos = rootPos + weaponSocket.up * (reach + 0.15f);
            float bladeRadius = weaponArchetype == WeaponType.Hammer || weaponArchetype == WeaponType.Axe ? 0.34f : 0.26f;

            int count = Physics.OverlapCapsuleNonAlloc(rootPos, tipPos, bladeRadius, _bladeSweepCache, targetLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (_bladeSweepCache[i] != null && !results.Contains(_bladeSweepCache[i]))
                    results.Add(_bladeSweepCache[i]);
            }
        }
    }
}
