using UnityEngine;
using ForjaGame.Environment;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Filtra la visibilidad e intención de persecución dentro de la mina:
    /// bloquea el aggro cuando el jugador está fuera de la cueva o separado por muros destruibles intactos.
    /// </summary>
    public static class CaveTargetVisibility
    {
        public static bool HasAliveWallBetweenZ(float zA, float zB)
        {
            float lo = Mathf.Min(zA, zB), hi = Mathf.Max(zA, zB);
            var walls = DestructibleTunnelWall.ActiveWalls;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (w == null || !w.IsAlive || !w.gameObject.activeInHierarchy) continue;
                float wz = w.transform.position.z;
                if (lo <= wz + 0.15f && hi >= wz - 0.15f && (zA - wz) * (zB - wz) <= 0.05f) return true;
            }
            return false;
        }

        public static bool IsTargetAccessibleInCave(Vector3 observerPos, Transform candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy) return false;
            Vector3 targetPos = candidate.position;
            if (targetPos.z < CaveDungeonBuilder.CaveEntranceZ + 0.35f) return false;
            if (HasAliveWallBetweenZ(observerPos.z, targetPos.z)) return false;
            if (Mathf.Abs(observerPos.y - targetPos.y) > 8.5f) return false;
            return true;
        }

        public static Transform FindClosestCaveTarget(Vector3 observerPos)
        {
            Transform best = null;
            float bestSq = float.MaxValue;
            var list = PlayerHealthController.AllPlayers;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null || !p.IsAlive || !IsTargetAccessibleInCave(observerPos, p.transform)) continue;
                float sq = (p.transform.position - observerPos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p.transform; }
            }
            return best;
        }
    }
}
