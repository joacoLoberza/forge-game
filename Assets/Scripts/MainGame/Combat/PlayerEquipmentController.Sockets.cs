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

        /// <summary>
        /// Construye un guantelete de forja profesional y estilizado compuesto por cuero y placas de acero.
        /// </summary>
        private GameObject CreateArmoredGauntlet(string gauntletName, Transform parentSocket, bool isFrontGrip)
        {
            GameObject gauntletRoot = new GameObject(gauntletName);
            gauntletRoot.transform.SetParent(parentSocket, false);
            gauntletRoot.transform.localPosition = Vector3.zero;
            gauntletRoot.transform.localRotation = Quaternion.identity;
            gauntletRoot.transform.localScale = Vector3.one * gauntletScale;

            Material metalMat = GetGauntletMetalMaterial();
            Material leatherMat = GetGauntletLeatherMaterial();

            // 1. Núcleo del puño / agarre de cuero (cilindro curvado alrededor del mango)
            GameObject palm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            palm.name = "LeatherGripCore";
            palm.transform.SetParent(gauntletRoot.transform, false);
            palm.transform.localPosition = new Vector3(0f, 0f, 0f);
            palm.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            palm.transform.localScale = new Vector3(0.085f, 0.055f, 0.085f);
            StripCollider(palm);
            SetMaterial(palm, leatherMat);

            // 2. Placa dorsal metálica reforzada (nudillos y dorso de la mano)
            GameObject backPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backPlate.name = "ArmorBackPlate";
            backPlate.transform.SetParent(gauntletRoot.transform, false);
            float sideOffset = isFrontGrip ? -0.045f : 0.045f;
            backPlate.transform.localPosition = new Vector3(sideOffset, 0f, 0.02f);
            backPlate.transform.localRotation = Quaternion.Euler(0f, isFrontGrip ? -20f : 20f, 0f);
            backPlate.transform.localScale = new Vector3(0.065f, 0.095f, 0.035f);
            StripCollider(backPlate);
            SetMaterial(backPlate, metalMat);

            // 3. Protector de nudillos biselado
            GameObject knuckles = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knuckles.name = "ArmorKnuckles";
            knuckles.transform.SetParent(gauntletRoot.transform, false);
            knuckles.transform.localPosition = new Vector3(sideOffset * 0.9f, 0.025f, 0.038f);
            knuckles.transform.localRotation = Quaternion.identity;
            knuckles.transform.localScale = new Vector3(0.055f, 0.040f, 0.050f);
            StripCollider(knuckles);
            SetMaterial(knuckles, metalMat);

            // 4. Muñequera / Brazalete de forja (Vambrace cuff)
            GameObject cuff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuff.name = "WristVambrace";
            cuff.transform.SetParent(gauntletRoot.transform, false);
            cuff.transform.localPosition = new Vector3(0f, -0.045f, 0f);
            cuff.transform.localRotation = Quaternion.identity;
            cuff.transform.localScale = new Vector3(0.095f, 0.015f, 0.095f);
            StripCollider(cuff);
            SetMaterial(cuff, metalMat);

            return gauntletRoot;
        }

        private void StripCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void SetMaterial(GameObject go, Material mat)
        {
            Renderer ren = go.GetComponent<Renderer>();
            if (ren != null) ren.sharedMaterial = mat;
        }

        private Material GetGauntletMetalMaterial()
        {
            if (customGauntletMetalMaterial != null) return customGauntletMetalMaterial;
            if (_metalMaterialInstance != null) return _metalMaterialInstance;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _metalMaterialInstance = new Material(shader) { name = "Gauntlet_Metal_Mat" };
            if (_metalMaterialInstance.HasProperty("_BaseColor")) _metalMaterialInstance.SetColor("_BaseColor", metalPlateColor);
            _metalMaterialInstance.color = metalPlateColor;
            if (_metalMaterialInstance.HasProperty("_Metallic")) _metalMaterialInstance.SetFloat("_Metallic", 0.85f);
            if (_metalMaterialInstance.HasProperty("_Smoothness")) _metalMaterialInstance.SetFloat("_Smoothness", 0.60f);
            return _metalMaterialInstance;
        }

        private Material GetGauntletLeatherMaterial()
        {
            if (customGauntletLeatherMaterial != null) return customGauntletLeatherMaterial;
            if (_leatherMaterialInstance != null) return _leatherMaterialInstance;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _leatherMaterialInstance = new Material(shader) { name = "Gauntlet_Leather_Mat" };
            if (_leatherMaterialInstance.HasProperty("_BaseColor")) _leatherMaterialInstance.SetColor("_BaseColor", leatherUnderColor);
            _leatherMaterialInstance.color = leatherUnderColor;
            if (_leatherMaterialInstance.HasProperty("_Metallic")) _leatherMaterialInstance.SetFloat("_Metallic", 0.05f);
            if (_leatherMaterialInstance.HasProperty("_Smoothness")) _leatherMaterialInstance.SetFloat("_Smoothness", 0.28f);
            return _leatherMaterialInstance;
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
