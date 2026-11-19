using System.Collections;
using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Generador de VFX 3D de alto impacto visual para las pasivas de aleación:
    /// anillos expansivos de choque, tajos crecientes carmesí, arcos de rayo cobalto y auras áureas.
    /// </summary>
    public static class AlloyPassiveVfx
    {
        public static void SpawnShockwaveRing(Vector3 origin, float maxRadius, Color color, float duration = 0.34f, float width = 0.24f)
        {
            var go = new GameObject("AlloyShockRing");
            go.transform.position = new Vector3(origin.x, origin.y + 0.12f, origin.z);
            var runner = go.AddComponent<VfxCoroutineHost>();
            runner.StartCoroutine(AnimateShockRing(go, maxRadius, color, duration, width));
        }

        public static void SpawnCrescentSlash(Vector3 center, Vector3 forward, Color color, float radius = 1.65f)
        {
            var go = new GameObject("AlloyCrescentSlash");
            go.transform.position = center + Vector3.up * 0.95f;
            go.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward) * Quaternion.Euler(14f, 0f, -18f);
            var runner = go.AddComponent<VfxCoroutineHost>();
            runner.StartCoroutine(AnimateCrescentSlash(go, color, radius));
        }

        public static void SpawnLightningArc(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("AlloyCobaltArc");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.positionCount = 8;
            lr.startWidth = 0.11f; lr.endWidth = 0.04f;
            lr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_CobaltArc", true);
            lr.startColor = Color.white; lr.endColor = color;

            Vector3 dir = to - from;
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < 7) p += Random.insideUnitSphere * Mathf.Min(0.45f, dir.magnitude * 0.14f);
                lr.SetPosition(i, p);
            }
            var host = go.AddComponent<VfxCoroutineHost>();
            host.StartCoroutine(FadeLineAndDestroy(lr, Color.white, color, 0.24f));
        }

        public static void SpawnSparkBurst(Vector3 pos, Color c1, Color c2, int count = 26, float radius = 0.25f)
        {
            var go = new GameObject("AlloySparkBurst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.56f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 7.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
            main.gravityModifier = 0.7f;
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch; psr.lengthScale = 2.6f;
            psr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_AlloySpark", true);
            ps.Emit(count);
            Object.Destroy(go, 0.85f);
        }

        public static void SpawnPlayerAuraPillar(Transform player, Color color, float radius = 0.72f)
        {
            if (player == null) return;
            var go = new GameObject("AlloyPlayerAura");
            go.transform.position = player.position + Vector3.up * 0.1f;
            var host = go.AddComponent<VfxCoroutineHost>();
            host.StartCoroutine(AnimatePlayerAura(go, player, color, radius));
        }

        private static IEnumerator AnimateShockRing(GameObject go, float maxR, Color col, float dur, float width)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 32;
            lr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_ShockRing", true);
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                float t = Mathf.Clamp01(e / dur);
                float r = Mathf.Lerp(0.35f, maxR, 1f - (1f - t) * (1f - t));
                float a = Mathf.Clamp01(1f - t);
                lr.startWidth = lr.endWidth = width * Mathf.Lerp(1.25f, 0.20f, t);
                Color c = new Color(col.r, col.g, col.b, a * 0.92f);
                lr.startColor = lr.endColor = c;
                for (int i = 0; i < 32; i++)
                {
                    float ang = (i / 32f) * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        private static IEnumerator AnimateCrescentSlash(GameObject go, Color col, float rad)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = false; lr.positionCount = 16;
            lr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_Crescent", true);
            const float dur = 0.24f;
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                float t = Mathf.Clamp01(e / dur);
                float a = 1f - t;
                lr.startWidth = 0.04f; lr.endWidth = 0.28f * a;
                lr.startColor = new Color(col.r, col.g, col.b, a * 0.4f);
                lr.endColor = new Color(1f, col.g * 0.6f, col.b * 0.6f, a);
                float sweepR = rad * Mathf.Lerp(0.75f, 1.30f, t);
                for (int i = 0; i < 16; i++)
                {
                    float ang = Mathf.Lerp(-1.15f, 1.15f, i / 15f);
                    lr.SetPosition(i, new Vector3(Mathf.Sin(ang) * sweepR, 0f, Mathf.Cos(ang) * sweepR * 0.78f));
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        private static IEnumerator AnimatePlayerAura(GameObject go, Transform target, Color col, float rad)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 24;
            lr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_Aura", true);
            const float dur = 0.42f;
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                if (target != null) go.transform.position = target.position + Vector3.up * Mathf.Lerp(0.15f, 1.65f, e / dur);
                float t = Mathf.Clamp01(e / dur);
                float a = Mathf.Sin(t * Mathf.PI);
                float r = rad * Mathf.Lerp(1.1f, 0.65f, t);
                lr.startWidth = lr.endWidth = 0.14f * a;
                lr.startColor = lr.endColor = new Color(col.r, col.g, col.b, a * 0.90f);
                for (int i = 0; i < 24; i++)
                {
                    float ang = (i / 24f) * Mathf.PI * 2f + t * 5f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
                }
                yield return null;
            }
            Object.Destroy(go);
        }

        private static IEnumerator FadeLineAndDestroy(LineRenderer lr, Color cStart, Color cEnd, float dur)
        {
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                float a = 1f - Mathf.Clamp01(e / dur);
                lr.startColor = new Color(cStart.r, cStart.g, cStart.b, a);
                lr.endColor = new Color(cEnd.r, cEnd.g, cEnd.b, a);
                yield return null;
            }
            if (lr != null) Object.Destroy(lr.gameObject);
        }

        private sealed class VfxCoroutineHost : MonoBehaviour { }
    }
}

