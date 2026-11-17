using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Texturas procedurales con relieve (Normal Map) para diferenciar piso, bóveda y madera minera,
    /// columnas orgánicas en forma de reloj de arena, arcos de túnel y Altar de Consagración.
    /// </summary>
    public static partial class CaveDungeonBuilder
    {
        private static Texture2D _rockTex, _rockNormal, _floorTex;

        private static void BuildEntranceFacadeAndPortal(Transform parent)
        {
            var portalRoot = new GameObject("CaveEntrancePortal").transform;
            portalRoot.SetParent(parent, false);
            portalRoot.localPosition = new Vector3(0f, 0f, CaveEntranceZ);

            Build3DCaveEntranceCollar(portalRoot);

            Material woodMat = GetMineTimberMaterial();
            Material rockMat = GetCaveRockMaterial();
            const float frameZ = -0.32f;
            CreateBeam(portalRoot, "LeftPost", new Vector3(-4.65f, 2.60f, frameZ), new Vector3(0.62f, 5.20f, 0.62f), Quaternion.identity, woodMat, true);
            CreateBeam(portalRoot, "RightPost", new Vector3(4.65f, 2.60f, frameZ), new Vector3(0.62f, 5.20f, 0.62f), Quaternion.identity, woodMat, true);
            CreateBeam(portalRoot, "LeftRockButtress", new Vector3(-5.35f, 1.80f, frameZ + 0.15f), new Vector3(1.80f, 4.20f, 2.20f), Quaternion.Euler(0f, -12f, 8f), rockMat, true);
            CreateBeam(portalRoot, "RightRockButtress", new Vector3(5.35f, 1.80f, frameZ + 0.15f), new Vector3(1.80f, 4.20f, 2.20f), Quaternion.Euler(0f, 12f, -8f), rockMat, true);
            CreateBeam(portalRoot, "TopCrossbeam", new Vector3(0f, 5.25f, frameZ), new Vector3(10.40f, 0.65f, 0.72f), Quaternion.identity, woodMat, false);
            CreateBeam(portalRoot, "LeftStrut", new Vector3(-3.55f, 4.30f, frameZ), new Vector3(0.42f, 2.30f, 0.42f), Quaternion.Euler(0f, 0f, -42f), woodMat, false);
            CreateBeam(portalRoot, "RightStrut", new Vector3(3.55f, 4.30f, frameZ), new Vector3(0.42f, 2.30f, 0.42f), Quaternion.Euler(0f, 0f, 42f), woodMat, false);
        }

        private static void BuildCaveTorchesAndCrystals(Transform parent)
        {
            var propsRoot = new GameObject("CaveLightingAndFormations").transform;
            propsRoot.SetParent(parent, false);
            Material rockMat = GetCaveRockMaterial();
            Material woodMat = GetMineTimberMaterial();

            // Iluminación natural que se derrama desde la boca de la caverna (Z=28 a 38)
            var spillGo = new GameObject("CaveMouthSunlightSpill", typeof(Light));
            spillGo.transform.SetParent(propsRoot, false); spillGo.transform.localPosition = new Vector3(0f, 3.8f, 30.5f);
            var sl = spillGo.GetComponent<Light>();
            sl.type = LightType.Point; sl.color = new Color(0.86f, 0.90f, 1.0f); sl.range = 18f; sl.intensity = 1.65f;

            float[] tunnelArchZ = { 44f, 118f, 212f, 306f, 402f, 496f };
            for (int i = 0; i < tunnelArchZ.Length; i++)
            {
                float z = tunnelArchZ[i], sx = GetSpineX(z), fy = GetFloorY(sx, z);
                GetWallWidthsAtZ(z, out float lw, out float rw);
                float span = Mathf.Min(14.5f, (lw + rw) * 0.70f);
                float h = Mathf.Min(8.5f, GetCeilingHeightAtZ(z) * 0.72f);
                float yaw = Mathf.Atan2(GetSpineX(z + 3f) - GetSpineX(z - 3f), 6f) * Mathf.Rad2Deg;
                Quaternion archRot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 right = archRot * Vector3.right;

                CreateBeam(propsRoot, $"MineArchL_{i}", new Vector3(sx, fy + h * 0.5f, z) - right * (span * 0.5f), new Vector3(0.72f, h, 0.72f), archRot, woodMat, true);
                CreateBeam(propsRoot, $"MineArchR_{i}", new Vector3(sx, fy + h * 0.5f, z) + right * (span * 0.5f), new Vector3(0.72f, h, 0.72f), archRot, woodMat, true);
                CreateBeam(propsRoot, $"MineArchTop_{i}", new Vector3(sx, fy + h, z), new Vector3(span + 1.4f, 0.72f, 0.82f), archRot, woodMat, false);
            }

            float[] colZ = { 64f, 86f, 152f, 176f, 248f, 272f, 344f, 366f, 438f, 462f, 518f };
            for (int i = 0; i < colZ.Length; i++)
            {
                float z = colZ[i], sx = GetSpineX(z);
                GetWallWidthsAtZ(z, out float lw, out float rw);
                float ceilH = GetCeilingHeightAtZ(z);
                float colX = sx + ((i % 2 == 0) ? -lw * 0.34f : rw * 0.34f);
                BuildHourglassColumn(propsRoot, $"HourglassPillar_{i}", colX, z, ceilH, 1.55f + (i % 2) * 0.35f, 900 + i * 31, rockMat);
            }
        }

        private static void BuildHourglassColumn(Transform parent, string name, float wx, float wz, float ceilH, float baseRadius, int seed, Material rockMat)
        {
            float floorY = GetFloorY(wx, wz), ceilingY = GetSpineFloorY(wz) + ceilH;
            float totalH = Mathf.Max(7.5f, ceilingY - floorY + 1.6f);
            SpawnRockFormation(parent, $"{name}_TopStalactite", new Vector3(wx, ceilingY + 0.5f, wz), baseRadius * 1.25f, totalH * 0.66f, true, seed, rockMat, true);
            SpawnRockFormation(parent, $"{name}_BottomStalagmite", new Vector3(wx, floorY - 0.4f, wz), baseRadius * 1.35f, totalH * 0.64f, false, seed + 17, rockMat, true);
        }

        private static void SpawnRockFormation(Transform parent, string name, Vector3 pos, float r, float h, bool inverted, int seed, Material mat, bool addCollider)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            if (inverted) go.transform.localRotation = Quaternion.Euler(180f, seed * 17f, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(r, h, seed);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (addCollider)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.radius = r * 0.46f; cap.height = h * 0.90f; cap.center = new Vector3(0f, h * 0.45f, 0f);
            }
        }

        private static void BuildVictorySanctumAltar(Transform parent)
        {
            float altarX = GetSpineX(VictoryAltarZ), altarY = GetFloorY(altarX, VictoryAltarZ);
            var altar = new GameObject("VictorySanctumAltar", typeof(BoxCollider), typeof(ConsecrationAltarInteractable)).transform;
            altar.SetParent(parent, false); altar.localPosition = new Vector3(altarX, altarY, VictoryAltarZ);

            var col = altar.GetComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.25f, 0f); col.size = new Vector3(4.2f, 2.6f, 4.2f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material stoneMat = GetCaveRockMaterial();
            Material goldMat = new Material(lit) { name = "Mat_VictoryAltarGold", color = new Color(1.0f, 0.84f, 0.26f, 1f) };
            if (goldMat.HasProperty("_BaseColor")) goldMat.SetColor("_BaseColor", goldMat.color);
            if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 0.92f);
            if (goldMat.HasProperty("_Smoothness")) goldMat.SetFloat("_Smoothness", 0.86f);
            if (goldMat.HasProperty("_EmissionColor")) { goldMat.EnableKeyword("_EMISSION"); goldMat.SetColor("_EmissionColor", new Color(1.0f, 0.72f, 0.16f) * 1.85f); }

            CreateBeam(altar, "AltarStep1", new Vector3(0f, 0.20f, 0f), new Vector3(6.8f, 0.40f, 6.8f), Quaternion.identity, stoneMat, true);
            CreateBeam(altar, "AltarStep2", new Vector3(0f, 0.52f, 0f), new Vector3(4.6f, 0.32f, 4.6f), Quaternion.identity, stoneMat, true);
            CreateBeam(altar, "AltarPedestal", new Vector3(0f, 1.15f, 0f), new Vector3(1.60f, 1.0f, 1.60f), Quaternion.identity, goldMat, true);

            var relic = new GameObject("SacredForgeCrystal", typeof(MeshFilter), typeof(MeshRenderer));
            relic.transform.SetParent(altar, false); relic.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            relic.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(0.68f, 2.05f, 777);
            relic.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            var beaconLight = new GameObject("VictoryBeaconLight", typeof(Light));
            beaconLight.transform.SetParent(altar, false); beaconLight.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            var l = beaconLight.GetComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1.0f, 0.85f, 0.36f); l.range = 32f; l.intensity = 5.8f; l.shadows = LightShadows.None;
            altar.GetComponent<ConsecrationAltarInteractable>().BindAltarVisuals(relic.transform, l, goldMat);
        }

        public static void ConfigureVictoryAndDefeatTriggers(Transform caveRoot)
        {
            DisableLegacyAreaTrigger("AcceptDefeat"); DisableLegacyAreaTrigger("AcceptVictory");
        }

        private static void DisableLegacyAreaTrigger(string objName)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj == null) return;
            if (obj.TryGetComponent<AreaTrigger>(out var trigger)) { trigger.onTriggerEntered?.RemoveAllListeners(); trigger.enabled = false; }
            if (obj.TryGetComponent<Collider>(out var col)) col.enabled = false;
            obj.SetActive(false);
        }

        private static Mesh BuildFlatRockMesh(List<Vector3> verts, List<Vector2> uvs, List<int> tris, string name)
        {
            Vector3[] fv = new Vector3[tris.Count]; Vector2[] fu = new Vector2[tris.Count]; int[] ft = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++) { fv[i] = verts[tris[i]]; fu[i] = uvs[tris[i]]; ft[i] = i; }
            Mesh m = new Mesh { name = name };
            if (fv.Length > 65000) m.indexFormat = IndexFormat.UInt32;
            m.vertices = fv; m.uv = fu; m.triangles = ft; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static void EnsureProceduralRockTextures()
        {
            if (_rockTex != null && _floorTex != null && _rockNormal != null) return;
            const int S = 64;
            _rockTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            _floorTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            _rockNormal = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float strata = Mathf.PerlinNoise(x * 0.08f + 3f, y * 0.24f + 7f) * 0.65f + Mathf.PerlinNoise(x * 0.22f, y * 0.22f) * 0.35f;
                    float pebble = Mathf.PerlinNoise(x * 0.18f + 11f, y * 0.18f + 19f);
                    _rockTex.SetPixel(x, y, Color.Lerp(new Color(0.13f, 0.14f, 0.16f), new Color(0.26f, 0.27f, 0.30f), strata));
                    _floorTex.SetPixel(x, y, Color.Lerp(new Color(0.20f, 0.16f, 0.12f), new Color(0.34f, 0.28f, 0.21f), pebble));
                    float dx = (Mathf.PerlinNoise((x + 1) * 0.18f, y * 0.18f) - strata) * 2.2f;
                    float dy = (Mathf.PerlinNoise(x * 0.18f, (y + 1) * 0.18f) - strata) * 2.2f;
                    _rockNormal.SetPixel(x, y, new Color(0.5f + dx * 0.5f, 0.5f + dy * 0.5f, 1f, 1f));
                }
            }
            _rockTex.Apply(); _floorTex.Apply(); _rockNormal.Apply();
        }

        public static Material GetCaveRockMaterial()
        {
            if (_caveRockMat != null) return _caveRockMat;
            EnsureProceduralRockTextures();
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _caveRockMat = new Material(s) { name = "Mat_CaveGraniteRock", color = Color.white };
            if (_caveRockMat.HasProperty("_BaseMap")) _caveRockMat.SetTexture("_BaseMap", _rockTex);
            if (_caveRockMat.HasProperty("_MainTex")) _caveRockMat.SetTexture("_MainTex", _rockTex);
            if (_caveRockMat.HasProperty("_BumpMap")) { _caveRockMat.SetTexture("_BumpMap", _rockNormal); _caveRockMat.EnableKeyword("_NORMALMAP"); }
            if (_caveRockMat.HasProperty("_Smoothness")) _caveRockMat.SetFloat("_Smoothness", 0.16f);
            return _caveRockMat;
        }

        public static Material GetCaveFloorMaterial()
        {
            if (_caveFloorMat != null) return _caveFloorMat;
            EnsureProceduralRockTextures();
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _caveFloorMat = new Material(s) { name = "Mat_CaveStoneFloor", color = Color.white };
            if (_caveFloorMat.HasProperty("_BaseMap")) _caveFloorMat.SetTexture("_BaseMap", _floorTex);
            if (_caveFloorMat.HasProperty("_MainTex")) _caveFloorMat.SetTexture("_MainTex", _floorTex);
            if (_caveFloorMat.HasProperty("_BumpMap")) { _caveFloorMat.SetTexture("_BumpMap", _rockNormal); _caveFloorMat.EnableKeyword("_NORMALMAP"); }
            if (_caveFloorMat.HasProperty("_Smoothness")) _caveFloorMat.SetFloat("_Smoothness", 0.12f);
            return _caveFloorMat;
        }

        public static Material GetMineTimberMaterial()
        {
            if (_mineTimberMat != null) return _mineTimberMat;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _mineTimberMat = new Material(s) { name = "Mat_MineTimber", color = new Color(0.36f, 0.23f, 0.13f, 1f) };
            if (_mineTimberMat.HasProperty("_BaseColor")) _mineTimberMat.SetColor("_BaseColor", _mineTimberMat.color);
            if (_mineTimberMat.HasProperty("_Smoothness")) _mineTimberMat.SetFloat("_Smoothness", 0.14f);
            return _mineTimberMat;
        }

        private static void CreateBeam(Transform parent, string name, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat, bool withCollider)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos; cube.transform.localRotation = rot; cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!withCollider && cube.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
        }
    }
}
