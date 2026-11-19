using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Telegrafiado diegético PvE (estilo Souls / Monster Hunter) sin brillos artificiales ni rotaciones irreales:
    /// durante la preparación comprime ligeramente la postura hacia abajo/atrás manteniendo las patas apoyadas en el suelo,
    /// genera un leve temblor muscular al límite de la carga y expulsa pequeñas motas de polvo pétreo al impactar.
    /// </summary>
    public sealed class EnemyAttackTelegraph : MonoBehaviour
    {
        private struct DustMote { public Transform Tr; public Vector3 Vel; public float Life, MaxLife, BaseScale; }

        private CaveCreatureBase _owner;
        private Transform _visualChild;
        private Quaternion _baseChildRot = Quaternion.identity;
        private Vector3 _baseChildPos = Vector3.zero, _baseChildScale = Vector3.one;
        private float _startTime, _endTime, _lungeUntil, _nextSparkTime;
        private bool _active, _inStrikeFollowThrough, _dustTriggered, _isArmored;
        private readonly List<DustMote> _motes = new();
        private static Material _dustMat, _goldSparkMat;
        private Light _armorLight;
        private Transform _armorAegisRoot;
        private readonly Transform[] _armorShields = new Transform[4];
        private readonly Transform[] _armorRingSegs = new Transform[12];
        private float _groundRadius = 2.2f;

        public static void Begin(CaveCreatureBase owner, float windupDuration, string label, float groundRadius = 0f, bool isArmored = false)
        {
            if (owner == null || !owner.IsAlive) return;
            var t = owner.TryGetComponent<EnemyAttackTelegraph>(out var existing) && existing != null ? existing : owner.gameObject.AddComponent<EnemyAttackTelegraph>();
            t.Activate(owner, windupDuration, groundRadius, isArmored);
        }

        public static void Cancel(CaveCreatureBase owner) { if (owner != null && owner.TryGetComponent<EnemyAttackTelegraph>(out var t) && t != null) t.StopVisuals(); }

        public static void EmitImpactDust(Vector3 worldPos, Vector3 forward, int count = 6, float spread = 0.65f)
        {
            var host = FindAnyOwnerInRange(worldPos);
            if (host != null) host.SpawnDustBurst(worldPos, forward, count, spread);
        }

        private static readonly List<EnemyAttackTelegraph> ActiveTelegraphs = new();
        private void OnEnable() { if (!ActiveTelegraphs.Contains(this)) ActiveTelegraphs.Add(this); }
        private void OnDisable() { ActiveTelegraphs.Remove(this); }

        private static EnemyAttackTelegraph FindAnyOwnerInRange(Vector3 pos)
        {
            EnemyAttackTelegraph best = null; float bestSq = 25f;
            for (int i = 0; i < ActiveTelegraphs.Count; i++)
            {
                var t = ActiveTelegraphs[i];
                if (t == null) continue;
                float sq = (t.transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }

        private void Activate(CaveCreatureBase owner, float duration, float groundRadius, bool isArmored)
        {
            _owner = owner; _startTime = Time.time; _endTime = Time.time + Mathf.Max(0.25f, duration);
            _active = true; _inStrikeFollowThrough = false; _dustTriggered = false; _isArmored = isArmored; _nextSparkTime = 0f;
            _groundRadius = Mathf.Clamp(groundRadius, 1.5f, 3.2f);
            SetArmorAegisVisible(isArmored);
            CacheVisualRig();
            SpawnDustBurst(transform.position + transform.forward * 0.45f, transform.forward, 4, 0.38f, isArmored);
        }

        private void SetArmorAegisVisible(bool visible)
        {
            if (visible)
            {
                EnsureGoldMat();
                if (_armorLight == null)
                {
                    var lg = new GameObject("ArmorTelegraphLight", typeof(Light));
                    lg.transform.SetParent(transform, false); lg.transform.localPosition = Vector3.up * 1.45f;
                    _armorLight = lg.GetComponent<Light>(); _armorLight.type = LightType.Point;
                    _armorLight.color = new Color(1f, 0.84f, 0.16f); _armorLight.range = 6.2f;
                }
                _armorLight.enabled = true; _armorLight.intensity = 4.6f;
                if (_armorAegisRoot == null) BuildArmorAegisRig();
                _armorAegisRoot.gameObject.SetActive(true);
            }
            else
            {
                if (_armorLight != null) _armorLight.enabled = false;
                if (_armorAegisRoot != null) _armorAegisRoot.gameObject.SetActive(false);
            }
        }

        private void BuildArmorAegisRig()
        {
            _armorAegisRoot = new GameObject("ArmorAegisRig").transform;
            _armorAegisRoot.SetParent(transform, false);
            for (int i = 0; i < _armorShields.Length; i++)
            {
                var sh = new GameObject($"ArmorCrest_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                sh.transform.SetParent(_armorAegisRoot, false);
                sh.GetComponent<MeshFilter>().sharedMesh = ForjaGame.Resources.OreMeshGenerator.CreateCrystalSpireMesh(0.26f, 0.78f, 510 + i * 19);
                sh.GetComponent<MeshRenderer>().sharedMaterial = _goldSparkMat;
                _armorShields[i] = sh.transform;
            }
            for (int i = 0; i < _armorRingSegs.Length; i++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube); seg.name = $"ArmorRing_{i}";
                if (seg.TryGetComponent<Collider>(out var c)) Destroy(c);
                seg.transform.SetParent(_armorAegisRoot, false);
                seg.GetComponent<MeshRenderer>().sharedMaterial = _goldSparkMat;
                _armorRingSegs[i] = seg.transform;
            }
        }

        private void CacheVisualRig()
        {
            if (_visualChild != null) return;
            for (int i = 0; i < transform.childCount; i++)
            {
                var ch = transform.GetChild(i);
                if (ch.name.StartsWith("Visual") || ch.GetComponentInChildren<Renderer>() != null)
                { _visualChild = ch; _baseChildRot = ch.localRotation; _baseChildPos = ch.localPosition; _baseChildScale = ch.localScale; break; }
            }
        }

        private void LateUpdate()
        {
            UpdateDustMotes();
            if (_inStrikeFollowThrough)
            {
                if (_visualChild == null || Time.time >= _lungeUntil) { _inStrikeFollowThrough = false; RestoreRig(); return; }
                float u = 1f - Mathf.Clamp01((_lungeUntil - Time.time) / 0.18f);
                float snap = Mathf.Sin(u * Mathf.PI);
                _visualChild.localPosition = _baseChildPos + new Vector3(0f, 0f, 0.12f * snap);
                _visualChild.localScale = new Vector3(_baseChildScale.x * (1f - 0.03f * snap), _baseChildScale.y * (1f + 0.04f * snap), _baseChildScale.z * (1f + 0.05f * snap));
                return;
            }

            if (!_active) return;
            if (_owner == null || !_owner.IsAlive || _owner.IsStunned) { StopVisuals(); return; }

            if (Time.time >= _endTime)
            {
                _active = false; _inStrikeFollowThrough = true; _lungeUntil = Time.time + 0.18f;
                _isArmored = false; SetArmorAegisVisible(false);
                SpawnDustBurst(transform.position + transform.forward * 0.75f, transform.forward, 5, 0.55f);
                return;
            }
            float pct = Mathf.Clamp01((Time.time - _startTime) / Mathf.Max(0.05f, _endTime - _startTime));
            if (_isArmored) UpdateArmorAegisAnim(pct);

            float ease = pct * pct * (3f - 2f * pct);
            bool apexHold = (_endTime - Time.time) <= 0.24f;
            if (apexHold && !_dustTriggered) { _dustTriggered = true; SpawnDustBurst(transform.position + transform.forward * 0.35f, -transform.forward, 3, 0.30f); }
            float tremor = apexHold ? Mathf.Sin(Time.time * 42f) * 0.012f : 0f;
            if (_visualChild != null)
            {
                _visualChild.localRotation = _baseChildRot;
                _visualChild.localPosition = _baseChildPos + new Vector3(tremor, -0.025f * ease, -0.06f * ease);
                _visualChild.localScale = new Vector3(_baseChildScale.x * (1f + 0.045f * ease), _baseChildScale.y * (1f - 0.065f * ease), _baseChildScale.z * (1f + 0.035f * ease));
            }
        }

        private void UpdateArmorAegisAnim(float pct)
        {
            if (_armorLight != null) _armorLight.intensity = 4.0f + 1.8f * Mathf.Sin(Time.time * 18f);
            if (_armorAegisRoot != null)
            {
                float spin = Time.time * 3.8f, rad = Mathf.Lerp(1.55f, 1.15f, pct);
                for (int i = 0; i < _armorShields.Length; i++)
                {
                    if (_armorShields[i] == null) continue;
                    float a = spin + i * (Mathf.PI * 0.5f);
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    _armorShields[i].localPosition = d * rad + Vector3.up * (1.28f + 0.14f * Mathf.Sin(Time.time * 6f + i));
                    _armorShields[i].localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(24f, 0f, 0f);
                    _armorShields[i].localScale = Vector3.one * Mathf.Lerp(0.75f, 1.15f, pct);
                }
                float ringR = Mathf.Lerp(_groundRadius * 1.1f, _groundRadius * 0.62f, pct);
                float segLen = (2f * Mathf.PI * ringR / _armorRingSegs.Length) * 0.86f;
                for (int i = 0; i < _armorRingSegs.Length; i++)
                {
                    if (_armorRingSegs[i] == null) continue;
                    float a = -spin * 0.4f + (i / (float)_armorRingSegs.Length) * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    _armorRingSegs[i].localPosition = d * ringR + Vector3.up * 0.12f;
                    _armorRingSegs[i].localRotation = Quaternion.LookRotation(d);
                    _armorRingSegs[i].localScale = new Vector3(segLen, 0.10f, 0.18f);
                }
            }
            if (Time.time >= _nextSparkTime) { _nextSparkTime = Time.time + 0.11f; SpawnDustBurst(transform.position + Vector3.up * 1.1f, Vector3.up, 2, 0.55f, true); }
        }

        private static void EnsureGoldMat()
        {
            if (_goldSparkMat != null) return;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _goldSparkMat = new Material(s) { name = "Mat_ArmorGoldSpark", color = new Color(1f, 0.86f, 0.18f) };
            if (_goldSparkMat.HasProperty("_EmissionColor")) { _goldSparkMat.EnableKeyword("_EMISSION"); _goldSparkMat.SetColor("_EmissionColor", new Color(2.6f, 1.95f, 0.25f)); }
        }

        private void SpawnDustBurst(Vector3 origin, Vector3 dir, int count, float spread, bool isGold = false)
        {
            if (_dustMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _dustMat = new Material(s) { name = "Mat_CaveTelegraphDust", color = new Color(0.36f, 0.31f, 0.26f, 1f) };
                if (_dustMat.HasProperty("_Smoothness")) _dustMat.SetFloat("_Smoothness", 0.08f);
            }
            if (isGold) EnsureGoldMat();
            Material mat = isGold ? _goldSparkMat : _dustMat;
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                if (go.TryGetComponent<Collider>(out var c)) Destroy(c);
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                float sc = Random.Range(isGold ? 0.06f : 0.05f, isGold ? 0.12f : 0.10f);
                go.transform.position = origin + new Vector3(Random.Range(-spread, spread), isGold ? 0.25f : 0.08f, Random.Range(-spread, spread));
                go.transform.localScale = Vector3.one * sc;
                Vector3 v = (dir.normalized * (isGold ? 1.1f : 0.4f) + new Vector3(Random.Range(-1f, 1f), Random.Range(0.7f, 1.5f), Random.Range(-1f, 1f))) * Random.Range(0.9f, 1.8f);
                _motes.Add(new DustMote { Tr = go.transform, Vel = v, Life = 0f, MaxLife = Random.Range(0.28f, 0.45f), BaseScale = sc });
            }
        }

        private void UpdateDustMotes()
        {
            for (int i = _motes.Count - 1; i >= 0; i--)
            {
                var m = _motes[i]; m.Life += Time.deltaTime;
                if (m.Tr == null || m.Life >= m.MaxLife) { if (m.Tr != null) Destroy(m.Tr.gameObject); _motes.RemoveAt(i); continue; }
                m.Vel.y -= 4.5f * Time.deltaTime; m.Tr.position += m.Vel * Time.deltaTime;
                m.Tr.localScale = Vector3.one * (m.BaseScale * (1f - m.Life / m.MaxLife)); _motes[i] = m;
            }
        }

        public void StopVisuals()
        {
            _isArmored = false; SetArmorAegisVisible(false);
            if (!_active && !_inStrikeFollowThrough) return;
            _active = false; _inStrikeFollowThrough = false; RestoreRig();
        }

        private void RestoreRig() { if (_visualChild != null) { _visualChild.localRotation = _baseChildRot; _visualChild.localPosition = _baseChildPos; _visualChild.localScale = _baseChildScale; } }

        private void OnDestroy() { StopVisuals(); for (int i = 0; i < _motes.Count; i++) if (_motes[i].Tr != null) Destroy(_motes[i].Tr.gameObject); _motes.Clear(); }
    }
}
