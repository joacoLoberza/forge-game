using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Resources;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Generador procedural de las 3 variantes de paredes destruibles para los túneles:
    /// 1) Tela de araña (Filamentos tensados y capullos de seda)
    /// 2) Maderas (Tablones mineros superpuestos y vigas en X)
    /// 3) Ladrillos rotos (Muro de mampostería de roca fracturada y escombros)
    /// </summary>
    public static partial class DestructibleTunnelWallBuilder
    {
        private static Material _webMat;
        private static Texture2D _webSilkTex;

        public static GameObject Create(Transform parent, string name, Vector3 pos, Quaternion rot, float span, float height, TunnelWallType type, int seed)
        {
            var wallObj = new GameObject(name);
            wallObj.transform.SetParent(parent, false);
            wallObj.transform.SetPositionAndRotation(pos, rot);

            var col = wallObj.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height * 0.45f, 0f);
            col.size = new Vector3(span + 2.0f, height + 2.0f, type == TunnelWallType.BrokenBricks ? 3.2f : 2.4f);

            var wallCtrl = wallObj.AddComponent<DestructibleTunnelWall>();
            int hp = type switch { TunnelWallType.Web => 160, TunnelWallType.WoodPlanks => 240, _ => 300 };
            wallCtrl.Initialize(type, hp, col);

            switch (type)
            {
                case TunnelWallType.Web: BuildWebVisual(wallObj.transform, span, height, seed); break;
                case TunnelWallType.WoodPlanks: BuildWoodPlanksVisual(wallObj.transform, span, height, seed); break;
                case TunnelWallType.BrokenBricks: BuildBrokenBricksVisual(wallObj.transform, span, height, seed); break;
            }
            return wallObj;
        }

        public static Material GetWebMaterial()
        {
            if (_webMat != null) return _webMat;
            if (_webSilkTex == null) _webSilkTex = CreateSilkTexture();
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _webMat = new Material(s) { name = "Mat_TunnelWebSilk", color = new Color(0.92f, 0.94f, 0.89f, 1f) };
            if (_webMat.HasProperty("_BaseColor")) _webMat.SetColor("_BaseColor", _webMat.color);
            if (_webMat.HasProperty("_BaseMap")) _webMat.SetTexture("_BaseMap", _webSilkTex);
            if (_webMat.HasProperty("_MainTex")) _webMat.SetTexture("_MainTex", _webSilkTex);
            if (_webMat.HasProperty("_Smoothness")) _webMat.SetFloat("_Smoothness", 0.14f);
            if (_webMat.HasProperty("_Metallic")) _webMat.SetFloat("_Metallic", 0.0f);
            return _webMat;
        }

        private static Texture2D CreateSilkTexture()
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float fiberA = Mathf.Abs(Mathf.Sin((u * 14f + v * 6f) * Mathf.PI));
                    float fiberB = Mathf.Abs(Mathf.Sin((u * 8f - v * 13f) * Mathf.PI));
                    float noise = Mathf.PerlinNoise(u * 18f, v * 18f);
                    float weave = Mathf.Clamp01(0.76f + 0.14f * fiberA + 0.10f * fiberB - (1f - noise) * 0.18f);
                    tex.SetPixel(x, y, new Color(weave, weave * 1.01f, weave * 0.96f, 1f));
                }
            }
            tex.Apply();
            return tex;
        }

        private static void CreateStrand(Transform root, string name, Vector3 p1, Vector3 p2, float thickness, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(root, false);
            UnitySafeUtility.DestroySafe(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            Vector3 mid = (p1 + p2) * 0.5f;
            Vector3 dir = p2 - p1;
            if (dir.sqrMagnitude < 0.0001f) { UnitySafeUtility.DestroyGameObjectSafe(go); return; }
            go.transform.localPosition = mid;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            go.transform.localScale = new Vector3(thickness, dir.magnitude * 0.5f, thickness);
        }
    }
}
