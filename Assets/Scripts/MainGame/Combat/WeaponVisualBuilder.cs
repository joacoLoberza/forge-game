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

            // Si tiene mallas personalizadas creadas en el yunque, construir el modelo procedural
            if (weapon.customMesh != null || weapon.customHandleMesh != null)
            {
                return BuildProceduralWeapon(weapon, parentSocket);
            }

            // Fallback para armas de prueba o sin mallas de forja
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

            // Contenedor de orientación:
            // En el yunque, el arma se forja longitudinalmente a lo largo del eje +X.
            // Para estándares de empuñadura en mano humanoide, rotamos +X hacia +Y (hacia arriba del puño).
            GameObject orientationContainer = new GameObject("GripOrientationContainer");
            orientationContainer.transform.SetParent(rootGo.transform, false);
            orientationContainer.transform.localPosition = Vector3.zero;
            orientationContainer.transform.localScale = Vector3.one;

            // Orientación de la cara frontal de ataque:
            // - Para Espada: filo afilado mira al frente (+Z)
            // - Para Hacha: filo cortante mira al frente (+Z), compensando los 90° del montaje en el mango
            // - Para Martillo: cara de impacto mira al frente (+Z)
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
                // Empuñadura por defecto a 5 cm del pomo (base del mango)
                gripPivot = new Vector3(0.05f, 0f, 0f);
            }

            // 1. Instanciar y posicionar el mango de madera
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

            // 2. Instanciar y posicionar la hoja / cabeza metálica
            if (weapon.customMesh != null)
            {
                GameObject bladeGo = new GameObject("ForgedMetalBlade");
                bladeGo.transform.SetParent(orientationContainer.transform, false);

                Vector3 mountPos = weapon.bladeMountPosition;
                // Si la posición de montaje no fue asignada (Vector3.zero) y tenemos un mango,
                // montar automáticamente al final del mango (en el extremo opuesto al pomo).
                if (mountPos == Vector3.zero && weapon.customHandleMesh != null)
                {
                    float handleLength = Mathf.Max(0.20f, weapon.customHandleMesh.bounds.size.x);
                    mountPos = new Vector3(handleLength, 0f, 0f);
                }

                // La posición de la hoja respecto al mango compensa el punto de agarre (gripPivot)
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
            Color col = new Color(0.65f, 0.68f, 0.72f);
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
