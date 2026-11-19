using UnityEngine;
using ForjaGame.Environment;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Filtra la visibilidad e intención de persecución dentro de la mina:
    /// bloquea visión a través de muros de cueva o paredes destruibles y calcula ángulos de flanqueo en manada.
    /// </summary>
    public static class CaveTargetVisibility
    {
        private static readonly RaycastHit[] _losHitBuffer = new RaycastHit[24];

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

        public static bool HasCaveWallLineOfSightBlocked(Vector3 observerPos, Vector3 targetPos)
        {
            Vector3 from = observerPos + Vector3.up * 1.25f;
            Vector3 to = targetPos + Vector3.up * 1.20f;
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.4f) return false;
            int hitCount = Physics.RaycastNonAlloc(from, d / dist, _losHitBuffer, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider c = _losHitBuffer[i].collider;
                if (c == null) continue;
                if (c.GetComponentInParent<IDestructibleTunnelWall>() != null) return true;
                if (c.GetComponentInParent<CaveCreatureBase>() != null || c.GetComponentInParent<Core.PlayerMovement>() != null
                    || c.GetComponentInParent<PlayerHealthController>() != null || c.GetComponentInParent<ResourceNodeController>() != null)
                    continue;
                if (_losHitBuffer[i].normal.y < 0.55f) return true;
            }
            return false;
        }

        public static bool IsTargetAccessibleInCave(Vector3 observerPos, Transform candidate, bool requireLineOfSight = true)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy) return false;
            Vector3 targetPos = candidate.position;
            if (targetPos.z < CaveDungeonBuilder.CaveEntranceZ + 0.35f) return false;
            if (HasAliveWallBetweenZ(observerPos.z, targetPos.z)) return false;
            if (Mathf.Abs(observerPos.y - targetPos.y) > 8.5f) return false;
            if (requireLineOfSight && HasCaveWallLineOfSightBlocked(observerPos, targetPos)) return false;
            return true;
        }

        public static Transform FindClosestCaveTarget(Vector3 observerPos, bool requireLineOfSight = true)
        {
            Transform best = null;
            float bestSq = float.MaxValue;
            var list = PlayerHealthController.AllPlayers;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null || !p.IsAlive || !IsTargetAccessibleInCave(observerPos, p.transform, requireLineOfSight)) continue;
                float sq = (p.transform.position - observerPos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p.transform; }
            }
            return best;
        }

        public static Vector3 ComputePackFlankPosition(Vector3 targetPos, Vector3 selfPos, int memberIndex, int groupCount, float radius)
        {
            Vector3 away = selfPos - targetPos; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            if (groupCount <= 1) return targetPos + away.normalized * Mathf.Min(radius, away.magnitude);

            float spreadStep = groupCount == 2 ? 58f : 46f;
            float centeredOffset = (memberIndex - (groupCount - 1) * 0.5f) * spreadStep;
            Vector3 flankDir = Quaternion.Euler(0f, centeredOffset, 0f) * away.normalized;
            float sx = CaveDungeonBuilder.GetSpineX(targetPos.z + flankDir.z * radius);
            CaveDungeonBuilder.GetWallWidthsAtZ(targetPos.z + flankDir.z * radius, out float lw, out float rw);
            Vector3 res = targetPos + flankDir * radius;
            res.x = Mathf.Clamp(res.x, sx - Mathf.Max(1.6f, lw - 1.8f), sx + Mathf.Max(1.6f, rw - 1.8f));
            res.y = selfPos.y;
            return res;
        }

        public static void AlertPackAllies<T>(System.Collections.Generic.IList<T> list, CaveCreatureBase caller, Transform threat, float radius = 14.5f) where T : CaveCreatureBase
        {
            if (threat == null || list == null) return;
            float rSq = radius * radius;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null || c == caller || !c.IsAlive) continue;
                if ((c.transform.position - caller.transform.position).sqrMagnitude <= rSq && IsTargetAccessibleInCave(c.transform.position, threat, true))
                    c.ForceRitualAggro(threat);
            }
        }

        public static void GetPackSlot<T>(System.Collections.Generic.IList<T> list, CaveCreatureBase self, Transform target, float radius, out int rank, out int count, out bool anyAllyAttacking) where T : CaveCreatureBase
        {
            rank = 0; count = 0; anyAllyAttacking = false;
            float rSq = radius * radius;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null || !c.IsAlive || (c.transform.position - target.position).sqrMagnitude > rSq) continue;
                if (c != self && c.IsAttackingOrWindup) anyAllyAttacking = true;
                if (c.GetInstanceID() < self.GetInstanceID()) rank++;
                count++;
            }
        }

        public static bool ShouldWaitForPlayerAttackOpening(Transform target, ref float waitDeadline, float maxPatience = 0.90f)
        {
            if (target == null) { waitDeadline = 0f; return false; }
            var atk = target.GetComponentInParent<AttackController>();
            if (atk == null || !atk.IsAttacking || atk.AttackProgress >= 0.66f)
            { waitDeadline = 0f; return false; }
            if (waitDeadline <= 0f) waitDeadline = Time.time + maxPatience;
            if (Time.time < waitDeadline) return true;
            waitDeadline = 0f;
            return false;
        }
    }
}
