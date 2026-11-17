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

        public static GameObject Create(Transform parent, string name, Vector3 pos, Quaternion rot, float span, float height, TunnelWallType type, int seed)
        {
            var wallObj = new GameObject(name);
            wallObj.transform.SetParent(parent, false);
            wallObj.transform.SetPositionAndRotation(pos, rot);

            var col = wallObj.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height * 0.45f, 0f);
            col.size = new Vector3(span + 2.0f, height + 2.0f, type == TunnelWallType.BrokenBricks ? 3.2f : 2.4f);

            var wallCtrl = wallObj.AddComponent<DestructibleTunnelWall>();
            int hp = type switch
            {
                TunnelWallType.Web => 160,
                TunnelWallType.WoodPlanks => 240,
                _ => 300
            };
            wallCtrl.Initialize(type, hp, col);

            switch (type)
            {
                case TunnelWallType.Web:
                    BuildWebVisual(wallObj.transform, span, height, seed);
                    break;
                case TunnelWallType.WoodPlanks:
                    BuildWoodPlanksVisual(wallObj.transform, span, height, seed);
                    break;
                case TunnelWallType.BrokenBricks:
                    BuildBrokenBricksVisual(wallObj.transform, span, height, seed);
                    break;
            }

            return wallObj;
        }

        public static Material GetWebMaterial()
        {
            if (_webMat != null) return _webMat;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _webMat = new Material(s)
            {
                name = "Mat_TunnelWebSilk",
                color = new Color(0.92f, 0.94f, 0.88f, 0.92f)
            };
            if (_webMat.HasProperty("_BaseColor")) _webMat.SetColor("_BaseColor", _webMat.color);
            return _webMat;
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
            go.transform.localPosition = mid;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            go.transform.localScale = new Vector3(thickness, dir.magnitude * 0.5f, thickness);
        }
    }
}
