using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Proyectil de roca lítica arrojado a distancia por los Gólems cuando el jugador intenta mantener distancia.
    /// Describe una parábola física y explota al impactar contra el jugador o el suelo de la caverna.
    /// </summary>
    public sealed class GolemBoulderProjectile : MonoBehaviour
    {
        private Vector3 _velocity;
        private int _damage;
        private CaveCreatureBase _owner;
        private float _life = 4.2f;
        private bool _detonated;

        public static void Launch(Vector3 origin, Vector3 targetPos, int damage, Color coreColor, CaveCreatureBase owner)
        {
            var go = new GameObject("GolemThrownBoulder", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.position = origin;
            go.GetComponent<MeshFilter>().sharedMesh = ForjaGame.Resources.OreMeshGenerator.CreateCrystalSpireMesh(0.42f, 0.72f, 517);

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(sh) { color = Color.Lerp(new Color(0.36f, 0.34f, 0.32f), coreColor, 0.30f) };
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", coreColor * 1.1f);
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            Vector3 aim = targetPos + Vector3.up * 0.65f;
            Vector3 diff = aim - origin;
            float dist = new Vector2(diff.x, diff.z).magnitude;
            float flightT = Mathf.Clamp(dist / 13.5f, 0.32f, 1.05f);
            Vector3 horiz = new Vector3(diff.x, 0f, diff.z) / flightT;
            float vy = (diff.y + 0.5f * 13.5f * flightT * flightT) / flightT;

            var proj = go.AddComponent<GolemBoulderProjectile>();
            proj._velocity = horiz + Vector3.up * vy;
            proj._damage = Mathf.Max(4, damage);
            proj._owner = owner;
        }

        private void Update()
        {
            if (_detonated) return;
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }

            _velocity.y -= 13.5f * Time.deltaTime;
            Vector3 step = _velocity * Time.deltaTime;
            transform.Rotate(240f * Time.deltaTime, 165f * Time.deltaTime, 90f * Time.deltaTime);

            float stepDist = step.magnitude;
            if (stepDist > 0.001f && Physics.SphereCast(transform.position, 0.34f, step / stepDist, out RaycastHit hit, stepDist + 0.12f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && (_owner == null || !hit.collider.transform.IsChildOf(_owner.transform)))
                {
                    Detonate(hit.point);
                    return;
                }
            }
            transform.position += step;
        }

        private void Detonate(Vector3 point)
        {
            if (_detonated) return;
            _detonated = true;

            ForgeAudioSystem.PlayGolemSound(point, CreatureSoundType.Attack);
            EnemyAttackTelegraph.EmitImpactDust(point, Vector3.up, 7, 0.65f);
            PlayerCameraFollow.Instance?.TriggerImpulse(0.10f);

            foreach (var p in PlayerHealthController.AllPlayers)
            {
                if (p == null || !p.IsAlive) continue;
                Vector3 d = p.transform.position - point;
                if (Mathf.Abs(d.y) > 2.0f) continue;
                d.y = 0f;
                if (d.sqrMagnitude <= 1.85f * 1.85f)
                    p.TakeDamageFrom(_damage, _owner, isRanged: true);
            }
            Destroy(gameObject);
        }
    }
}

