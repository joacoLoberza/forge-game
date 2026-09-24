using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public partial class PlayerEquipmentController
    {
        private void SetupGripGauntlets(WeaponStats weapon, Transform weaponTransform, float separation)
        {
            float rearY, frontY;
            var stance = weapon != null ? weapon.GetCombatStance() : WeaponCombatStance.OneHanded;

            if (weapon != null && (weapon.customMesh != null || weapon.customHandleMesh != null))
            {
                rearY = -separation * 0.5f;
                frontY = separation * 0.5f;
            }
            else
            {
                if (stance == WeaponCombatStance.Polearm)
                {
                    rearY = 0.05f;
                    frontY = 0.05f + separation;
                }
                else if (stance == WeaponCombatStance.TwoHanded || (weapon != null && weapon.ingotSize == ItemSize.Large))
                {
                    rearY = -0.24f;
                    frontY = -0.02f;
                }
                else
                {
                    rearY = -0.16f;
                    frontY = -0.06f;
                }
            }

            // Socket trasero (mano dominante en el pomo)
            Transform gripRear = new GameObject("GripSocket_Rear").transform;
            gripRear.SetParent(weaponTransform, false);
            gripRear.localPosition = new Vector3(0f, rearY, 0f);
            gripRear.localRotation = Quaternion.identity;

            // Socket delantero (mano de apoyo)
            Transform gripFront = new GameObject("GripSocket_Front").transform;
            gripFront.SetParent(weaponTransform, false);
            gripFront.localPosition = new Vector3(0f, frontY, 0f);
            gripFront.localRotation = Quaternion.identity;

            _gripSocketRear = gripRear;
            _gripSocketFront = gripFront;

            // Conectar sockets al controlador de IK del Paladín
            PlayerCombatIKDriver.Instance?.SetWeaponEquipped(true, _weaponHolder, gripRear, gripFront, stance, weapon);
        }

        private RuntimeAnimatorController GetOverrideControllerForWeapon(WeaponStats weapon)
        {
            if (weapon == null) return _baseAnimatorController;
            string overridePath = "Assets/Animations/Character/Sword1H_Override.overrideController";

            var stance = weapon.GetCombatStance();
            if (stance == WeaponCombatStance.Polearm)
            {
                overridePath = "Assets/Animations/Character/Polearm_Override.overrideController";
            }
            else if (stance == WeaponCombatStance.TwoHanded)
            {
                overridePath = (weapon.weaponType == WeaponType.Hammer)
                    ? "Assets/Animations/Character/Hammer2H_Override.overrideController"
                    : "Assets/Animations/Character/Heavy2H_Override.overrideController";
            }
            else
            {
                if (weapon.weaponType == WeaponType.Hammer)
                    overridePath = "Assets/Animations/Character/Hammer1H_Override.overrideController";
                else if (weapon.weaponType == WeaponType.Axe)
                    overridePath = "Assets/Animations/Character/Axe1H_Override.overrideController";
                else
                    overridePath = "Assets/Animations/Character/Sword1H_Override.overrideController";
            }

#if UNITY_EDITOR
            var overrideCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(overridePath);
            if (overrideCtrl != null) return overrideCtrl;
#endif
            return _baseAnimatorController;
        }

        public void EnsurePaladinVisual()
        {
            Transform existing = transform.Find("PaladinVisual");
            GameObject paladinGo = existing != null ? existing.gameObject : null;

            if (paladinGo == null)
            {
                GameObject prefab = paladinPrefab;
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Character/character.fbx");
                }
#endif
                if (prefab != null)
                {
                    paladinGo = Instantiate(prefab, transform);
                    paladinGo.name = "PaladinVisual";
                }
            }

            if (paladinGo != null)
            {
                paladinGo.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                paladinGo.transform.localRotation = Quaternion.identity;
                paladinGo.transform.localScale = Vector3.one;

                _paladinAnimator = paladinGo.GetComponent<Animator>();
                if (_paladinAnimator != null)
                {
                    _paladinAnimator.applyRootMotion = false;
                    if (_baseAnimatorController == null)
                    {
                        _baseAnimatorController = paladinAnimatorController;
#if UNITY_EDITOR
                        if (_baseAnimatorController == null)
                        {
                            _baseAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Character/PlayerAnimator.controller");
                        }
#endif
                    }
                    if (_paladinAnimator.runtimeAnimatorController == null && _baseAnimatorController != null)
                    {
                        _paladinAnimator.runtimeAnimatorController = _baseAnimatorController;
                    }
                }

                if (paladinGo.GetComponent<PlayerCombatIKDriver>() == null)
                {
                    paladinGo.AddComponent<PlayerCombatIKDriver>();
                }

                var movement = GetComponent<ForjaGame.Core.PlayerMovement>();
                if (movement != null && _paladinAnimator != null)
                {
                    movement.SetAnimator(_paladinAnimator);
                }
            }

            var capsuleRenderer = GetComponent<MeshRenderer>();
            if (capsuleRenderer != null)
            {
                capsuleRenderer.enabled = false;
            }
        }
    }
}
