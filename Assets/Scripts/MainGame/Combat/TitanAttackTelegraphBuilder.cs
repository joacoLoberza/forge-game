using System.Collections;
using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Generador procedural de advertencias visuales volumétricas en 3D para los ataques del Titán Abisal.
    /// Su grosor vertical evita que se pierdan o entierren bajo el relieve irregular de la caverna.
    /// </summary>
    public static class TitanAttackTelegraphBuilder
    {
        private static Material _warningMat;

        public static Material GetWarningMaterial()
        {
            if (_warningMat != null) return _warningMat;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _warningMat = new Material(s)
            {
                name = "Mat_TitanTelegraphWarning",
                color = new Color(0.98f, 0.18f, 0.08f, 0.75f)
            };
            if (_warningMat.HasProperty("_BaseColor")) _warningMat.SetColor("_BaseColor", _warningMat.color);
            if (_warningMat.HasProperty("_EmissionColor"))
            {
                _warningMat.EnableKeyword("_EMISSION");
                _warningMat.SetColor("_EmissionColor", new Color(3.2f, 0.45f, 0.15f));
            }
            return _warningMat;
        }

        public static void SpawnMeleeCleaveTelegraph(
            MonoBehaviour host, Transform parent, Vector3 origin, Vector3 forward,
            float range, float angleDeg, float duration)
        {
            if (host == null || parent == null) return;
            host.StartCoroutine(MeleeCleaveRoutine(parent, origin, forward, range, angleDeg, duration));
        }

        public static void SpawnSpireWarningLine(
            MonoBehaviour host, Transform parent, Vector3 startPos, Vector3 dir,
            float length, float duration)
        {
            if (host == null || parent == null) return;
            host.StartCoroutine(SpireWarningLineRoutine(parent, startPos, dir, length, duration));
        }

        public static void SpawnShockwaveChargeTelegraph(
            MonoBehaviour host, Transform parent, Vector3 origin, float duration)
        {
            if (host == null || parent == null) return;
            host.StartCoroutine(ShockwaveChargeRoutine(parent, origin, duration));
        }

        private static IEnumerator MeleeCleaveRoutine(
            Transform parent, Vector3 origin, Vector3 forward, float range, float angleDeg, float duration)
        {
            float fy = CaveDungeonBuilder.GetFloorY(origin.x, origin.z);
            var go = new GameObject("Telegraph_MeleeCone", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(origin.x, fy + 0.35f, origin.z);

            Mesh mesh = CreateVolumetricConeMesh(range, angleDeg, 16, 0.85f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = GetWarningMaterial();

            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (go == null) yield break;
                float progress = t / duration;
                float pulse = 0.88f + 0.12f * Mathf.Sin(t * 22f);
                go.transform.localScale = new Vector3(pulse, 1f, Mathf.Lerp(0.40f, 1.0f, progress) * pulse);
                yield return null;
            }
            if (go != null) UnitySafeUtility.DestroyGameObjectSafe(go);
        }

        private static IEnumerator SpireWarningLineRoutine(
            Transform parent, Vector3 startPos, Vector3 dir, float length, float duration)
        {
            float fy = CaveDungeonBuilder.GetFloorY(startPos.x, startPos.z);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Telegraph_SpireLine";
            go.transform.SetParent(parent, false);
            if (go.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
            go.GetComponent<MeshRenderer>().sharedMaterial = GetWarningMaterial();

            dir.y = 0f; dir.Normalize();
            Vector3 center = startPos + dir * (length * 0.5f);
            center.y = fy + 0.40f;
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(dir);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (go == null) yield break;
                float pulse = 0.82f + 0.18f * Mathf.Sin(t * 24f);
                go.transform.localScale = new Vector3(2.6f * pulse, 0.85f, length);
                yield return null;
            }
            if (go != null) UnitySafeUtility.DestroyGameObjectSafe(go);
        }

        private static IEnumerator ShockwaveChargeRoutine(
            Transform parent, Vector3 origin, float duration)
        {
            float fy = CaveDungeonBuilder.GetFloorY(origin.x, origin.z);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Telegraph_ShockwaveCharge";
            go.transform.SetParent(parent, false);
            if (go.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
            go.GetComponent<MeshRenderer>().sharedMaterial = GetWarningMaterial();
            go.transform.position = new Vector3(origin.x, fy + 0.35f, origin.z);

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (go == null) yield break;
                float s = Mathf.Lerp(2.5f, 10.5f, t / duration);
                go.transform.localScale = new Vector3(s, 0.45f, s);
                yield return null;
            }
            if (go != null) UnitySafeUtility.DestroyGameObjectSafe(go);
        }

        private static Mesh CreateVolumetricConeMesh(float radius, float angleDeg, int segments, float thick)
        {
            var mesh = new Mesh { name = "MeleeVolumetricCone" };
            int count = segments + 1;
            var verts = new Vector3[(count + 1) * 2];
            float halfAngle = angleDeg * 0.5f, halfH = thick * 0.5f;

            // Capa superior (+halfH) y capa inferior (-halfH)
            verts[0] = new Vector3(0f, halfH, 0f);
            verts[count + 1] = new Vector3(0f, -halfH, 0f);

            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments) * Mathf.Deg2Rad;
                float x = Mathf.Sin(a) * radius, z = Mathf.Cos(a) * radius;
                verts[i + 1] = new Vector3(x, halfH, z);
                verts[count + 2 + i] = new Vector3(x, -halfH, z);
            }

            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < segments; i++)
            {
                tris.Add(0); tris.Add(i + 1); tris.Add(i + 2);
                int b0 = count + 1, b1 = count + 2 + i, b2 = count + 3 + i;
                tris.Add(b0); tris.Add(b2); tris.Add(b1);
                tris.Add(i + 1); tris.Add(b1); tris.Add(b2);
                tris.Add(i + 1); tris.Add(b2); tris.Add(i + 2);
            }

            mesh.vertices = verts;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
