using System;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public partial class ProceduralCombatController
    {
        public void InitializeSkeletonAndPivot()
        {
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (characterAnimator == null) return;

            _hipsBone = characterAnimator.GetBoneTransform(HumanBodyBones.Hips);
            _spineBone = characterAnimator.GetBoneTransform(HumanBodyBones.Spine);
            _chestBone = characterAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? _spineBone;
            _headBone = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
            _rightShoulder = characterAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
            _leftShoulder = characterAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);

            MeasureArmLengths();

            if (_chestBone != null)
            {
                if (_weaponPivot == null)
                {
                    Transform existing = _chestBone.Find("WeaponPivot_Chest");
                    if (existing != null) _weaponPivot = existing;
                    else
                    {
                        GameObject go = new GameObject("WeaponPivot_Chest");
                        go.transform.SetParent(_chestBone, false);
                        _weaponPivot = go.transform;
                    }
                }

                EnsureSockets();
            }

            // Asegurar puente OnAnimatorIK en el GameObject que contiene el Animator
            var bridge = characterAnimator.GetComponent<PlayerIKBridge>() ??
                         characterAnimator.gameObject.AddComponent<PlayerIKBridge>();
            bridge.SetProceduralController(this);
        }

        private void MeasureArmLengths()
        {
            if (characterAnimator == null) return;

            Transform rUpper = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rLower = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);

            if (rUpper != null && rLower != null && rHand != null)
            {
                _rightArmLength = Vector3.Distance(rUpper.position, rLower.position) +
                                  Vector3.Distance(rLower.position, rHand.position);
            }

            Transform lUpper = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lLower = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform lHand = characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (lUpper != null && lLower != null && lHand != null)
            {
                _leftArmLength = Vector3.Distance(lUpper.position, lLower.position) +
                                 Vector3.Distance(lLower.position, lHand.position);
            }
        }

        private void EnsureSockets()
        {
            if (_weaponPivot == null) return;

            _gripSocketR = FindOrCreateChild(_weaponPivot, "GripSocket_R", Vector3.zero, Quaternion.identity);
            _gripSocketL = FindOrCreateChild(_weaponPivot, "GripSocket_L", new Vector3(0f, 0.20f, 0f), Quaternion.identity);
            _bladeBase = FindOrCreateChild(_weaponPivot, "BladeBasePoint", new Vector3(0f, 0.50f, 0f), Quaternion.identity);
            _bladeTip = FindOrCreateChild(_weaponPivot, "BladeTipPoint", new Vector3(0f, 1.20f, 0f), Quaternion.identity);
        }

        private Transform FindOrCreateChild(Transform parent, string childName, Vector3 localPos, Quaternion localRot)
        {
            Transform t = parent.Find(childName);
            if (t == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            t.localPosition = localPos;
            t.localRotation = localRot;
            return t;
        }

        /// <summary>
        /// Equipa un arma forjada calculando su WeaponProfile, posicionando sockets dinámicos
        /// y estableciendo la pose de guardia según el GripMode.
        /// </summary>
        public void EquipWeapon(WeaponStats weapon, GameObject visualInstance)
        {
            InitializeSkeletonAndPivot();

            _currentWeapon = weapon;
            _spawnedWeaponInstance = visualInstance;
            _currentProfile = weapon != null ? weapon.Profile : default;
            _isEquipped = weapon != null;

            if (_weaponPivot != null && visualInstance != null)
            {
                // El arma es hija del WeaponPivot (hijo de Chest)
                visualInstance.transform.SetParent(_weaponPivot, false);
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                visualInstance.transform.localScale = Vector3.one;
            }

            // Calibrar sockets dinámicos desde la geometría del mango
            UpdateSocketsFromProfile(_currentProfile);

            // Cargar la secuencia de combo de 3 golpes predilecta para este perfil
            _currentComboSequence = ProceduralAttackDatabase.GetComboSequence(_currentProfile);

            // Determinar pose de guardia inicial según GripMode
            ConfigureGuardPoseForGrip(_currentProfile.grip);

            ResetToGuard();
            _targetIkWeight = 1.0f;
        }

        /// <summary>
        /// Posiciona GripSocket_R en el pomo y GripSocket_L a una fracción del mango
        /// que escala con h = InverseLerp(minHandle, maxHandle, handleLength).
        /// </summary>
        public void UpdateSocketsFromProfile(WeaponProfile profile)
        {
            if (_gripSocketR == null || _gripSocketL == null) EnsureSockets();

            float handleLen = profile.handleLength > 0.05f ? profile.handleLength : 0.50f;
            float minH = config != null ? config.minHandleLength : 0.15f;
            float maxH = config != null ? config.maxHandleLength : 1.85f;
            float h = Mathf.Clamp01(Mathf.InverseLerp(minH, maxH, handleLen));

            // Mano trasera (Right Hand): alineada en el centro de la empuñadura del pomo
            _gripSocketR.localPosition = Vector3.zero;
            _gripSocketR.localRotation = Quaternion.identity;

            // Mano delantera (Left Hand): separación que escala con h y agarre
            float leftY;
            if (profile.grip == GripMode.Pole)
            {
                leftY = Mathf.Clamp(Mathf.Lerp(0.35f, handleLen * 0.65f, h), 0.35f, handleLen * 0.75f);
            }
            else
            {
                leftY = Mathf.Clamp(0.15f + (handleLen * 0.12f), 0.15f, 0.28f);
            }

            _gripSocketL.localPosition = new Vector3(0f, leftY, 0f);
            _gripSocketL.localRotation = Quaternion.identity;

            // Base y punta de la hoja/cabeza
            if (_bladeBase != null) _bladeBase.localPosition = new Vector3(0f, handleLen, 0f);
            if (_bladeTip != null)
            {
                float totalLength = profile.length > 0.1f ? profile.length : (handleLen + profile.headLength);
                _bladeTip.localPosition = new Vector3(0f, totalLength, 0f);
            }
        }

        private void ConfigureGuardPoseForGrip(GripMode grip)
        {
            if (config != null)
            {
                switch (grip)
                {
                    case GripMode.OneShort:
                        _currentGuardLocalPos = config.guardPosOneShort;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotOneShort);
                        break;
                    case GripMode.OneHand:
                        _currentGuardLocalPos = config.guardPosOneHand;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotOneHand);
                        break;
                    case GripMode.TwoHand:
                        _currentGuardLocalPos = config.guardPosTwoHand;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotTwoHand);
                        break;
                    case GripMode.Pole:
                        _currentGuardLocalPos = config.guardPosPole;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotPole);
                        break;
                }
            }
            else
            {
                switch (grip)
                {
                    case GripMode.OneShort:
                        _currentGuardLocalPos = new Vector3(0.22f, -0.05f, 0.48f);
                        _currentGuardLocalRot = Quaternion.Euler(-12f, 20f, -10f);
                        break;
                    case GripMode.OneHand:
                        _currentGuardLocalPos = new Vector3(0.22f, -0.04f, 0.52f);
                        _currentGuardLocalRot = Quaternion.Euler(-20f, 20f, -12f);
                        break;
                    case GripMode.TwoHand:
                        _currentGuardLocalPos = new Vector3(0.14f, -0.10f, 0.48f);
                        _currentGuardLocalRot = Quaternion.Euler(-24f, 15f, -8f);
                        break;
                    case GripMode.Pole:
                        _currentGuardLocalPos = new Vector3(0.18f, -0.14f, 0.46f);
                        _currentGuardLocalRot = Quaternion.Euler(-32f, 18f, 12f);
                        break;
                }
            }
        }

        public void UnequipWeapon()
        {
            _currentWeapon = null;
            _spawnedWeaponInstance = null;
            _isEquipped = false;
            _targetIkWeight = 0.0f;
            ResetToGuard();
        }
    }
}
