using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Construye la estructura rústica perpendicular del Taller de Herrería al Oeste (X = -13.8 a -5.2, Z = 3.4 a 16.6),
    /// abierta hacia el Este (+X) rumbo al sendero central y con techo de tablones 100% completo sin huecos.
    /// </summary>
    public partial class GreenPlateauTerrainController
    {
        private const string WorkshopStructureTag = "RusticWorkshopCanopy_V3";

        private static void EnsureRusticWorkshopStructure(Transform workshopFloor)
        {
            if (workshopFloor == null) return;
            if (workshopFloor.Find(WorkshopStructureTag) != null) return;

            for (int i = workshopFloor.childCount - 1; i >= 0; i--)
                UnitySafeUtility.DestroyGameObjectSafe(workshopFloor.GetChild(i).gameObject);

            var root = new GameObject(WorkshopStructureTag).transform;
            root.SetParent(workshopFloor, false);
            root.localPosition = Vector3.zero;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material woodMat = MakeLitMat(lit, "Mat_WorkshopTimber", new Color(0.30f, 0.19f, 0.11f), 0.05f, 0.16f);
            Material darkWoodMat = MakeLitMat(lit, "Mat_WorkshopRoofPlank", new Color(0.23f, 0.15f, 0.09f), 0.04f, 0.14f);
            Material ironMat = MakeLitMat(lit, "Mat_WorkshopIron", new Color(0.18f, 0.19f, 0.21f), 0.82f, 0.35f);
            Material stoneWallMat = GetOrCreateStoneFloorMaterial();
            Material coalMat = MakeLitMat(lit, "Mat_WorkshopCoal", new Color(0.09f, 0.09f, 0.10f), 0.10f, 0.12f);

            // 1. Muretes bajos de mampostería trasera (Oeste) y laterales (Norte/Sur); frente Este (+X) 100% abierto
            MakeBox(root, "StoneWall_BackWest", new Vector3(-13.75f, 0.50f, 10.0f), new Vector3(0.44f, 0.90f, 13.1f), Quaternion.identity, stoneWallMat, true);
            MakeBox(root, "StoneWall_South", new Vector3(-10.8f, 0.46f, 3.45f), new Vector3(6.2f, 0.82f, 0.40f), Quaternion.identity, stoneWallMat, true);
            MakeBox(root, "StoneWall_North", new Vector3(-10.8f, 0.46f, 16.55f), new Vector3(6.2f, 0.82f, 0.40f), Quaternion.identity, stoneWallMat, true);

            // 2. Pilares maestros de roble (Frente Este X=-5.55 alto Y=4.38m; Fondo Oeste X=-13.45 bajo Y=3.62m)
            float[] postZ = { 3.75f, 10.0f, 16.25f };
            for (int i = 0; i < postZ.Length; i++)
            {
                if (i != 1) // El centro del frente Este queda despejado de columnas
                    MakeBox(root, $"FrontPost_{i}", new Vector3(-5.55f, 2.24f, postZ[i]), new Vector3(0.32f, 4.38f, 0.32f), Quaternion.identity, woodMat, true);

                MakeBox(root, $"BackPost_{i}", new Vector3(-13.45f, 1.86f, postZ[i]), new Vector3(0.32f, 3.62f, 0.32f), Quaternion.identity, woodMat, true);
            }

            // 3. Vigas maestras longitudinales (eje Z) y cabrios inclinados (eje X)
            MakeBox(root, "FrontHeaderBeam", new Vector3(-5.55f, 4.38f, 10.0f), new Vector3(0.28f, 0.28f, 13.2f), Quaternion.identity, woodMat, false);
            MakeBox(root, "BackHeaderBeam", new Vector3(-13.45f, 3.62f, 10.0f), new Vector3(0.28f, 0.28f, 13.2f), Quaternion.identity, woodMat, false);

            Quaternion roofPitch = Quaternion.Euler(0f, 0f, 5.5f);
            for (int i = 0; i < 7; i++)
            {
                float rz = Mathf.Lerp(3.75f, 16.25f, i / 6f);
                MakeBox(root, $"Rafter_{i}", new Vector3(-9.50f, 4.00f, rz), new Vector3(8.55f, 0.18f, 0.16f), roofPitch, woodMat, false);
            }

            MakeBox(root, "Brace_FS", new Vector3(-5.55f, 3.78f, 4.35f), new Vector3(0.18f, 1.35f, 0.18f), Quaternion.Euler(45f, 0f, 0f), woodMat, false);
            MakeBox(root, "Brace_FN", new Vector3(-5.55f, 3.78f, 15.65f), new Vector3(0.18f, 1.35f, 0.18f), Quaternion.Euler(-45f, 0f, 0f), woodMat, false);

            // 4. Techo de tablones 100% completo de borde a borde (13 hiladas solapadas sin huecos)
            const int plankRows = 13;
            for (int i = 0; i < plankRows; i++)
            {
                float t = i / (float)(plankRows - 1);
                float px = Mathf.Lerp(-13.55f, -5.45f, t);
                float py = Mathf.Lerp(3.61f, 4.39f, t) + 0.12f;
                Color tint = (i % 2 == 0) ? new Color(0.24f, 0.15f, 0.10f) : new Color(0.21f, 0.13f, 0.08f);
                Material rowMat = i % 2 == 0 ? darkWoodMat : MakeLitMat(lit, $"Mat_RoofPlankAlt", tint, 0.04f, 0.14f);
                MakeBox(root, $"CanopyPlank_{i}", new Vector3(px, py, 10.0f), new Vector3(0.71f, 0.06f, 13.35f), roofPitch, rowMat, false);
            }

            // Campana y ducto de hierro forjado para evacuar el humo del Horno a través del techo completo
            MakeBox(root, "ChimneyFlueHood", new Vector3(-9.60f, 3.45f, 6.20f), new Vector3(1.15f, 0.95f, 1.15f), Quaternion.identity, ironMat, false);
            MakeBox(root, "ChimneyRoofStack", new Vector3(-9.60f, 4.45f, 6.20f), new Vector3(0.68f, 1.25f, 0.68f), Quaternion.identity, ironMat, false);

            // 5. Mobiliario rústico de herrería alineado al muro trasero (orientado hacia +X)
            BuildFuelStockpileProp(root, new Vector3(-12.75f, 0.075f, 4.75f), woodMat, coalMat);
            BuildWorkshopBrazier(root, new Vector3(-12.55f, 0.075f, 10.0f), ironMat);
            BuildToolRackProp(root, new Vector3(-12.85f, 0.075f, 15.15f), woodMat, ironMat);
        }

        private static void BuildToolRackProp(Transform parent, Vector3 pos, Material woodMat, Material ironMat)
        {
            var rack = new GameObject("BlacksmithToolRack").transform;
            rack.SetParent(parent, false);
            rack.localPosition = pos;
            rack.localRotation = Quaternion.Euler(0f, 90f, 0f);

            MakeBox(rack, "LegL", new Vector3(-0.65f, 0.90f, 0f), new Vector3(0.12f, 1.8f, 0.12f), Quaternion.Euler(6f, 0f, 0f), woodMat, true);
            MakeBox(rack, "LegR", new Vector3(0.65f, 0.90f, 0f), new Vector3(0.12f, 1.8f, 0.12f), Quaternion.Euler(6f, 0f, 0f), woodMat, true);
            MakeBox(rack, "TopBar", new Vector3(0f, 1.55f, -0.05f), new Vector3(1.50f, 0.10f, 0.08f), Quaternion.identity, woodMat, false);
            MakeBox(rack, "MidBar", new Vector3(0f, 0.95f, 0.02f), new Vector3(1.50f, 0.10f, 0.08f), Quaternion.identity, woodMat, false);
            MakeBox(rack, "HangingTongs_A", new Vector3(-0.38f, 1.25f, -0.10f), new Vector3(0.04f, 0.62f, 0.03f), Quaternion.Euler(0f, 0f, 6f), ironMat, false);
            MakeBox(rack, "HangingTongs_B", new Vector3(-0.35f, 1.25f, -0.10f), new Vector3(0.04f, 0.62f, 0.03f), Quaternion.Euler(0f, 0f, -6f), ironMat, false);
            MakeBox(rack, "SpareHammerShaft", new Vector3(0.05f, 1.20f, -0.10f), new Vector3(0.05f, 0.68f, 0.05f), Quaternion.identity, woodMat, false);
            MakeBox(rack, "SpareHammerHead", new Vector3(0.05f, 0.90f, -0.10f), new Vector3(0.22f, 0.12f, 0.12f), Quaternion.identity, ironMat, false);
            MakeBox(rack, "TemplateBlade", new Vector3(0.42f, 1.18f, -0.10f), new Vector3(0.09f, 0.72f, 0.02f), Quaternion.identity, ironMat, false);
        }

        private static void BuildFuelStockpileProp(Transform parent, Vector3 pos, Material woodMat, Material coalMat)
        {
            var bin = new GameObject("FuelAndTimberBin").transform;
            bin.SetParent(parent, false);
            bin.localPosition = pos;
            bin.localRotation = Quaternion.Euler(0f, 90f, 0f);

            MakeBox(bin, "CoalCrate", new Vector3(-0.35f, 0.32f, 0f), new Vector3(0.95f, 0.64f, 0.85f), Quaternion.identity, woodMat, true);
            MakeBox(bin, "CoalMound", new Vector3(-0.35f, 0.66f, 0f), new Vector3(0.80f, 0.22f, 0.70f), Quaternion.Euler(4f, 18f, -3f), coalMat, false);

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3 - r; c++)
                {
                    var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    log.name = $"Firewood_{r}_{c}";
                    if (log.TryGetComponent<Collider>(out var col)) UnitySafeUtility.DestroySafe(col);
                    log.transform.SetParent(bin, false);
                    log.transform.localPosition = new Vector3(0.48f + c * 0.20f + r * 0.10f, 0.12f + r * 0.17f, 0f);
                    log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    log.transform.localScale = new Vector3(0.18f, 0.42f, 0.18f);
                    log.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
                }
            }
        }

        private static void BuildWorkshopBrazier(Transform parent, Vector3 pos, Material ironMat)
        {
            var br = new GameObject("WorkshopWarmBrazier").transform;
            br.SetParent(parent, false);
            br.localPosition = pos;

            MakeBox(br, "Stand", new Vector3(0f, 0.48f, 0f), new Vector3(0.22f, 0.96f, 0.22f), Quaternion.identity, ironMat, true);
            MakeBox(br, "Bowl", new Vector3(0f, 0.98f, 0f), new Vector3(0.62f, 0.24f, 0.62f), Quaternion.Euler(0f, 45f, 0f), ironMat, false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material emberMat = MakeLitMat(lit, "Mat_BrazierEmber", new Color(1f, 0.42f, 0.08f), 0f, 0.2f);
            if (emberMat.HasProperty("_EmissionColor")) { emberMat.EnableKeyword("_EMISSION"); emberMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.10f) * 2.4f); }
            MakeBox(br, "GlowingCoals", new Vector3(0f, 1.09f, 0f), new Vector3(0.46f, 0.12f, 0.46f), Quaternion.Euler(0f, 20f, 0f), emberMat, false);

            var lightGo = new GameObject("WorkshopHearthLight", typeof(Light));
            lightGo.transform.SetParent(br, false);
            lightGo.transform.localPosition = new Vector3(0.35f, 1.65f, 0f);
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1.0f, 0.64f, 0.28f); l.range = 16.5f; l.intensity = 2.5f;
        }

        private static Material MakeLitMat(Shader s, string name, Color col, float metallic, float smooth)
        {
            var m = new Material(s) { name = name, color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        private static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider && go.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
        }
    }
}
