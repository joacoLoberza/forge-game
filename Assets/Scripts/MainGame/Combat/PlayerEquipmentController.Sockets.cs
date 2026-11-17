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
                rearY = 0f;
                frontY = separation;
            }
            else
            {
                if (stance == WeaponCombatStance.Polearm)
                {
                    rearY = 0.02f;
                    frontY = 0.02f + separation;
                }
                else if (stance == WeaponCombatStance.TwoHanded || (weapon != null && weapon.ingotSize == ItemSize.Large))
                {
                    rearY = 0f;
                    frontY = separation;
                }
                else
                {
                    rearY = 0f;
                    frontY = separation;
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
            bool force2H = weapon.GetCombatStance() == WeaponCombatStance.TwoHanded && weapon.weaponType != WeaponType.Pickaxe;
            string lenTag = weapon.handleLength switch { HandleLength.Short => "Short", HandleLength.Long => "Long", _ => force2H ? "Long" : "Med" };
            string primary = weapon.weaponType switch
            {
                WeaponType.Pickaxe => "Pickaxe_Override",
                WeaponType.Axe => $"Axe{lenTag}_Override",
                WeaponType.Hammer => $"Hammer{lenTag}_Override",
                _ => $"Sword{lenTag}_Override"
            };
            string fallback = weapon.weaponType switch
            {
                WeaponType.Pickaxe => "Pickaxe_Override",
                WeaponType.Axe => lenTag == "Long" ? "Heavy2H_Override" : "Axe1H_Override",
                WeaponType.Hammer => lenTag == "Long" ? "Hammer2H_Override" : "Hammer1H_Override",
                _ => lenTag == "Short" ? "Dagger_Override" : (lenTag == "Long" ? "Heavy2H_Override" : "Sword1H_Override")
            };

            foreach (string resName in new[] { primary, fallback })
            {
                var loaded = UnityEngine.Resources.Load<RuntimeAnimatorController>($"Animations/{resName}");
                if (loaded != null) return loaded;
#if UNITY_EDITOR
                var overrideCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Animations/Character/{resName}.overrideController");
                if (overrideCtrl != null) return overrideCtrl;
#endif
            }
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
            if (capsuleRenderer != null) capsuleRenderer.enabled = false;
        }

        public bool TogglePickaxeAndForgedWeapon()
        {
            var inv = GetComponent<ForjaGame.Inventory.InventoryController>() ?? ForjaGame.Inventory.InventoryController.LocalInstance;
            if (inv == null || inv.Weapons == null) return false;
            bool isPick = _currentEquippedWeapon != null && _currentEquippedWeapon.weaponType == WeaponType.Pickaxe;
            WeaponStats pick = null, forged = null;
            for (int i = 0; i < inv.Weapons.Count; i++)
            {
                var w = inv.Weapons[i];
                if (w == null) continue;
                if (w.weaponType == WeaponType.Pickaxe && pick == null) pick = w;
                else if (w.weaponType != WeaponType.Pickaxe) forged = w;
            }
            WeaponStats target = isPick ? forged : pick;
            if (target == null) return false;
            EquipWeapon(target);
            ForjaGame.Audio.ForgeAudioSystem.PlayItemSelect();
            return true;
        }
    }
}
