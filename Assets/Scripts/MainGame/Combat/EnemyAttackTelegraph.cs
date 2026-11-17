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
        private float _startTime, _endTime, _lungeUntil;
        private bool _active, _inStrikeFollowThrough, _dustTriggered;
        private readonly List<DustMote> _motes = new();
        private static Material _dustMat;

        public static void Begin(CaveCreatureBase owner, float windupDuration, string label, float groundRadius = 0f)
        {
            if (owner == null || !owner.IsAlive) return;
            if (!owner.TryGetComponent<EnemyAttackTelegraph>(out var t) || t == null)
                t = owner.gameObject.AddComponent<EnemyAttackTelegraph>();
            t.Activate(owner, windupDuration);
        }

        public static void Cancel(CaveCreatureBase owner)
        {
            if (owner != null && owner.TryGetComponent<EnemyAttackTelegraph>(out var t) && t != null) t.StopVisuals();
        }

        public static void EmitImpactDust(Vector3 worldPos, Vector3 forward, int count = 6, float spread = 0.65f)
        {
            var host = FindAnyOwnerInRange(worldPos);
            if (host != null) host.SpawnDustBurst(worldPos, forward, count, spread);
        }

        private static EnemyAttackTelegraph FindAnyOwnerInRange(Vector3 pos)
        {
            var all = FindObjectsByType<EnemyAttackTelegraph>(FindObjectsSortMode.None);
            EnemyAttackTelegraph best = null; float bestSq = 25f;
            for (int i = 0; i < all.Length; i++)
            {
                float sq = (all[i].transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = all[i]; }
            }
            return best;
        }

        private void Activate(CaveCreatureBase owner, float duration)
        {
            _owner = owner;
            _startTime = Time.time;
            _endTime = Time.time + Mathf.Max(0.25f, duration);
            _active = true; _inStrikeFollowThrough = false; _dustTriggered = false;
            CacheVisualRig();
            SpawnDustBurst(transform.position + transform.forward * 0.45f, transform.forward, 4, 0.38f);
        }

        private void CacheVisualRig()
        {
            if (_visualChild != null) return;
            for (int i = 0; i < transform.childCount; i++)
            {
                var ch = transform.GetChild(i);
                if (ch.name.StartsWith("Visual") || ch.GetComponentInChildren<Renderer>() != null)
                {
                    _visualChild = ch;
                    _baseChildRot = ch.localRotation; _baseChildPos = ch.localPosition; _baseChildScale = ch.localScale;
                    break;
                }
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
                SpawnDustBurst(transform.position + transform.forward * 0.75f, transform.forward, 5, 0.55f);
                return;
            }

            float pct = Mathf.Clamp01((Time.time - _startTime) / Mathf.Max(0.05f, _endTime - _startTime));
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

        private void SpawnDustBurst(Vector3 origin, Vector3 dir, int count, float spread)
        {
            if (_dustMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _dustMat = new Material(s) { name = "Mat_CaveTelegraphDust", color = new Color(0.36f, 0.31f, 0.26f, 1f) };
                if (_dustMat.HasProperty("_Smoothness")) _dustMat.SetFloat("_Smoothness", 0.08f);
            }
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                if (go.TryGetComponent<Collider>(out var c)) Destroy(c);
                go.GetComponent<MeshRenderer>().sharedMaterial = _dustMat;
                float sc = Random.Range(0.05f, 0.10f);
                go.transform.position = origin + new Vector3(Random.Range(-spread, spread), 0.08f, Random.Range(-spread, spread));
                go.transform.localScale = Vector3.one * sc;
                Vector3 v = (dir.normalized * 0.4f + new Vector3(Random.Range(-1f, 1f), Random.Range(0.7f, 1.5f), Random.Range(-1f, 1f))) * Random.Range(0.9f, 1.8f);
                _motes.Add(new DustMote { Tr = go.transform, Vel = v, Life = 0f, MaxLife = Random.Range(0.28f, 0.45f), BaseScale = sc });
            }
        }

        private void UpdateDustMotes()
        {
            for (int i = _motes.Count - 1; i >= 0; i--)
            {
                var m = _motes[i]; m.Life += Time.deltaTime;
                if (m.Tr == null || m.Life >= m.MaxLife) { if (m.Tr != null) Destroy(m.Tr.gameObject); _motes.RemoveAt(i); continue; }
                m.Vel.y -= 4.5f * Time.deltaTime;
                m.Tr.position += m.Vel * Time.deltaTime;
                m.Tr.localScale = Vector3.one * (m.BaseScale * (1f - m.Life / m.MaxLife));
                _motes[i] = m;
            }
        }

        public void StopVisuals()
        {
            if (!_active && !_inStrikeFollowThrough) return;
            _active = false; _inStrikeFollowThrough = false;
            RestoreRig();
        }

        private void RestoreRig()
        {
            if (_visualChild != null) { _visualChild.localRotation = _baseChildRot; _visualChild.localPosition = _baseChildPos; _visualChild.localScale = _baseChildScale; }
        }

        private void OnDestroy()
        {
            StopVisuals();
            for (int i = 0; i < _motes.Count; i++) if (_motes[i].Tr != null) Destroy(_motes[i].Tr.gameObject);
            _motes.Clear();
        }
    }
}
