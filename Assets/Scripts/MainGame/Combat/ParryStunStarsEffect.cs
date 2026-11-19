using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Efecto realista de guardia rota / aturdimiento por Parry perfecto (2s):
    /// chispas de acero/piedra al impactar, brasas persistentes y tambaleo físico del enemigo.
    /// </summary>
    public sealed class ParryStunStarsEffect : MonoBehaviour
    {
        private CaveCreatureBase _target;
        private ParticleSystem _stunSparks;
        private Transform _visualChild;
        private Transform _orbitRing;
        private Quaternion _origLocalRot = Quaternion.identity;
        private Vector3 _origLocalPos = Vector3.zero;
        private float _startTime, _endTime, _duration = 2.0f, _headOffsetY = 1.9f;

        public static void AttachOrRefresh(CaveCreatureBase target, float duration = 2.0f)
        {
            if (target == null || !target.IsAlive) return;
            var existing = target.GetComponent<ParryStunStarsEffect>();
            if (existing == null) existing = target.gameObject.AddComponent<ParryStunStarsEffect>();
            existing.Refresh(target, duration);
        }

        private void Refresh(CaveCreatureBase target, float duration)
        {
            _target = target;
            _duration = Mathf.Max(0.25f, duration);
            _startTime = Time.time;
            _endTime = Time.time + _duration;
            CacheVisualAndHeadHeight();
            EnsureSparksSystem();
            EnsureOrbitingStars();
            _stunSparks?.Emit(28);
        }

        private void CacheVisualAndHeadHeight()
        {
            if (_visualChild == null)
            {
                _visualChild = transform.Find("Visual_Golem") ?? transform.Find("Visual_Spider");
                if (_visualChild == null)
                {
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var ch = transform.GetChild(i);
                        if (ch.GetComponentInChildren<SkinnedMeshRenderer>() != null || ch.GetComponentInChildren<MeshRenderer>() != null)
                        {
                            _visualChild = ch;
                            break;
                        }
                    }
                }
                if (_visualChild != null)
                {
                    _origLocalRot = _visualChild.localRotation;
                    _origLocalPos = _visualChild.localPosition;
                }
            }
            float maxY = transform.position.y + 1.6f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled && !(r is ParticleSystemRenderer) && !r.name.StartsWith("StunStar") && r.bounds.max.y > maxY)
                    maxY = r.bounds.max.y;
            _headOffsetY = Mathf.Clamp(maxY - transform.position.y + 0.25f, 1.0f, 3.8f);
        }

        private static Mesh _starMesh;

        private void EnsureOrbitingStars()
        {
            if (_orbitRing != null) return;
            if (_starMesh == null) _starMesh = BuildFivePointStarMesh();
            _orbitRing = new GameObject("ParryStunOrbitRing").transform;
            _orbitRing.SetParent(transform, false);
            _orbitRing.localPosition = new Vector3(0f, _headOffsetY, 0f);

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var mat = new Material(s) { color = new Color(1f, 0.90f, 0.30f, 1f) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);

            // Anillo tenue de mareo alrededor de la cabeza
            var ringLr = _orbitRing.gameObject.AddComponent<LineRenderer>();
            ringLr.useWorldSpace = false; ringLr.loop = true; ringLr.positionCount = 24;
            ringLr.startWidth = ringLr.endWidth = 0.025f;
            ringLr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_DizzyHalo", true);
            ringLr.startColor = ringLr.endColor = new Color(1f, 0.86f, 0.25f, 0.42f);
            for (int r = 0; r < 24; r++)
            {
                float a = (r / 24f) * Mathf.PI * 2f;
                ringLr.SetPosition(r, new Vector3(Mathf.Cos(a) * 0.62f, 0f, Mathf.Sin(a) * 0.62f));
            }

            // 5 estrellas reales de 5 puntas girando e inclinándose en órbita
            for (int i = 0; i < 5; i++)
            {
                float ang = i * (Mathf.PI * 2f / 5f);
                var star = new GameObject($"StunStar_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                star.transform.SetParent(_orbitRing, false);
                star.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.62f, Mathf.Sin(i * 1.8f) * 0.07f, Mathf.Sin(ang) * 0.62f);
                star.transform.localRotation = Quaternion.Euler(22f, -ang * Mathf.Rad2Deg, 0f);
                star.transform.localScale = Vector3.one * 0.24f;
                star.GetComponent<MeshFilter>().sharedMesh = _starMesh;
                star.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        private static Mesh BuildFivePointStarMesh()
        {
            var m = new Mesh { name = "Proc_StunStar5Pt" };
            var verts = new Vector3[12]; // 0 = front tip, 1 = back tip, 2..11 = 10 outer/inner rim
            verts[0] = new Vector3(0f, 0f, 0.16f);
            verts[1] = new Vector3(0f, 0f, -0.16f);
            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? 0.55f : 0.23f;
                float a = i * (Mathf.PI / 5f) + Mathf.PI * 0.5f;
                verts[2 + i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            var tris = new int[60];
            for (int i = 0; i < 10; i++)
            {
                int next = (i + 1) % 10;
                tris[i * 6 + 0] = 0; tris[i * 6 + 1] = 2 + next; tris[i * 6 + 2] = 2 + i;
                tris[i * 6 + 3] = 1; tris[i * 6 + 4] = 2 + i;    tris[i * 6 + 5] = 2 + next;
            }
            m.vertices = verts; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private void EnsureSparksSystem()
        {
            if (_stunSparks != null) return;
            var go = new GameObject("ParryStaggerSparks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, Mathf.Max(0.75f, _headOffsetY * 0.6f), 0f);
            _stunSparks = go.AddComponent<ParticleSystem>();
            var main = _stunSparks.main;
            main.playOnAwake = true; main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.gravityModifier = 1.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.42f, 1f), new Color(0.92f, 0.40f, 0.10f, 0.9f));
            var em = _stunSparks.emission; em.rateOverTime = 16f;
            var sh = _stunSparks.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.35f;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch; psr.lengthScale = 2.0f;
            psr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial("Mat_ParryStagger", true);
        }

        private void LateUpdate()
        {
            if (_target == null || !_target.IsAlive || Time.time >= _endTime)
            {
                CleanupVisuals();
                Destroy(this);
                return;
            }
            float elapsed = Time.time - _startTime;
            float rem = Mathf.Clamp01((_endTime - Time.time) / _duration);
            if (_orbitRing != null)
            {
                _orbitRing.Rotate(0f, 300f * Time.deltaTime, 0f, Space.Self);
                _orbitRing.localPosition = new Vector3(0f, _headOffsetY + Mathf.Sin(elapsed * 5f) * 0.06f, 0f);
            }
            if (_visualChild != null)
            {
                bool isSpider = _target is SpiderEnemyController;
                float recoil = elapsed < 0.28f ? -Mathf.Sin((elapsed / 0.28f) * Mathf.PI) * (isSpider ? 14f : 17f) : 0f;
                float slumpPitch = (isSpider ? -13f : 18f) * Mathf.Sin(rem * Mathf.PI * 0.85f);
                float dizzyYaw = Mathf.Sin(elapsed * 4.6f) * (15f * rem);
                float dizzyRoll = Mathf.Cos(elapsed * 7.2f) * (11f * rem);
                float tremor = elapsed < 0.35f ? Mathf.Sin(elapsed * 55f) * 3.5f : 0f;
                _visualChild.localRotation = _origLocalRot * Quaternion.Euler(recoil + slumpPitch + tremor, dizzyYaw, dizzyRoll);
                float dipY = isSpider ? (Mathf.Sin(elapsed * 14f) * 0.03f * rem) : (-Mathf.Abs(Mathf.Sin(rem * Mathf.PI)) * 0.06f);
                _visualChild.localPosition = _origLocalPos + new Vector3(0f, dipY, elapsed < 0.26f ? -0.10f * (1f - elapsed / 0.26f) : 0f);
            }
        }

        private void CleanupVisuals()
        {
            if (_visualChild != null) { _visualChild.localRotation = _origLocalRot; _visualChild.localPosition = _origLocalPos; }
            if (_stunSparks != null) Destroy(_stunSparks.gameObject);
            if (_orbitRing != null) Destroy(_orbitRing.gameObject);
        }

        private void OnDestroy() => CleanupVisuals();
    }
}
