using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Pórtico minero libre de intersecciones, iluminación/formaciones a lo largo de las cámaras
    /// y corredores variables, Altar Ancestral interactivo y desactivación de triggers automáticos.
    /// </summary>
    public static partial class CaveDungeonBuilder
    {
        private static void BuildEntranceFacadeAndPortal(Transform parent)
        {
            var portalRoot = new GameObject("CaveEntrancePortal").transform;
            portalRoot.SetParent(parent, false);
            portalRoot.localPosition = new Vector3(0f, 0f, CaveEntranceZ);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material woodMat = new Material(lit) { name = "Mat_MineTimber", color = new Color(0.33f, 0.21f, 0.12f, 1f) };
            if (woodMat.HasProperty("_BaseColor")) woodMat.SetColor("_BaseColor", woodMat.color);
            if (woodMat.HasProperty("_Smoothness")) woodMat.SetFloat("_Smoothness", 0.14f);

            // Pórtico dimensionado estrictamente bajo la curva de la bóveda (sin atravesar la roca)
            const float frameZ = -0.32f;
            CreateBeam(portalRoot, "LeftPost", new Vector3(-4.15f, 2.30f, frameZ), new Vector3(0.56f, 4.65f, 0.56f), Quaternion.identity, woodMat, true);
            CreateBeam(portalRoot, "RightPost", new Vector3(4.15f, 2.30f, frameZ), new Vector3(0.56f, 4.65f, 0.56f), Quaternion.identity, woodMat, true);
            CreateBeam(portalRoot, "TopCrossbeam", new Vector3(0f, 4.68f, frameZ), new Vector3(9.30f, 0.58f, 0.66f), Quaternion.identity, woodMat, false);
            CreateBeam(portalRoot, "LeftStrut", new Vector3(-3.15f, 3.85f, frameZ), new Vector3(0.38f, 2.10f, 0.38f), Quaternion.Euler(0f, 0f, -42f), woodMat, false);
            CreateBeam(portalRoot, "RightStrut", new Vector3(3.15f, 3.85f, frameZ), new Vector3(0.38f, 2.10f, 0.38f), Quaternion.Euler(0f, 0f, 42f), woodMat, false);

            CreateWallTorch(portalRoot, new Vector3(-3.75f, 2.45f, frameZ - 0.32f), new Color(1f, 0.64f, 0.24f), 13f, 2.6f);
            CreateWallTorch(portalRoot, new Vector3(3.75f, 2.45f, frameZ - 0.32f), new Color(1f, 0.64f, 0.24f), 13f, 2.6f);
        }

        private static void BuildCaveTorchesAndCrystals(Transform parent)
        {
            var propsRoot = new GameObject("CaveLightingAndFormations").transform;
            propsRoot.SetParent(parent, false);
            Material rockMat = GetCaveRockMaterial();

            float[] torchZ = { 36f, 50f, 64f, 78f, 94f, 112f, 128f, 142f, 158f, 176f, 192f, 206f };
            for (int i = 0; i < torchZ.Length; i++)
            {
                float z = torchZ[i];
                float sx = GetSpineX(z);
                GetWallWidthsAtZ(z, out float lw, out float rw);
                bool left = i % 2 == 0;
                float wx = sx + (left ? -(lw - 1.25f) : (rw - 1.25f));
                Color col = i < 4 ? new Color(1f, 0.58f, 0.22f)
                          : i < 8 ? new Color(0.98f, 0.74f, 0.26f)
                                  : new Color(0.62f, 0.82f, 1.0f);
                CreateWallTorch(propsRoot, new Vector3(wx, GetFloorY(wx, z) + 2.6f, z), col, 18.5f, 2.3f);
            }

            float[] formationZ = { 48f, 62f, 92f, 108f, 116f, 136f, 160f, 174f, 182f, 204f, 218f };
            for (int i = 0; i < formationZ.Length; i++)
            {
                float z = formationZ[i];
                float sx = GetSpineX(z);
                GetWallWidthsAtZ(z, out float lw, out float rw);
                bool left = i % 2 == 0;

                float gx = sx + (left ? -lw * 0.72f : rw * 0.72f);
                SpawnRockFormation(propsRoot, $"Stalagmite_{i}", new Vector3(gx, GetFloorY(gx, z) - 0.15f, z),
                    0.75f + (i % 3) * 0.28f, 2.3f + (i % 4) * 0.75f, false, 600 + i * 19, rockMat, true);

                float cx = sx + (left ? rw * 0.44f : -lw * 0.44f);
                float cy = GetSpineFloorY(z) + GetCeilingHeightAtZ(z) - 0.2f;
                SpawnRockFormation(propsRoot, $"Stalactite_{i}", new Vector3(cx, cy, z + 1.5f),
                    0.55f + (i % 2) * 0.25f, 2.2f + (i % 3) * 0.65f, true, 800 + i * 23, rockMat, false);
            }
        }

        private static void SpawnRockFormation(Transform parent, string name, Vector3 pos, float r, float h, bool inverted, int seed, Material mat, bool addCollider)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (inverted) go.transform.localRotation = Quaternion.Euler(180f, seed * 17f, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(r, h, seed);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (addCollider)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.radius = r * 0.55f;
                cap.height = h * 0.85f;
                cap.center = new Vector3(0f, h * 0.42f, 0f);
            }
        }

        private static void BuildVictorySanctumAltar(Transform parent)
        {
            float altarX = GetSpineX(VictoryAltarZ);
            float altarY = GetSpineFloorY(VictoryAltarZ);

            var altar = new GameObject("VictorySanctumAltar", typeof(BoxCollider), typeof(ConsecrationAltarInteractable)).transform;
            altar.SetParent(parent, false);
            altar.localPosition = new Vector3(altarX, altarY, VictoryAltarZ);

            var col = altar.GetComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.25f, 0f);
            col.size = new Vector3(3.8f, 2.5f, 3.8f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material stoneMat = GetCaveRockMaterial();
            Material goldMat = new Material(lit) { name = "Mat_VictoryAltarGold", color = new Color(1.0f, 0.84f, 0.26f, 1f) };
            if (goldMat.HasProperty("_BaseColor")) goldMat.SetColor("_BaseColor", goldMat.color);
            if (goldMat.HasProperty("_Metallic")) goldMat.SetFloat("_Metallic", 0.92f);
            if (goldMat.HasProperty("_Smoothness")) goldMat.SetFloat("_Smoothness", 0.86f);
            if (goldMat.HasProperty("_EmissionColor")) { goldMat.EnableKeyword("_EMISSION"); goldMat.SetColor("_EmissionColor", new Color(1.0f, 0.72f, 0.16f) * 1.65f); }

            CreateBeam(altar, "AltarStep1", new Vector3(0f, 0.20f, 0f), new Vector3(6.2f, 0.40f, 6.2f), Quaternion.identity, stoneMat, true);
            CreateBeam(altar, "AltarStep2", new Vector3(0f, 0.52f, 0f), new Vector3(4.2f, 0.32f, 4.2f), Quaternion.identity, stoneMat, true);
            CreateBeam(altar, "AltarPedestal", new Vector3(0f, 1.15f, 0f), new Vector3(1.50f, 1.0f, 1.50f), Quaternion.identity, goldMat, true);

            var relic = new GameObject("SacredForgeCrystal", typeof(MeshFilter), typeof(MeshRenderer));
            relic.transform.SetParent(altar, false);
            relic.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            relic.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(0.62f, 1.85f, 777);
            relic.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

            var beaconLight = new GameObject("VictoryBeaconLight", typeof(Light));
            beaconLight.transform.SetParent(altar, false);
            beaconLight.transform.localPosition = new Vector3(0f, 3.6f, 0f);
            var l = beaconLight.GetComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1.0f, 0.85f, 0.36f); l.range = 24f; l.intensity = 4.6f;

            altar.GetComponent<ConsecrationAltarInteractable>().BindAltarVisuals(relic.transform, l, goldMat);
        }

        public static void ConfigureVictoryAndDefeatTriggers(Transform caveRoot)
        {
            DisableLegacyAreaTrigger("AcceptDefeat");
            DisableLegacyAreaTrigger("AcceptVictory");
        }

        private static void DisableLegacyAreaTrigger(string objName)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj == null) return;
            if (obj.TryGetComponent<AreaTrigger>(out var trigger) && trigger.onTriggerEntered != null)
            {
                int persistentCount = trigger.onTriggerEntered.GetPersistentEventCount();
                for (int i = 0; i < persistentCount; i++)
                    trigger.onTriggerEntered.SetPersistentListenerState(i, UnityEventCallState.Off);
                trigger.onTriggerEntered.RemoveAllListeners();
                trigger.enabled = false;
            }
            if (obj.TryGetComponent<Collider>(out var col)) col.enabled = false;
            obj.SetActive(false);
        }

        private static Mesh BuildFlatRockMesh(List<Vector3> verts, List<Vector2> uvs, List<int> tris, string name)
        {
            Vector3[] fv = new Vector3[tris.Count];
            Vector2[] fu = new Vector2[tris.Count];
            int[] ft = new int[tris.Count];
            for (int i = 0; i < tris.Count; i++) { fv[i] = verts[tris[i]]; fu[i] = uvs[tris[i]]; ft[i] = i; }
            Mesh m = new Mesh { name = name };
            if (fv.Length > 65000) m.indexFormat = IndexFormat.UInt32;
            m.vertices = fv; m.uv = fu; m.triangles = ft;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static Material GetCaveRockMaterial()
        {
            if (_caveRockMat != null) return _caveRockMat;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _caveRockMat = new Material(s) { name = "Mat_CaveGraniteRock", color = new Color(0.20f, 0.19f, 0.18f, 1f) };
            if (_caveRockMat.HasProperty("_BaseColor")) _caveRockMat.SetColor("_BaseColor", _caveRockMat.color);
            if (_caveRockMat.HasProperty("_Smoothness")) _caveRockMat.SetFloat("_Smoothness", 0.12f);
            return _caveRockMat;
        }

        public static Material GetCaveFloorMaterial()
        {
            if (_caveFloorMat != null) return _caveFloorMat;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _caveFloorMat = new Material(s) { name = "Mat_CaveStoneFloor", color = new Color(0.16f, 0.15f, 0.14f, 1f) };
            if (_caveFloorMat.HasProperty("_BaseColor")) _caveFloorMat.SetColor("_BaseColor", _caveFloorMat.color);
            if (_caveFloorMat.HasProperty("_Smoothness")) _caveFloorMat.SetFloat("_Smoothness", 0.09f);
            return _caveFloorMat;
        }

        private static void CreateBeam(Transform parent, string name, Vector3 localPos, Vector3 scale, Quaternion rot, Material mat, bool withCollider)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localRotation = rot;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!withCollider && cube.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
        }

        private static void CreateWallTorch(Transform parent, Vector3 localPos, Color lightColor, float range, float intensity)
        {
            var holder = new GameObject("CaveTorch").transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPos;

            var flame = new GameObject("FlameCrystal", typeof(MeshFilter), typeof(MeshRenderer));
            flame.transform.SetParent(holder, false);
            flame.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(0.15f, 0.38f, Mathf.Abs((int)(localPos.z * 13f)));
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(lit) { color = lightColor };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", lightColor);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", lightColor * 2.2f); }
            flame.GetComponent<MeshRenderer>().sharedMaterial = m;

            var lObj = new GameObject("TorchPointLight", typeof(Light));
            lObj.transform.SetParent(holder, false);
            lObj.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var l = lObj.GetComponent<Light>();
            l.type = LightType.Point; l.color = lightColor; l.range = range; l.intensity = intensity;
        }
    }
}
