using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Construye y ensambla el modelo 3D del arma forjada por el jugador en tiempo real.
    /// Combina la hoja metálica deformada y el mango de madera tallado del yunque,
    /// compensando el pivote exactamente en el centro de la empuñadura (Grip Center).
    /// </summary>
    public static class WeaponVisualBuilder
    {
        private static Material _defaultMetalMat;
        private static Material _defaultWoodMat;

        public static GameObject BuildWeaponVisual(WeaponStats weapon, Transform parentSocket, GameObject fallbackPrefab = null)
        {
            if (weapon == null) return null;

            EnsureWeaponMeshes(weapon);

            if (weapon.customMesh != null || weapon.customHandleMesh != null)
            {
                return BuildProceduralWeapon(weapon, parentSocket);
            }

            if (fallbackPrefab != null && parentSocket != null)
            {
                GameObject fallbackInst = Object.Instantiate(fallbackPrefab, parentSocket);
                fallbackInst.name = $"Equipped_{weapon.weaponType}_{weapon.DisplayName}_Fallback";
                fallbackInst.transform.localPosition = Vector3.zero;
                float fallbackYaw = weapon.isFrontFaceDominant ? -90f : 90f;
                fallbackInst.transform.localRotation = Quaternion.Euler(0f, fallbackYaw, 0f);
                fallbackInst.transform.localScale = Vector3.one;
                return fallbackInst;
            }

            return null;
        }

        public static void EnsureWeaponMeshes(WeaponStats weapon)
        {
            if (weapon == null) return;
            if (weapon.customHandleMesh == null)
            {
                weapon.customHandleMesh = PickaxeModelGenerator.GenerateHandleMesh();
                if (weapon.gripCenterOffset == Vector3.zero)
                    weapon.gripCenterOffset = new Vector3(0.12f, 0f, 0f);
            }

            if (weapon.customMesh == null)
            {
                weapon.customMesh = PickaxeModelGenerator.GenerateHeadMesh();
                if (weapon.bladeMountPosition == Vector3.zero && weapon.customHandleMesh != null)
                {
                    float len = Mathf.Max(0.28f, weapon.customHandleMesh.bounds.size.x * 0.90f);
                    weapon.bladeMountPosition = new Vector3(len, 0f, 0f);
                    weapon.bladeMountRotation = weapon.weaponType == WeaponType.Sword ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                }
            }
        }

        private static GameObject BuildProceduralWeapon(WeaponStats weapon, Transform parentSocket)
        {
            GameObject rootGo = new GameObject($"EquippedWeapon_{weapon.weaponType}_{weapon.DisplayName}");
            if (parentSocket != null)
            {
                rootGo.transform.SetParent(parentSocket, false);
                rootGo.transform.localPosition = Vector3.zero;
                rootGo.transform.localRotation = Quaternion.identity;
                rootGo.transform.localScale = Vector3.one;
            }

            GameObject orientationContainer = new GameObject("GripOrientationContainer");
            orientationContainer.transform.SetParent(rootGo.transform, false);
            orientationContainer.transform.localPosition = Vector3.zero;
            orientationContainer.transform.localScale = Vector3.one;

            float yRot = 0f;
            if (weapon.weaponType == WeaponType.Hammer || weapon.weaponType == WeaponType.Pickaxe)
            {
                yRot = 270f;
            }
            else if (weapon.weaponType == WeaponType.Axe)
            {
                yRot = (weapon.isFrontFaceDominant ? 0f : 180f) - 90f;
            }
            else
            {
                yRot = weapon.isFrontFaceDominant ? 0f : 180f;
            }

            orientationContainer.transform.localRotation = Quaternion.Euler(0f, yRot, 90f);

            Vector3 gripPivot = weapon.gripCenterOffset;
            if (gripPivot == Vector3.zero && weapon.customHandleMesh != null)
            {
                gripPivot = new Vector3(0.08f, 0f, 0f);
            }

            if (weapon.customHandleMesh != null)
            {
                GameObject handleGo = new GameObject("CarvedWoodHandle");
                handleGo.transform.SetParent(orientationContainer.transform, false);
                handleGo.transform.localPosition = -gripPivot;
                handleGo.transform.localRotation = Quaternion.identity;
                handleGo.transform.localScale = Vector3.one;

                MeshFilter mf = handleGo.AddComponent<MeshFilter>();
                mf.sharedMesh = weapon.customHandleMesh;

                MeshRenderer mr = handleGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = weapon.customHandleMaterial != null ? weapon.customHandleMaterial : GetDefaultWoodMaterial();
            }

            if (weapon.customMesh != null)
            {
                GameObject bladeGo = new GameObject("ForgedMetalBlade");
                bladeGo.transform.SetParent(orientationContainer.transform, false);

                Vector3 mountPos = weapon.bladeMountPosition;
                if (mountPos == Vector3.zero && weapon.customHandleMesh != null)
                {
                    float handleLength = Mathf.Max(0.20f, weapon.customHandleMesh.bounds.size.x);
                    mountPos = new Vector3(handleLength, 0f, 0f);
                }

                Vector3 bladeOffsetFromGrip = mountPos - gripPivot;
                bladeGo.transform.localPosition = bladeOffsetFromGrip;
                bladeGo.transform.localRotation = weapon.bladeMountRotation;
                bladeGo.transform.localScale = Vector3.one;

                MeshFilter mf = bladeGo.AddComponent<MeshFilter>();
                mf.sharedMesh = weapon.customMesh;

                MeshRenderer mr = bladeGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = weapon.customBladeMaterial != null ? weapon.customBladeMaterial : GetDefaultMetalMaterial();
            }

            return rootGo;
        }

        private static Material GetDefaultMetalMaterial()
        {
            if (_defaultMetalMat != null) return _defaultMetalMat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _defaultMetalMat = new Material(shader) { name = "DefaultMetal_Mat" };
            Color col = new Color(0.68f, 0.70f, 0.74f);
            if (_defaultMetalMat.HasProperty("_BaseColor")) _defaultMetalMat.SetColor("_BaseColor", col);
            _defaultMetalMat.color = col;
            if (_defaultMetalMat.HasProperty("_Metallic")) _defaultMetalMat.SetFloat("_Metallic", 0.90f);
            if (_defaultMetalMat.HasProperty("_Smoothness")) _defaultMetalMat.SetFloat("_Smoothness", 0.65f);
            return _defaultMetalMat;
        }

        private static Material GetDefaultWoodMaterial()
        {
            if (_defaultWoodMat != null) return _defaultWoodMat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _defaultWoodMat = new Material(shader) { name = "DefaultWood_Mat" };
            Color col = new Color(0.48f, 0.32f, 0.18f);
            if (_defaultWoodMat.HasProperty("_BaseColor")) _defaultWoodMat.SetColor("_BaseColor", col);
            _defaultWoodMat.color = col;
            if (_defaultWoodMat.HasProperty("_Metallic")) _defaultWoodMat.SetFloat("_Metallic", 0.05f);
            if (_defaultWoodMat.HasProperty("_Smoothness")) _defaultWoodMat.SetFloat("_Smoothness", 0.30f);
            return _defaultWoodMat;
        }
    }
}
