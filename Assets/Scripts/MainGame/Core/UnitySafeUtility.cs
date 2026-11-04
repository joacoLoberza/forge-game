using UnityEngine;

namespace ForjaGame.Core
{
    /// <summary>
    /// Utilidades compartidas de ciclo de vida seguro (Play Mode / Edit Mode), obtención
    /// de componentes sin errores de sobrecarga de Unity y sondeo físico sin asignaciones GC.
    /// </summary>
    public static class UnitySafeUtility
    {
        private static readonly RaycastHit[] GroundHitsBuffer = new RaycastHit[12];

        public static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        public static void DestroyGameObjectSafe(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (!go.name.StartsWith("_DESTROYED_"))
                go.name = "_DESTROYED_" + go.name;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        public static void DestroyChildrenNamed(Transform parent, string exactName)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform ch = parent.GetChild(i);
                if (ch != null && ch.name == exactName)
                {
                    if (!Application.isPlaying) ch.SetParent(null);
                    DestroyGameObjectSafe(ch.gameObject);
                }
            }
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            if (!go.TryGetComponent<T>(out var comp))
                comp = go.AddComponent<T>();
            return comp;
        }

        public static float ProbeGroundY(Vector3 worldPos, Transform ignoreHierarchy = null, float rayStartOffset = 6.0f, float maxDist = 24.0f)
        {
            Vector3 origin = new Vector3(worldPos.x, worldPos.y + rayStartOffset, worldPos.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHitsBuffer, maxDist, ~0, QueryTriggerInteraction.Ignore);

            float bestY = worldPos.y;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                Collider col = GroundHitsBuffer[i].collider;
                if (col == null) continue;
                if (ignoreHierarchy != null && (col.transform == ignoreHierarchy || col.transform.IsChildOf(ignoreHierarchy)))
                    continue;
                if (col.GetComponentInParent<PlayerMovement>() != null)
                    continue;

                if (GroundHitsBuffer[i].distance < bestDist)
                {
                    bestDist = GroundHitsBuffer[i].distance;
                    bestY = GroundHitsBuffer[i].point.y;
                    found = true;
                }
            }

            return found ? bestY : worldPos.y;
        }
    }
}

