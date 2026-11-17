using UnityEngine;

namespace ForjaGame.UI
{
    /// <summary>
    /// Construye una sala de herrería medieval cerrada 3D (paredes, techo, vigas, suelo de piedra),
    /// con los modelos reales del Horno, Prensa y Yunque visibles, fuego y lingote incandescente.
    /// </summary>
    public class MainMenuBackdropBuilder : MonoBehaviour
    {
        public static void EnsureBackdrop()
        {
            if (GameObject.Find("MainMenuAtmosphericBackdrop") != null) return;
            new GameObject("MainMenuAtmosphericBackdrop").AddComponent<MainMenuBackdropBuilder>();
        }

        private void Awake()
        {
            SetupLightingAndFog();
            BuildWorkshopChamber();
            SetupCameraMotion();
        }

        private void SetupLightingAndFog()
        {
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.035f; RenderSettings.fogColor = new Color(0.08f, 0.06f, 0.05f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.20f, 0.14f);
            var dirLight = FindFirstObjectByType<Light>();
            if (dirLight != null && dirLight.type == LightType.Directional)
            {
                dirLight.color = new Color(1.0f, 0.82f, 0.60f); dirLight.intensity = 0.55f;
                dirLight.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            }
        }

        private void BuildWorkshopChamber()
        {
            Material stoneMat = CreateLitMaterial(new Color(0.24f, 0.22f, 0.20f), 0.05f, 0.15f), woodMat = CreateLitMaterial(new Color(0.24f, 0.15f, 0.09f), 0.02f, 0.12f);
            Material ironMat = CreateLitMaterial(new Color(0.22f, 0.23f, 0.25f), 0.65f, 0.35f), hotIngotMat = CreateEmissiveLitMaterial(new Color(1f, 0.32f, 0.04f), new Color(4.5f, 1.1f, 0.15f));

            float floorY = -1.25f, ceilY = 4.8f;
            CreatePrimitive(PrimitiveType.Cube, "Floor", new Vector3(0f, floorY - 0.2f, -4f), new Vector3(20f, 0.4f, 18f), stoneMat);
            CreatePrimitive(PrimitiveType.Cube, "Ceiling", new Vector3(0f, ceilY + 0.2f, -4f), new Vector3(20f, 0.4f, 18f), woodMat);
            CreatePrimitive(PrimitiveType.Cube, "BackWall", new Vector3(0f, (floorY + ceilY) * 0.5f, 1.2f), new Vector3(20f, ceilY - floorY, 0.6f), stoneMat);
            CreatePrimitive(PrimitiveType.Cube, "LeftWall", new Vector3(-8.5f, (floorY + ceilY) * 0.5f, -4f), new Vector3(0.6f, ceilY - floorY, 18f), stoneMat);
            CreatePrimitive(PrimitiveType.Cube, "RightWall", new Vector3(8.5f, (floorY + ceilY) * 0.5f, -4f), new Vector3(0.6f, ceilY - floorY, 18f), stoneMat);

            for (float z = 0f; z >= -8f; z -= 3.5f)
                CreatePrimitive(PrimitiveType.Cube, "Beam", new Vector3(0f, ceilY - 0.2f, z), new Vector3(17f, 0.4f, 0.4f), woodMat);

            CreatePrimitive(PrimitiveType.Cube, "PillarL", new Vector3(-5.2f, 1.8f, -2.5f), new Vector3(1.0f, 6.0f, 1.0f), stoneMat);
            CreatePrimitive(PrimitiveType.Cube, "PillarR", new Vector3(5.2f, 1.8f, -2.5f), new Vector3(1.0f, 6.0f, 1.0f), stoneMat);
            CreateTorch(new Vector3(-5.2f, 1.8f, -2.0f)); CreateTorch(new Vector3(5.2f, 1.8f, -2.0f));

            BuildFurnace(new Vector3(-3.8f, floorY, -4.2f), stoneMat);
            BuildPress(new Vector3(-1.8f, floorY, -2.6f), ironMat);
            BuildAnvil(new Vector3(2.8f, floorY, -3.2f), woodMat, ironMat, hotIngotMat);
        }

        private void BuildFurnace(Vector3 pos, Material stoneMat)
        {
            var prefab = UnityEngine.Resources.Load<GameObject>("Models/horno");
            if (prefab != null)
            {
                var inst = Instantiate(prefab, transform);
                inst.name = "Station_Horno";
                inst.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                ApplyMat(inst, stoneMat);
                PositionModelInWorld(inst, pos, 2.8f, 45f);
            }
            CreateFireAndEmbers(pos + new Vector3(0.35f, 0.85f, 0.35f));
        }

        private void BuildPress(Vector3 pos, Material ironMat)
        {
            var visual = new GameObject("Station_Prensa");
            visual.transform.SetParent(transform, false);

            var sop = UnityEngine.Resources.Load<GameObject>("Models/prensaSoporte");
            var pala = UnityEngine.Resources.Load<GameObject>("Models/prensaPala");
            var ram = UnityEngine.Resources.Load<GameObject>("Models/prensaPrensa");

            if (sop != null) Instantiate(sop, visual.transform).transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            if (pala != null) Instantiate(pala, visual.transform).transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            if (ram != null) Instantiate(ram, visual.transform).transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ApplyMat(visual, ironMat);
            PositionModelInWorld(visual, pos, 2.4f, 25f);

            var lGo = new GameObject("PressLight");
            lGo.transform.SetParent(visual.transform); lGo.transform.position = pos + Vector3.up * 2.2f;
            var l = lGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1.0f, 0.78f, 0.55f); l.range = 5.5f; l.intensity = 1.6f;
        }

        private void BuildAnvil(Vector3 pos, Material wood, Material iron, Material hotIngot)
        {
            float stumpH = 0.58f, anvilH = 0.52f;
            CreatePrimitive(PrimitiveType.Cylinder, "Stump", pos + Vector3.up * (stumpH * 0.5f), new Vector3(1.1f, stumpH * 0.5f, 1.1f), wood);

            var prefab = UnityEngine.Resources.Load<GameObject>("Models/yunque");
            if (prefab != null)
            {
                var inst = Instantiate(prefab, transform);
                inst.name = "Station_Yunque";
                inst.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                ApplyMat(inst, iron);
                PositionModelInWorld(inst, pos, anvilH, -25f, stumpH);
            }

            Vector3 ingotPos = pos + Vector3.up * (stumpH + anvilH + 0.04f);
            CreatePrimitive(PrimitiveType.Cube, "HotIngot", ingotPos, new Vector3(0.52f, 0.10f, 0.20f), hotIngot);

            var iLight = new GameObject("IngotLight");
            iLight.transform.SetParent(transform); iLight.transform.position = ingotPos + Vector3.up * 0.12f;
            var l = iLight.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1.0f, 0.40f, 0.06f); l.range = 4.5f; l.intensity = 2.4f;
        }

        private static void PositionModelInWorld(GameObject go, Vector3 targetPos, float targetH, float yawAngle, float baseOffsetY = 0f)
        {
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one;
            Bounds b = GetCombinedBounds(go);
            if (b.size.y > 0.0001f)
            {
                float factor = targetH / b.size.y;
                go.transform.localScale = Vector3.one * factor;
                b = GetCombinedBounds(go);
                Vector3 dest = new Vector3(targetPos.x, targetPos.y + baseOffsetY, targetPos.z);
                go.transform.position = dest - new Vector3(b.center.x, b.min.y, b.center.z);
                if (Mathf.Abs(yawAngle) > 0.01f)
                    go.transform.RotateAround(dest, Vector3.up, yawAngle);
            }
        }

        private static Bounds GetCombinedBounds(GameObject go)
        {
            Bounds b = default; bool hasB = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasB) { b = r.bounds; hasB = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private void CreateFireAndEmbers(Vector3 firePos)
        {
            var fGo = new GameObject("HearthFire");
            fGo.transform.SetParent(transform); fGo.transform.position = firePos;
            var l = fGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1.0f, 0.54f, 0.12f); l.range = 14f; l.intensity = 3.8f;
            fGo.AddComponent<FireLightFlicker>().baseIntensity = 3.8f;

            var psGo = new GameObject("HearthEmbers");
            psGo.transform.SetParent(transform); psGo.transform.position = firePos;
            var ps = psGo.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.startLifetime = 3.0f; m.startSpeed = 1.1f; m.startSize = 0.07f;
            m.startColor = new Color(1.0f, 0.58f, 0.15f, 0.9f);
            m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 65;
            m.gravityModifier = -0.18f;
            var em = ps.emission; em.rateOverTime = 14f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.3f;
        }

        private void CreateTorch(Vector3 pos)
        {
            var tGo = new GameObject("TorchLight");
            tGo.transform.SetParent(transform); tGo.transform.position = pos;
            var l = tGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1.0f, 0.65f, 0.24f); l.range = 9.5f; l.intensity = 2.2f;
            tGo.AddComponent<FireLightFlicker>().baseIntensity = 2.2f;
        }

        private static void ApplyMat(GameObject go, Material m)
        {
            foreach (var ren in go.GetComponentsInChildren<Renderer>(true)) ren.sharedMaterial = m;
        }

        private void SetupCameraMotion()
        {
            var cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null && cam.GetComponent<MainMenuCameraDrift>() == null)
                cam.gameObject.AddComponent<MainMenuCameraDrift>();
        }

        private GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.position = pos; go.transform.localScale = scale;
            go.transform.SetParent(transform);
            if (mat != null && go.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = mat;
            if (go.TryGetComponent<Collider>(out var col)) Destroy(col);
            return go;
        }

        private static Material CreateLitMaterial(Color color, float metallic, float smoothness)
        {
            var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(s) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static Material CreateEmissiveLitMaterial(Color baseColor, Color emissiveColor)
        {
            var m = CreateLitMaterial(baseColor, 0f, 0.4f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emissiveColor);
            return m;
        }
    }

    public class FireLightFlicker : MonoBehaviour
    {
        public float baseIntensity = 2.5f;
        public float flickerSpeed = 16f;
        private Light _light;
        private void Awake() => _light = GetComponent<Light>();
        private void Update()
        {
            if (_light == null) return;
            _light.intensity = baseIntensity * (0.82f + Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * 0.36f);
        }
    }

    public class MainMenuCameraDrift : MonoBehaviour
    {
        private Vector3 _startPos;
        private Quaternion _startRot;
        private void Awake() { _startPos = transform.position; _startRot = transform.rotation; }
        private void Update()
        {
            float t = Time.time;
            transform.position = _startPos + new Vector3(Mathf.Sin(t * 0.28f) * 0.25f, Mathf.Sin(t * 0.42f) * 0.12f, 0f);
            transform.rotation = _startRot * Quaternion.Euler(Mathf.Sin(t * 0.38f) * 0.6f, Mathf.Sin(t * 0.22f) * 1.1f, 0f);
        }
    }
}
