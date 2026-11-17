using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Generación e integración de paredes destruibles en cada uno de los túneles de la mina.
    /// Sellan completamente la sección transversal del túnel de pared a pared y de suelo a techo.
    /// </summary>
    public static partial class CaveDungeonBuilder
    {
        public static void BuildDestructibleTunnelWalls(Transform parent)
        {
            Transform existing = parent.Find("DestructibleTunnelWalls");
            if (existing != null) ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(existing.gameObject);

            var wallsRoot = new GameObject("DestructibleTunnelWalls").transform;
            wallsRoot.SetParent(parent, false);

            float[] tunnelArchZ = { 44f, 118f, 212f, 306f, 402f, 496f };
            TunnelWallType[] wallSequence = {
                TunnelWallType.Web,
                TunnelWallType.WoodPlanks,
                TunnelWallType.BrokenBricks,
                TunnelWallType.Web,
                TunnelWallType.WoodPlanks,
                TunnelWallType.BrokenBricks
            };

            for (int i = 0; i < tunnelArchZ.Length; i++)
            {
                float z = tunnelArchZ[i];
                float sx = GetSpineX(z);
                GetWallWidthsAtZ(z, out float lw, out float rw);
                float centerX = sx + (rw - lw) * 0.5f;
                float fy = GetFloorY(centerX, z);
                float tunnelWidth = lw + rw;
                // Ancho ajustado al diámetro del túnel penetrando 2.5m en cada pared de roca para sellar sin huecos laterales
                float span = tunnelWidth + 5.0f;
                float h = GetCeilingHeightAtZ(z) + 3.0f;

                float yaw = Mathf.Atan2(GetSpineX(z + 3f) - GetSpineX(z - 3f), 6f) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 pos = new Vector3(centerX, fy - 0.5f, z);

                TunnelWallType type = wallSequence[i % wallSequence.Length];
                int wallHp = (i == 0) ? 300 : (700 + i * 47);
                DestructibleTunnelWallBuilder.Create(wallsRoot, $"TunnelWall_{i + 1}_{type}", pos, rot, span, h, type, wallHp);
            }
        }
    }
}
