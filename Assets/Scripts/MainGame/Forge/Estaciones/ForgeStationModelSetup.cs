using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ForjaGame.Forge
{
    /// <summary>
    /// Configura e integra los modelos 3D del Horno, Yunque y Prensa por partes.
    /// </summary>
    public static class ForgeStationModelSetup
    {
        public const string HornoFbxPath = "Assets/Models/horno.fbx";
        public const string YunqueFbxPath = "Assets/Models/yunque.fbx";

        public static void SetupAllModelsInScene(bool forceRebuild = false)
        {
            EnsureStationPositions();
            SetupFurnaceVisual(forceRebuild);
            SetupAnvilVisual(forceRebuild);
            SetupPressVisual(forceRebuild);
            RemoveMineVisual();
        }

        public static void EnsureStationPositions()
        {
            Quaternion workshopFacing = Quaternion.Euler(0f, -90f, 0f);
            var f = Object.FindFirstObjectByType<FurnaceStationController>();
            if (f != null) f.transform.SetPositionAndRotation(new Vector3(-9.60f, f.transform.position.y, 6.20f), workshopFacing);
            var p = Object.FindFirstObjectByType<PressStationController>();
            if (p != null) p.transform.SetPositionAndRotation(new Vector3(-9.60f, p.transform.position.y, 10.00f), workshopFacing);
            var a = Object.FindFirstObjectByType<AnvilStationController>();
            if (a != null) a.transform.SetPositionAndRotation(new Vector3(-9.60f, a.transform.position.y, 13.80f), workshopFacing);
        }

        private static bool ShouldRebuild(Transform parent, string name, bool force)
        {
            int found = 0;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name.StartsWith(name) || child.name.Contains(name) || child.name.StartsWith("Model_") || child.name.StartsWith("AnvilStump"))
                {
                    found++;
                    if (force) { child.name = "_DESTROYED_" + child.name; child.SetParent(null); child.gameObject.SetActive(false); DestroyObj(child.gameObject); }
                }
            }
            return force || found == 0;
        }

        public static void SetupFurnaceVisual(bool forceRebuild = false)
        {
            var furnace = Object.FindFirstObjectByType<FurnaceStationController>();
            if (furnace == null) return;

            if (ShouldRebuild(furnace.transform, "VisualModel_Horno", forceRebuild))
            {
                GameObject prefab = LoadModelAsset(HornoFbxPath);
                if (prefab != null)
                {
                    GameObject visualRoot = new GameObject("VisualModel_Horno");
                    visualRoot.transform.SetParent(furnace.transform, false);
                    visualRoot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 270f, 0f));
                    GameObject model = Object.Instantiate(prefab, visualRoot.transform);
                    model.name = "Model_Horno";
                    model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    ApplyMaterial(model, CreateStationMaterial("Mat_HornoEstacion", new Color(0.46f, 0.42f, 0.38f)));
                    AlignModelToGroundAndCenter(model, furnace.transform, 2.30f);
                }
            }

            var cubeRenderer = furnace.GetComponent<MeshRenderer>();
            if (cubeRenderer != null) cubeRenderer.enabled = false;
            var col = furnace.GetComponent<BoxCollider>();
            if (col != null) { col.center = new Vector3(0f, 0.20f, 0f); col.size = new Vector3(2.2f, 2.40f, 2.2f); }
            var fx = furnace.GetComponent<FurnaceVisualController>();
            if (fx == null) fx = furnace.gameObject.AddComponent<FurnaceVisualController>();
            fx.EnsureBuilt();
        }

        public static void SetupAnvilVisual(bool forceRebuild = false)
        {
            var anvil = Object.FindFirstObjectByType<AnvilStationController>();
            if (anvil == null || !ShouldRebuild(anvil.transform, "VisualModel_Yunque", forceRebuild)) return;
            GameObject prefab = LoadModelAsset(YunqueFbxPath);
            if (prefab == null) return;

            GameObject visualRoot = new GameObject("VisualModel_Yunque");
            visualRoot.transform.SetParent(anvil.transform, false);
            visualRoot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            float groundY = GetGroundHeightAt(anvil.transform.position.x, anvil.transform.position.z) + 0.02f;
            float targetAnvilTop = groundY + 1.08f, anvilHeight = 0.42f;
            float anvilBaseY = targetAnvilTop - anvilHeight, blockHeight = Mathf.Max(0.2f, anvilBaseY - groundY);

            GameObject model = Object.Instantiate(prefab, visualRoot.transform);
            model.name = "Model_Yunque";
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ApplyMaterial(model, CreateStationMaterial("Mat_YunqueEstacion", new Color(0.35f, 0.38f, 0.42f), 0.45f, 0.40f));

            Bounds b = GetCombinedBounds(model.GetComponentsInChildren<Renderer>(true));
            if (b.size.y > 0.01f) model.transform.localScale *= (anvilHeight / b.size.y);
            b = GetCombinedBounds(model.GetComponentsInChildren<Renderer>(true));
            model.transform.position += new Vector3(anvil.transform.position.x - b.center.x, anvilBaseY - b.min.y, anvil.transform.position.z - b.center.z) - anvil.transform.right * 0.135f;
            b = GetCombinedBounds(model.GetComponentsInChildren<Renderer>(true));

            GameObject baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCube.name = "AnvilStump_Block";
            baseCube.transform.SetParent(visualRoot.transform, false);
            var bCol = baseCube.GetComponent<Collider>();
            if (bCol != null) { if (Application.isPlaying) Object.Destroy(bCol); else Object.DestroyImmediate(bCol); }
            baseCube.transform.position = new Vector3(anvil.transform.position.x, (groundY + b.min.y) * 0.5f, anvil.transform.position.z);
            baseCube.transform.localScale = new Vector3(0.68f, blockHeight, 0.68f);
            ApplyMaterial(baseCube, CreateStationMaterial("Mat_AnvilWoodBlock", new Color(0.36f, 0.24f, 0.15f), 0f, 0.20f));

            Transform ingot = anvil.transform.Find("DeformableIngot");
            if (ingot != null) { ingot.position = new Vector3(anvil.transform.position.x, b.max.y + 0.04f, anvil.transform.position.z); ingot.localScale = new Vector3(0.6f, 0.2f, 0.3f); }
            var cubeRenderer = anvil.GetComponent<MeshRenderer>();
            if (cubeRenderer != null) cubeRenderer.enabled = false;
            var col = anvil.GetComponent<BoxCollider>();
            if (col != null) { col.center = new Vector3(0f, ((targetAnvilTop + groundY) * 0.5f) - anvil.transform.position.y, 0f); col.size = new Vector3(1.4f, targetAnvilTop - groundY, 1.8f); }

            if (!anvil.TryGetComponent<AnvilStationVisualController>(out var visual)) visual = anvil.gameObject.AddComponent<AnvilStationVisualController>();
            visual.EnsureBuilt();
        }

        public static void SetupPressVisual(bool forceRebuild = false)
        {
            var press = Object.FindFirstObjectByType<PressStationController>();
            if (press == null) return;

            Transform existingVisual = press.transform.Find("VisualModel_Prensa");
            if (existingVisual != null && !forceRebuild)
            {
                if (FindDeepChild(existingVisual, "Part_Ram") == null || FindDeepChild(existingVisual, "Part_Pala") == null)
                    forceRebuild = true;
                RemovePalancaIfPresent(existingVisual);
            }

            if (ShouldRebuild(press.transform, "VisualModel_Prensa", forceRebuild))
            {
                GameObject soportePrefab = LoadModelAsset("Assets/Models/prensaSoporte.fbx");
                GameObject palaPrefab = LoadModelAsset("Assets/Models/prensaPala.fbx");
                GameObject prensaPrefab = LoadModelAsset("Assets/Models/prensaPrensa.fbx");
                if (soportePrefab == null) return;

                GameObject visualRoot = new GameObject("VisualModel_Prensa");
                visualRoot.transform.SetParent(press.transform, false);
                visualRoot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

                GameObject soporte = Object.Instantiate(soportePrefab, visualRoot.transform);
                soporte.name = "Part_Soporte";
                soporte.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                GameObject pala = palaPrefab != null ? Object.Instantiate(palaPrefab, visualRoot.transform) : null;
                if (pala != null) { pala.name = "Part_Pala"; pala.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); }
                GameObject ram = prensaPrefab != null ? Object.Instantiate(prensaPrefab, visualRoot.transform) : null;
                if (ram != null) { ram.name = "Part_Ram"; ram.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); }

                ApplyMaterial(visualRoot, CreateStationMaterial("Mat_PrensaEstacion", new Color(0.35f, 0.38f, 0.42f), 0.48f, 0.42f));
                AlignModelToGroundAndCenter(visualRoot, press.transform, 2.40f);

                var mech = press.GetComponent<PressMechanicalVisual>();
                if (mech == null) mech = press.gameObject.AddComponent<PressMechanicalVisual>();
                mech.InitializeScrewPress(visualRoot.transform, ram != null ? ram.transform : null, pala != null ? pala.transform : null);
            }
            else if (existingVisual != null)
            {
                var mech = press.GetComponent<PressMechanicalVisual>();
                if (mech == null) mech = press.gameObject.AddComponent<PressMechanicalVisual>();
                mech.InitializeScrewPress(existingVisual, FindDeepChild(existingVisual, "Part_Ram"), FindDeepChild(existingVisual, "Part_Pala"));
            }

            var cubeRenderer = press.GetComponent<MeshRenderer>();
            if (cubeRenderer != null) cubeRenderer.enabled = false;
            var col = press.GetComponent<BoxCollider>();
            if (col != null) { col.center = new Vector3(0f, 0.20f, 0f); col.size = new Vector3(2.2f, 2.50f, 2.0f); }
        }

        private static void RemovePalancaIfPresent(Transform root)
        {
            Transform p = FindDeepChild(root, "Part_Palanca");
            if (p != null) { p.SetParent(null); DestroyObj(p.gameObject); }
        }

        private static void DestroyObj(GameObject go) => ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(go);

        public static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null) return null;
            foreach (var tr in parent.GetComponentsInChildren<Transform>(true))
                if (tr != null && tr.name == name) return tr;
            return null;
        }

        public static void RemoveMineVisual() => DestroyObj(GameObject.Find("MineEntrance_Structure"));

        public static float GetGroundHeightAt(float worldX, float worldZ) =>
            Physics.Raycast(new Vector3(worldX, 20f, worldZ), Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;

        private static void AlignModelToGroundAndCenter(GameObject model, Transform station, float targetHeight)
        {
            var r = model.GetComponentsInChildren<Renderer>(true);
            if (r == null || r.Length == 0) return;
            Bounds b = GetCombinedBounds(r);
            if (b.size.y > 0.01f) model.transform.localScale *= (targetHeight / b.size.y);
            b = GetCombinedBounds(r);
            float gY = GetGroundHeightAt(station.position.x, station.position.z) + 0.02f;
            model.transform.position += new Vector3(station.position.x - b.center.x, gY - b.min.y, station.position.z - b.center.z);
        }

        private static Bounds GetCombinedBounds(Renderer[] r)
        {
            Bounds b = r[0].bounds;
            for (int i = 1; i < r.Length; i++) b.Encapsulate(r[i].bounds);
            return b;
        }

        private static Material CreateStationMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.35f)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = name, color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (metallic > 0f && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static void ApplyMaterial(GameObject root, Material mat)
        {
            if (root == null || mat == null) return;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                int c = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials.Length : 1;
                var mats = new Material[c];
                for (int i = 0; i < c; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        private static GameObject LoadModelAsset(string path)
        {
            var res = UnityEngine.Resources.Load<GameObject>($"Models/{System.IO.Path.GetFileNameWithoutExtension(path)}");
#if UNITY_EDITOR
            if (res == null) res = AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
            return res;
        }
    }
}
