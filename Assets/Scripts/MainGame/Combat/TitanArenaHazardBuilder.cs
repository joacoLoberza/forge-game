using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Generador procedural de ataques especiales de arena para el Titán Abisal (sin requerir animaciones extra):
    /// 1) Onda Sísmica Expansiva, 2) Lluvia de Estalactitas con círculos de aviso, 3) Línea de Agujas Abisales.
    /// </summary>
    public static class TitanArenaHazardBuilder
    {
        private static Material _magmaMat, _warningMat, _spireMat, _golemWaveMat;

        private static Material GetEmissiveMat(ref Material cache, string name, Color baseCol, Color emissiveCol)
        {
            if (cache != null) return cache;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            cache = new Material(s) { name = name, color = baseCol };
            if (cache.HasProperty("_BaseColor")) cache.SetColor("_BaseColor", baseCol);
            if (cache.HasProperty("_EmissionColor")) { cache.EnableKeyword("_EMISSION"); cache.SetColor("_EmissionColor", emissiveCol); }
            return cache;
        }

        public static void SpawnExpandingShockwave(Transform parent, Vector3 origin, float maxRadius, float duration, int damage)
        {
            var go = new GameObject("TitanShockwaveRing"); go.transform.SetParent(parent, false);
            float fy = CaveDungeonBuilder.GetFloorY(origin.x, origin.z);
            go.transform.position = new Vector3(origin.x, fy + 0.18f, origin.z);
            var runner = go.AddComponent<ShockwaveRingRunner>();
            runner.Initialize(1.6f, maxRadius, duration, damage,
                GetEmissiveMat(ref _magmaMat, "Mat_TitanShockwave", new Color(1f, 0.44f, 0.12f), new Color(2.4f, 0.75f, 0.15f)));
        }

        public static void SpawnGolemArmoredShockwave(Vector3 origin, float maxRadius, int damage, CaveCreatureBase attacker)
        {
            var go = new GameObject("GolemArmorShockwave");
            float fy = CaveDungeonBuilder.GetFloorY(origin.x, origin.z);
            go.transform.position = new Vector3(origin.x, fy + 0.10f, origin.z);
            var runner = go.AddComponent<ShockwaveRingRunner>();
            runner.Initialize(0.55f, Mathf.Clamp(maxRadius, 3.2f, 4.6f), 0.46f, damage,
                GetEmissiveMat(ref _golemWaveMat, "Mat_GolemArmorWave", new Color(1f, 0.74f, 0.14f), new Color(2.2f, 1.35f, 0.18f)),
                0.18f, 0.26f, attacker);
        }

        public static void SpawnStalactiteBarrage(MonoBehaviour host, Transform parent, Vector3 targetCenter, int count, int damage)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i <= 3 ? (i * 2.094f) : ((i - 4) * 2.094f + 1.047f);
                float dist = i == 0 ? 0f : (i <= 3 ? 4.8f : 8.6f);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                float wx = targetCenter.x + offset.x;
                float wz = Mathf.Clamp(targetCenter.z + offset.y, 496f, 550f);
                float fy = CaveDungeonBuilder.GetFloorY(wx, wz);
                host.StartCoroutine(StalactiteStrikeRoutine(parent, new Vector3(wx, fy, wz), 1.85f, 1.05f, damage, 600 + i * 19));
            }
        }

        public static void SpawnAbyssalSpireLine(MonoBehaviour host, Transform parent, Vector3 bossPos, Vector3 dir, int count, int damage)
        {
            for (int i = 0; i < count; i++)
            {
                float dist = 3.2f + i * 2.35f;
                Vector3 p = bossPos + dir * dist;
                float fy = CaveDungeonBuilder.GetFloorY(p.x, p.z);
                host.StartCoroutine(AbyssalSpireEruptionRoutine(parent, new Vector3(p.x, fy, p.z), i * 0.14f, damage, 800 + i * 23));
            }
        }

        private static IEnumerator StalactiteStrikeRoutine(Transform parent, Vector3 floorPos, float radius, float warningTime, int damage, int seed)
        {
            if (parent == null) yield break;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "RockfallWarningZone";
            marker.transform.SetParent(parent, false);
            marker.transform.position = floorPos + Vector3.up * 0.06f;
            if (marker.TryGetComponent<Collider>(out var mc)) UnitySafeUtility.DestroySafe(mc);
            marker.GetComponent<MeshRenderer>().sharedMaterial =
                GetEmissiveMat(ref _warningMat, "Mat_TitanWarning", new Color(0.95f, 0.18f, 0.12f), new Color(2.1f, 0.25f, 0.12f));

            for (float t = 0f; t < warningTime; t += Time.deltaTime)
            {
                if (marker == null) yield break;
                float s = Mathf.Lerp(0.4f, radius * 2f, t / warningTime);
                marker.transform.localScale = new Vector3(s, 0.04f, s);
                yield return null;
            }

            var rock = new GameObject("FallingSanctumStalactite", typeof(MeshFilter), typeof(MeshRenderer));
            rock.transform.SetParent(parent, false);
            rock.transform.position = floorPos + Vector3.up * 15.0f;
            rock.transform.rotation = Quaternion.Euler(180f, seed * 29f, 0f);
            rock.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(0.95f, 4.0f, seed);
            rock.GetComponent<MeshRenderer>().sharedMaterial = CaveDungeonBuilder.GetCaveRockMaterial();

            for (float f = 0f; f < 0.26f; f += Time.deltaTime)
            {
                if (rock == null) yield break;
                rock.transform.position = Vector3.Lerp(floorPos + Vector3.up * 15.0f, floorPos + Vector3.up * 1.8f, f / 0.26f);
                yield return null;
            }

            if (marker != null) UnitySafeUtility.DestroyGameObjectSafe(marker);
            PlayerCameraFollow.Instance?.TriggerImpulse(0.15f);
            ForgeAudioSystem.PlayTitanSound(floorPos, CreatureSoundType.Attack);
            DamagePlayersInRadius(floorPos, radius, 3.2f, damage, 6.5f);

            yield return new WaitForSeconds(0.45f);
            if (rock != null) UnitySafeUtility.DestroyGameObjectSafe(rock);
        }

        private static IEnumerator AbyssalSpireEruptionRoutine(Transform parent, Vector3 floorPos, float delay, int damage, int seed)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (parent == null) yield break;

            var spire = new GameObject("AbyssalCrystalSpire", typeof(MeshFilter), typeof(MeshRenderer));
            spire.transform.SetParent(parent, false);
            spire.transform.position = floorPos + Vector3.down * 3.2f;
            spire.transform.rotation = Quaternion.Euler(0f, seed * 37f, 0f);
            spire.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateCrystalSpireMesh(0.95f, 3.5f, seed);
            spire.GetComponent<MeshRenderer>().sharedMaterial =
                GetEmissiveMat(ref _spireMat, "Mat_AbyssalSpire", new Color(0.95f, 0.55f, 0.14f), new Color(1.9f, 0.85f, 0.18f));

            for (float w = 0f; w < 0.45f; w += Time.deltaTime)
            {
                if (spire == null) yield break;
                spire.transform.position = Vector3.Lerp(floorPos + Vector3.down * 3.0f, floorPos + Vector3.down * 2.4f, w / 0.45f);
                yield return null;
            }

            ForgeAudioSystem.PlayTitanSound(floorPos, CreatureSoundType.Attack);
            for (float u = 0f; u < 0.16f; u += Time.deltaTime)
            {
                if (spire == null) yield break;
                spire.transform.position = Vector3.Lerp(floorPos + Vector3.down * 2.4f, floorPos, u / 0.16f);
                yield return null;
            }

            DamagePlayersInRadius(floorPos, 2.15f, 3.6f, damage, 7.5f);
            yield return new WaitForSeconds(0.55f);

            for (float d = 0f; d < 0.35f; d += Time.deltaTime)
            {
                if (spire == null) yield break;
                spire.transform.position = Vector3.Lerp(floorPos, floorPos + Vector3.down * 3.5f, d / 0.35f);
                yield return null;
            }
            if (spire != null) UnitySafeUtility.DestroyGameObjectSafe(spire);
        }

        internal static void DamagePlayersInRadius(Vector3 center, float radius, float maxHeight, int damage, float knockback)
        {
            float rSq = radius * radius;
            foreach (var p in PlayerHealthController.AllPlayers)
            {
                if (p == null || p.CurrentHealth <= 0) continue;
                Vector3 d = p.transform.position - center;
                if (Mathf.Abs(d.y) > maxHeight) continue;
                d.y = 0f;
                if (d.sqrMagnitude <= rSq)
                {
                    p.TakeDamageFrom(damage, null, true);
                    Vector3 kbDir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
                    if (p.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                        rb.linearVelocity = new Vector3(kbDir.x * knockback, 2.2f, kbDir.z * knockback);
                }
            }
        }

        private sealed class ShockwaveRingRunner : MonoBehaviour
        {
            private readonly Transform[] _segments = new Transform[20];
            private readonly HashSet<int> _hitPlayers = new();
            private float _startRadius, _maxRadius, _duration, _elapsed, _segHeight, _segDepth;
            private int _damage;
            private CaveCreatureBase _attacker;

            public void Initialize(float startR, float maxR, float dur, int dmg, Material mat, float segH = 0.42f, float segD = 0.55f, CaveCreatureBase attacker = null)
            {
                _startRadius = startR; _maxRadius = maxR; _duration = Mathf.Max(0.2f, dur); _damage = dmg;
                _segHeight = segH; _segDepth = segD; _attacker = attacker;
                for (int i = 0; i < _segments.Length; i++)
                {
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube); seg.name = $"WaveSeg_{i}"; seg.transform.SetParent(transform, false);
                    if (seg.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
                    seg.GetComponent<MeshRenderer>().sharedMaterial = mat; _segments[i] = seg.transform;
                }
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                float r = Mathf.Lerp(_startRadius, _maxRadius, t);
                float segLen = (2f * Mathf.PI * r / _segments.Length) * 1.08f;
                for (int i = 0; i < _segments.Length; i++)
                {
                    float ang = (i / (float)_segments.Length) * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    _segments[i].localPosition = dir * r;
                    _segments[i].localRotation = Quaternion.LookRotation(dir);
                    _segments[i].localScale = new Vector3(segLen, _segHeight * (1f - t * 0.4f), _segDepth);
                }
                CheckWaveCollisions(r);
                if (t >= 1f) UnitySafeUtility.DestroyGameObjectSafe(gameObject);
            }

            private void CheckWaveCollisions(float currentRadius)
            {
                float maxY = _segHeight < 0.25f ? 0.50f : 0.62f;
                foreach (var p in PlayerHealthController.AllPlayers)
                {
                    if (p == null || p.CurrentHealth <= 0 || _hitPlayers.Contains(p.GetInstanceID())) continue;
                    Vector3 diff = p.transform.position - transform.position;
                    if (diff.y > maxY || diff.y < -1.5f) continue;
                    float dist = new Vector2(diff.x, diff.z).magnitude;
                    if (Mathf.Abs(dist - currentRadius) <= (_segHeight < 0.25f ? 0.68f : 0.85f))
                    {
                        _hitPlayers.Add(p.GetInstanceID());
                        p.TakeDamageFrom(_damage, _attacker, _attacker == null);
                        Vector3 kb = new Vector3(diff.x, 0f, diff.z).normalized;
                        float push = _attacker != null ? 5.2f : 7.5f;
                        if (p.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                            rb.linearVelocity = new Vector3(kb.x * push, 2.2f, kb.z * push);
                    }
                }
            }
        }
    }
}
