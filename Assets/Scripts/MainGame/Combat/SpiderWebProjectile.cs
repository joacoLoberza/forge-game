using System.Collections;
using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Proyectil de telaraña viscosa disparado por arañas cavernícolas.
    /// Al impactar, inflige daño leve, proyecta una red de seda orgánica exclusivamente sobre el suelo
    /// y desata un efecto de ralentización severo (35% velocidad, sin sprint) durante varios segundos.
    /// </summary>
    public sealed class SpiderWebProjectile : MonoBehaviour
    {
        private Vector3 _velocity;
        private int _damage = 1;
        private float _lifetime = 4.0f;
        private bool _hasImpacted;

        public static void Shoot(Vector3 origin, Vector3 target, int damage)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpiderWebSpit";
            go.transform.position = origin;
            go.transform.localScale = new Vector3(0.32f, 0.30f, 0.56f);

            var col = go.GetComponent<SphereCollider>();
            if (col != null) col.isTrigger = true;

            Material mat = DestructibleTunnelWallBuilder.GetWebMaterial();
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.24f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0.03f;
            trail.material = mat;
            trail.startColor = new Color(0.88f, 0.96f, 0.86f, 0.85f);
            trail.endColor = new Color(0.65f, 0.78f, 0.62f, 0f);

            Vector3 toTarget = (target + Vector3.up * 0.55f) - origin;
            float dist = toTarget.magnitude;
            float travelTime = Mathf.Clamp(dist / 15.5f, 0.20f, 0.80f);
            Vector3 horizVel = new Vector3(toTarget.x, 0f, toTarget.z) / travelTime;
            float vertVel = (toTarget.y + 0.5f * 9.81f * travelTime * travelTime) / travelTime;

            var proj = go.AddComponent<SpiderWebProjectile>();
            proj._velocity = horizVel + Vector3.up * vertVel;
            proj._damage = Mathf.Clamp(damage, 1, 10);
            if (proj._velocity.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(proj._velocity);
        }

        private void Update()
        {
            if (_hasImpacted) return;
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f) { Destroy(gameObject); return; }

            _velocity.y -= 9.81f * Time.deltaTime;
            Vector3 step = _velocity * Time.deltaTime;

            if (Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude + 0.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                OnWebImpact(hit.collider, hit.point, hit.normal);
                return;
            }

            transform.position += step;
            if (_velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(_velocity);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasImpacted || other.isTrigger) return;
            if (other.GetComponentInParent<SpiderEnemyController>() != null) return;
            OnWebImpact(other, transform.position, Vector3.up);
        }

        private void OnWebImpact(Collider hitCol, Vector3 point, Vector3 normal)
        {
            _hasImpacted = true;
            Audio.ForgeAudioSystem.PlaySpiderSound(point, Audio.CreatureSoundType.Attack);

            bool isVictory = GameStateController.State == GameState.Victory || GameLoopController.IsEndScreenOpen;

            // Daño y ralentización directa al impactar jugadores (máximo 1 segundo de duración)
            if (!isVictory)
            {
                var hitPlayer = hitCol != null ? hitCol.GetComponentInParent<PlayerHealthController>() : null;
                if (hitPlayer != null && hitPlayer.IsAlive)
                {
                    if (!hitPlayer.TakeDamageFrom(_damage, null, true))
                        hitPlayer.GetComponent<PlayerMovement>()?.ApplySlowdown(0.38f, 2.2f);
                }
                foreach (var p in PlayerHealthController.AllPlayers)
                {
                    if (p != null && p != hitPlayer && p.IsAlive && (p.transform.position - point).sqrMagnitude <= 5.5f)
                    {
                        if (!p.TakeDamageFrom(_damage, null, true))
                            p.GetComponent<PlayerMovement>()?.ApplySlowdown(0.38f, 2.2f);
                    }
                }
            }

            // Proyectar el charco de telaraña SIEMPRE sobre el suelo (nunca flotando en el aire)
            Vector3 groundPoint = point;
            Vector3 groundNorm = Vector3.up;
            if (Physics.Raycast(point + Vector3.up * 1.5f, Vector3.down, out RaycastHit fHit, 14f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundPoint = fHit.point;
                groundNorm = fHit.normal;
            }
            else
            {
                float baseFloorY = CaveDungeonBuilder.GetFloorY(point.x, point.z);
                groundPoint = new Vector3(point.x, baseFloorY, point.z);
            }

            SpawnGroundOrganicWeb(groundPoint, groundNorm);
            Destroy(gameObject);
        }

        private static void SpawnGroundOrganicWeb(Vector3 groundPos, Vector3 groundNormal)
        {
            var webRoot = new GameObject("GroundOrganicSpiderWeb");
            webRoot.transform.position = groundPos + Vector3.up * 0.02f;
            webRoot.transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);

            var col = webRoot.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(3.6f, 2.2f, 3.6f);
            col.center = new Vector3(0f, 1.0f, 0f); // Volumen vertical alto para interceptar la cápsula del jugador

            Material webMat = DestructibleTunnelWallBuilder.GetWebMaterial();

            // Construcción procedural de telaraña radial en estrella sobre el terreno
            int spokes = 10;
            float radius = 1.75f;
            for (int i = 0; i < spokes; i++)
            {
                float ang = (i / (float)spokes) * Mathf.PI * 2f;
                Vector3 endLocal = new Vector3(Mathf.Cos(ang) * radius, 0.015f, Mathf.Sin(ang) * radius);

                var spoke = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spoke.name = $"WebSpoke_{i}";
                spoke.transform.SetParent(webRoot.transform, false);
                UnitySafeUtility.DestroySafe(spoke.GetComponent<Collider>());
                if (webMat != null) spoke.GetComponent<MeshRenderer>().sharedMaterial = webMat;

                Vector3 mid = endLocal * 0.5f;
                spoke.transform.localPosition = mid;
                spoke.transform.localRotation = Quaternion.FromToRotation(Vector3.up, endLocal.normalized);
                spoke.transform.localScale = new Vector3(0.045f, endLocal.magnitude * 0.5f, 0.045f);
            }

            // Membrana central orgánica
            var core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            core.name = "WebCoreMesh";
            core.transform.SetParent(webRoot.transform, false);
            UnitySafeUtility.DestroySafe(core.GetComponent<Collider>());
            if (webMat != null) core.GetComponent<MeshRenderer>().sharedMaterial = webMat;
            core.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            core.transform.localScale = new Vector3(1.35f, 0.005f, 1.35f);

            webRoot.AddComponent<StickyWebPuddleRunner>();
        }

        private sealed class StickyWebPuddleRunner : MonoBehaviour
        {
            private float _timer = 1.8f;
            private bool _hasSnared;

            private void Update()
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f) Destroy(gameObject);
            }

            private void OnTriggerEnter(Collider other)
            {
                if (_hasSnared) return;
                var pm = other.GetComponentInParent<PlayerMovement>();
                if (pm != null)
                {
                    _hasSnared = true;
                    pm.ApplySlowdown(0.40f, 1.0f);
                    Destroy(gameObject, 0.35f);
                }
            }
        }
    }
}
