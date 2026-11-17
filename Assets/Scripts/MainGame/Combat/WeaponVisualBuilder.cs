using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Construye y ensambla el modelo 3D del arma forjada por el jugador en tiempo real (local y multijugador).
    /// Combina la hoja metálica deformada (o cabeza arquetípica escalada por tamaño de lingote) con su
    /// material de aleación real y el mango de madera tallado, sin mostrar placeholders grises.
    /// </summary>
    public static partial class WeaponVisualBuilder
    {
        private static Material _defaultMetalMat;
        private static Material _defaultWoodMat;

        public static GameObject BuildWeaponVisual(WeaponStats weapon, Transform parentSocket, GameObject fallbackPrefab = null)
        {
            if (weapon == null) return null;

            if (weapon.isPrebuiltModel && parentSocket != null)
            {
                GameObject prefab = fallbackPrefab != null ? fallbackPrefab : UnityEngine.Resources.Load<GameObject>("Weapons/Human_Sword");
                if (prefab != null)
                {
                    GameObject prebuiltInst = Object.Instantiate(prefab, parentSocket);
                    prebuiltInst.name = $"Equipped_{weapon.weaponType}_{weapon.DisplayName}_Prebuilt";
                    prebuiltInst.transform.localPosition = Vector3.zero;
                    float fallbackYaw = weapon.isFrontFaceDominant ? -90f : 90f;
                    prebuiltInst.transform.localRotation = Quaternion.Euler(0f, fallbackYaw, 0f);
                    prebuiltInst.transform.localScale = Vector3.one;
                    return prebuiltInst;
                }
            }

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
                weapon.customHandleMesh = GenerateArchetypeHandle(weapon.handleLength, weapon.weaponType);
                if (weapon.gripCenterOffset == Vector3.zero)
                    weapon.gripCenterOffset = GetArchetypeGripOffset(weapon.handleLength, weapon.weaponType);
            }

            if (weapon.customMesh == null)
            {
                Mesh head = GenerateArchetypeHead(weapon.weaponType);
                float sizeMult = weapon.ingotSize switch
                {
                    ItemSize.Small => 0.80f,
                    ItemSize.Large => 1.26f,
                    _ => 1.00f
                };
                if (!Mathf.Approximately(sizeMult, 1.0f) && head != null)
                {
                    Vector3[] v = head.vertices;
                    for (int i = 0; i < v.Length; i++) v[i] *= sizeMult;
                    head.vertices = v;
                    head.RecalculateBounds();
                }
                weapon.customMesh = head;

                if (weapon.bladeMountPosition == Vector3.zero && weapon.customHandleMesh != null)
                {
                    float len = weapon.customHandleMesh.bounds.max.x;
                    weapon.bladeMountPosition = new Vector3(len, 0f, 0f);
                    weapon.bladeMountRotation = Quaternion.identity;
                }
            }

            if (weapon.customBladeMaterial == null)
            {
                weapon.customBladeMaterial = CreateAlloyBladeMaterial(weapon);
            }
        }

        public static Material CreateAlloyBladeMaterial(WeaponStats weapon, Color? explicitAlloyColor = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { name = $"AlloyBlade_{weapon?.weaponType}" };

            Color col = explicitAlloyColor ?? new Color(0.74f, 0.77f, 0.82f);
            if (!explicitAlloyColor.HasValue && weapon?.composition != null && weapon.composition.ingredients != null && weapon.composition.ingredients.Count > 0)
            {
                var wp = weapon.composition.CalculateWeightedProperties();
                col = MetalProperties.GetMetalColor(wp.metalType);
            }

            float qual = weapon != null ? Mathf.Clamp01(weapon.quality) : 0.85f;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            mat.color = col;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Lerp(0.78f, 0.95f, qual));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Lerp(0.52f, 0.84f, qual));
            return mat;
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
            if (weapon.weaponType == WeaponType.Sword)
            {
                yRot = weapon.isFrontFaceDominant ? 0f : 180f;
            }
            else if (weapon.customMesh != null)
            {
                float headZ = (weapon.bladeMountRotation * weapon.customMesh.bounds.center).z + weapon.bladeMountPosition.z;
                if (headZ < -0.01f) yRot = 180f;
            }

            orientationContainer.transform.localRotation = Quaternion.Euler(0f, yRot, 90f);

            Mesh hMesh = weapon.customHandleMesh;
            float hMinX = hMesh != null ? hMesh.bounds.min.x : 0f;
            float hMaxX = hMesh != null ? hMesh.bounds.max.x : 0.65f;
            float hLen = Mathf.Max(0.15f, hMaxX - hMinX);

            Vector3 gripPivot = weapon.gripCenterOffset;
            if (gripPivot == Vector3.zero)
            {
                float gripFrac = weapon.weaponType == WeaponType.Sword ? 0.16f
                    : (weapon.weaponType == WeaponType.Pickaxe ? 0.35f : 0.22f);
                float gripX = (weapon.weaponType == WeaponType.Pickaxe)
                    ? (hMinX + 0.35f)
                    : (hMinX + Mathf.Clamp(hLen * gripFrac, 0.06f, 0.32f));
                gripPivot = new Vector3(gripX, 0f, 0f);
            }

            if (weapon.customHandleMesh != null)
            {
                GameObject handleGo = new GameObject("CarvedWoodHandle", typeof(MeshFilter), typeof(MeshRenderer));
                handleGo.transform.SetParent(orientationContainer.transform, false);
                handleGo.transform.localPosition = -gripPivot;
                handleGo.transform.localRotation = Quaternion.identity;
                handleGo.transform.localScale = Vector3.one;
                handleGo.GetComponent<MeshFilter>().sharedMesh = weapon.customHandleMesh;
                handleGo.GetComponent<MeshRenderer>().sharedMaterial =
                    weapon.customHandleMaterial != null ? weapon.customHandleMaterial : GetDefaultWoodMaterial();
            }

            if (weapon.customMesh != null)
            {
                GameObject bladeGo = new GameObject("ForgedMetalBlade", typeof(MeshFilter), typeof(MeshRenderer));
                bladeGo.transform.SetParent(orientationContainer.transform, false);

                Vector3 headPos;
                Quaternion headRot;
                if (weapon.bladeMountPosition != Vector3.zero)
                {
                    headPos = weapon.bladeMountPosition - gripPivot;
                    headRot = weapon.bladeMountRotation;
                }
                else
                {
                    Bounds bBounds = weapon.customMesh.bounds;
                    Vector3 headOffset;
                    float mountX;
                    if (weapon.weaponType == WeaponType.Sword)
                    {
                        mountX = hMaxX;
                        headOffset = new Vector3(-bBounds.min.x, 0f, 0f);
                    }
                    else
                    {
                        float protrusion = Mathf.Clamp(hLen * 0.05f, 0.025f, 0.05f);
                        mountX = hMaxX - protrusion;
                        headOffset = new Vector3(-bBounds.center.x, 0f, 0f);
                    }
                    headPos = new Vector3(mountX, 0f, 0f) - gripPivot + headOffset;
                    headRot = Quaternion.identity;
                }

                bladeGo.transform.localPosition = headPos;
                bladeGo.transform.localRotation = headRot;
                bladeGo.transform.localScale = Vector3.one;
                bladeGo.GetComponent<MeshFilter>().sharedMesh = weapon.customMesh;
                bladeGo.GetComponent<MeshRenderer>().sharedMaterial =
                    weapon.customBladeMaterial != null ? weapon.customBladeMaterial : CreateAlloyBladeMaterial(weapon);
            }

            return rootGo;
        }

        private static Material GetDefaultMetalMaterial()
        {
            if (_defaultMetalMat != null) return _defaultMetalMat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _defaultMetalMat = new Material(shader) { name = "DefaultMetal_Mat", color = new Color(0.68f, 0.70f, 0.74f) };
            if (_defaultMetalMat.HasProperty("_BaseColor")) _defaultMetalMat.SetColor("_BaseColor", _defaultMetalMat.color);
            if (_defaultMetalMat.HasProperty("_Metallic")) _defaultMetalMat.SetFloat("_Metallic", 0.90f);
            if (_defaultMetalMat.HasProperty("_Smoothness")) _defaultMetalMat.SetFloat("_Smoothness", 0.65f);
            return _defaultMetalMat;
        }

        private static Material GetDefaultWoodMaterial()
        {
            if (_defaultWoodMat != null) return _defaultWoodMat;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _defaultWoodMat = new Material(shader) { name = "DefaultWood_Mat", color = new Color(0.48f, 0.32f, 0.18f) };
            if (_defaultWoodMat.HasProperty("_BaseColor")) _defaultWoodMat.SetColor("_BaseColor", _defaultWoodMat.color);
            if (_defaultWoodMat.HasProperty("_Metallic")) _defaultWoodMat.SetFloat("_Metallic", 0.05f);
            if (_defaultWoodMat.HasProperty("_Smoothness")) _defaultWoodMat.SetFloat("_Smoothness", 0.30f);
            return _defaultWoodMat;
        }
    }
}
