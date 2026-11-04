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
        }

        private RuntimeAnimatorController GetOverrideControllerForWeapon(WeaponStats weapon)
        {
            if (weapon == null) return _baseAnimatorController;
            string resName = "Sword1H_Override";

            var stance = weapon.GetCombatStance();
            if (weapon.weaponType == WeaponType.Pickaxe)
            {
                resName = "Pickaxe_Override";
            }
            else if (weapon.weaponType == WeaponType.Hammer)
            {
                resName = (stance == WeaponCombatStance.TwoHanded || stance == WeaponCombatStance.Polearm)
                    ? "Hammer2H_Override"
                    : "Hammer1H_Override";
            }
            else if (stance == WeaponCombatStance.Polearm)
            {
                resName = "Polearm_Override";
            }
            else if (stance == WeaponCombatStance.TwoHanded)
            {
                resName = "Heavy2H_Override";
            }
            else
            {
                if (weapon.weaponType == WeaponType.Axe)
                    resName = "Axe1H_Override";
                else if (weapon.weaponType == WeaponType.Sword && weapon.handleLength == HandleLength.Short)
                    resName = "Dagger_Override";
                else
                    resName = "Sword1H_Override";
            }

            var loaded = UnityEngine.Resources.Load<RuntimeAnimatorController>($"Animations/{resName}");
            if (loaded != null) return loaded;

#if UNITY_EDITOR
            var overrideCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Animations/Character/{resName}.overrideController");
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
                GameObject prefab = paladinPrefab ?? UnityEngine.Resources.Load<GameObject>("PaladinVisual");
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

                Material paladinMat = UnityEngine.Resources.Load<Material>("Paladin_Mat");
#if UNITY_EDITOR
                if (paladinMat == null)
                {
                    paladinMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Character/Materials/Paladin_Mat.mat");
                }
#endif
                if (paladinMat != null)
                {
                    foreach (var r in paladinGo.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.sharedMaterial == null || r.sharedMaterial.name.StartsWith("Default-") || r.sharedMaterial.name == "Paladin_Mat")
                            r.sharedMaterial = paladinMat;
                    }
                }

                paladinGo.TryGetComponent<Animator>(out _paladinAnimator);
                if (_paladinAnimator != null)
                {
                    _paladinAnimator.applyRootMotion = false;
                    if (_baseAnimatorController == null)
                    {
                        _baseAnimatorController = paladinAnimatorController != null ? paladinAnimatorController : UnityEngine.Resources.Load<RuntimeAnimatorController>("PlayerAnimator");
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
