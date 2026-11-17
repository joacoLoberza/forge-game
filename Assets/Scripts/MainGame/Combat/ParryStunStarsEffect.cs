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

        private void EnsureOrbitingStars()
        {
            if (_orbitRing != null) return;
            _orbitRing = new GameObject("ParryStunOrbitRing").transform;
            _orbitRing.SetParent(transform, false);
            _orbitRing.localPosition = new Vector3(0f, _headOffsetY, 0f);
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var mat = new Material(s) { color = new Color(1f, 0.88f, 0.25f, 1f) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
            for (int i = 0; i < 4; i++)
            {
                float ang = i * Mathf.PI * 0.5f;
                var star = GameObject.CreatePrimitive(PrimitiveType.Cube);
                star.name = $"StunStar_{i}";
                if (star.TryGetComponent<Collider>(out var c)) Destroy(c);
                star.transform.SetParent(_orbitRing, false);
                star.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.55f, Mathf.Sin(i * 1.6f) * 0.06f, Mathf.Sin(ang) * 0.55f);
                star.transform.localRotation = Quaternion.Euler(45f, i * 45f, 45f);
                star.transform.localScale = Vector3.one * 0.14f;
                star.GetComponent<Renderer>().sharedMaterial = mat;
            }
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
