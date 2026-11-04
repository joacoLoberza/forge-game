using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Barrera física y visual entre zonas de la cueva minera.
    /// Permanece cerrada hasta derrotar a los enemigos de la zona actual y luego desciende al suelo.
    /// </summary>
    public class CaveZoneGateController : MonoBehaviour
    {
        [SerializeField] private int zoneIndex = 1;
        [SerializeField] private string currentZoneName = "Carbón";
        [SerializeField] private string nextZoneName = "Cobre";
        [SerializeField] private float zoneMinZ = 26f;
        [SerializeField] private float zoneMaxZ = 48f;
        [SerializeField] private List<GameObject> zoneEnemies = new();

        private BoxCollider _blockerCollider;
        private Transform _barsContainer;
        private GameObject _energyVeil;
        private Light _sealLight;
        private int _initialEnemyCount = 1;
        private int _remainingEnemies = 1;
        private bool _isUnlocked;
        private bool _hasSeenLivingEnemy;
        private float _awakeTime;

        public int ZoneIndex => zoneIndex;
        public bool IsUnlocked => _isUnlocked;
        public int RemainingEnemies => _remainingEnemies;

        private void Awake()
        {
            _awakeTime = Time.time;
            ResolveGateComponents();
            if (_barsContainer == null) BuildGateVisualsAndCollider();
            else ResetGateLockVisuals();
        }

        public void InitializeGate(int zone, string currentMineral, string nextMineral, float gateZ, float minZ, float maxZ, List<GameObject> enemies) =>
            SetupGate(zone, currentMineral, nextMineral, gateZ, minZ, maxZ, enemies, rebuildVisuals: true);

        public void RebindZone(int zone, string currentMineral, string nextMineral, float gateZ, float minZ, float maxZ, List<GameObject> enemies) =>
            SetupGate(zone, currentMineral, nextMineral, gateZ, minZ, maxZ, enemies, rebuildVisuals: false);

        private void SetupGate(int zone, string currentMineral, string nextMineral, float gateZ, float minZ, float maxZ, List<GameObject> enemies, bool rebuildVisuals)
        {
            zoneIndex = zone;
            currentZoneName = currentMineral;
            nextZoneName = nextMineral;
            zoneMinZ = minZ;
            zoneMaxZ = maxZ;
            transform.SetPositionAndRotation(new Vector3(0f, 0f, gateZ), Quaternion.identity);

            zoneEnemies.Clear();
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i] != null && !zoneEnemies.Contains(enemies[i])) zoneEnemies.Add(enemies[i]);
            }
            _initialEnemyCount = Mathf.Max(1, zoneEnemies.Count);
            _remainingEnemies = zoneEnemies.Count;
            _isUnlocked = false;
            _hasSeenLivingEnemy = _remainingEnemies > 0;
            _awakeTime = Time.time;

            ResolveGateComponents();
            if (rebuildVisuals || _barsContainer == null) BuildGateVisualsAndCollider();
            else ResetGateLockVisuals();
        }

        private void ResolveGateComponents()
        {
            if (_blockerCollider == null) _blockerCollider = GetComponent<BoxCollider>();
            if (_barsContainer == null) _barsContainer = transform.Find("GateMonolithBars");
            if (_energyVeil == null && _barsContainer != null)
            {
                Transform v = _barsContainer.Find("GateRunicVeil");
                if (v != null) _energyVeil = v.gameObject;
            }
            if (_sealLight == null) _sealLight = GetComponentInChildren<Light>(true);
        }

        private void ResetGateLockVisuals()
        {
            if (_blockerCollider != null) _blockerCollider.enabled = true;
            if (_barsContainer != null) _barsContainer.localPosition = Vector3.zero;
            if (_energyVeil != null) _energyVeil.SetActive(true);
            if (_sealLight != null) { _sealLight.color = new Color(1.0f, 0.25f, 0.15f); _sealLight.intensity = 3.2f; }
        }

        private void BuildGateVisualsAndCollider()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(ch); else DestroyImmediate(ch);
            }

            _blockerCollider = GetComponent<BoxCollider>();
            if (_blockerCollider == null) _blockerCollider = gameObject.AddComponent<BoxCollider>();
            _blockerCollider.isTrigger = false;
            _blockerCollider.center = new Vector3(0f, 4.0f, 0f);
            _blockerCollider.size = new Vector3(22.0f, 8.5f, 0.95f);
            _blockerCollider.enabled = true;

            _barsContainer = new GameObject("GateMonolithBars").transform;
            _barsContainer.SetParent(transform, false);
            _barsContainer.localPosition = Vector3.zero;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            Material barMat = new Material(lit) { name = "Mat_GateIronStone" };
            Color barCol = new Color(0.16f, 0.14f, 0.13f, 1f);
            barMat.color = barCol;
            if (barMat.HasProperty("_BaseColor")) barMat.SetColor("_BaseColor", barCol);
            if (barMat.HasProperty("_Metallic")) barMat.SetFloat("_Metallic", 0.55f);
            if (barMat.HasProperty("_Smoothness")) barMat.SetFloat("_Smoothness", 0.35f);

            const int barCount = 9;
            float halfW = CaveDungeonBuilder.GetHalfWidthAtZ(transform.position.z) - 0.8f;
            for (int i = 0; i < barCount; i++)
            {
                float x = Mathf.Lerp(-halfW, halfW, i / (float)(barCount - 1));
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"GateBar_{i}";
                bar.transform.SetParent(_barsContainer, false);
                bar.transform.localPosition = new Vector3(x, 3.6f, 0f);
                bar.transform.localScale = new Vector3(0.52f, 7.4f, 0.52f);
                var c = bar.GetComponent<Collider>();
                if (c != null) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
                bar.GetComponent<MeshRenderer>().sharedMaterial = barMat;
            }

            _energyVeil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _energyVeil.name = "GateRunicVeil";
            _energyVeil.transform.SetParent(_barsContainer, false);
            _energyVeil.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            _energyVeil.transform.localScale = new Vector3(halfW * 2.0f, 6.8f, 0.12f);
            var vc = _energyVeil.GetComponent<Collider>();
            if (vc != null) { if (Application.isPlaying) Destroy(vc); else DestroyImmediate(vc); }

            Material veilMat = new Material(lit) { name = "Mat_GateSealGlow" };
            Color sealRed = new Color(0.85f, 0.16f, 0.12f, 1f);
            veilMat.color = sealRed;
            if (veilMat.HasProperty("_BaseColor")) veilMat.SetColor("_BaseColor", sealRed);
            if (veilMat.HasProperty("_EmissionColor")) { veilMat.EnableKeyword("_EMISSION"); veilMat.SetColor("_EmissionColor", sealRed * 1.4f); }
            _energyVeil.GetComponent<MeshRenderer>().sharedMaterial = veilMat;

            var lObj = new GameObject("GateSealLight", typeof(Light));
            lObj.transform.SetParent(transform, false);
            lObj.transform.localPosition = new Vector3(0f, 3.2f, -0.8f);
            _sealLight = lObj.GetComponent<Light>();
            _sealLight.type = LightType.Point;
            _sealLight.color = new Color(1.0f, 0.25f, 0.15f);
            _sealLight.range = 14f;
            _sealLight.intensity = 3.2f;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            ResolveGateComponents();

            if (!_isUnlocked)
            {
                int alive = CountLivingZoneEnemies();
                _remainingEnemies = alive;
                if (alive > 0)
                {
                    _hasSeenLivingEnemy = true;
                    if (alive > _initialEnemyCount) _initialEnemyCount = alive;
                }
                else if (_hasSeenLivingEnemy || (Time.time - _awakeTime) >= 0.35f)
                {
                    UnlockGate();
                }
            }
            else
            {
                if (_blockerCollider != null && _blockerCollider.enabled) _blockerCollider.enabled = false;
                if (_energyVeil != null && _energyVeil.activeSelf) _energyVeil.SetActive(false);
                if (_barsContainer != null && _barsContainer.localPosition.y > -7.8f)
                    _barsContainer.localPosition = Vector3.MoveTowards(_barsContainer.localPosition, new Vector3(0f, -8.0f, 0f), Time.deltaTime * 5.0f);
            }
        }

        private int CountLivingZoneEnemies()
        {
            int alive = 0;
            for (int i = zoneEnemies.Count - 1; i >= 0; i--)
            {
                var go = zoneEnemies[i];
                if (go == null) continue;
                var dmg = go.GetComponent<IDamageable>();
                if (dmg != null && dmg.IsAlive) alive++;
            }

            for (int i = 0; i < SpiderEnemyController.ActiveSpiders.Count; i++)
            {
                var sp = SpiderEnemyController.ActiveSpiders[i];
                if (sp == null || !sp.IsAlive) continue;
                float z = sp.SpawnPoint.z > 1f ? sp.SpawnPoint.z : sp.transform.position.z;
                if (z >= zoneMinZ && z <= zoneMaxZ && !zoneEnemies.Contains(sp.gameObject)) { zoneEnemies.Add(sp.gameObject); alive++; }
            }

            for (int i = 0; i < GolemEnemyController.ActiveGolems.Count; i++)
            {
                var gl = GolemEnemyController.ActiveGolems[i];
                if (gl == null || !gl.IsAlive) continue;
                float z = gl.SpawnPoint.z > 1f ? gl.SpawnPoint.z : gl.transform.position.z;
                if (z >= zoneMinZ && z <= zoneMaxZ && !zoneEnemies.Contains(gl.gameObject)) { zoneEnemies.Add(gl.gameObject); alive++; }
            }
            return alive;
        }

        private void UnlockGate()
        {
            ResolveGateComponents();
            _isUnlocked = true;
            foreach (var col in GetComponentsInChildren<Collider>(true))
                if (col != null) col.enabled = false;
            if (_energyVeil != null) _energyVeil.SetActive(false);
            if (_sealLight != null) { _sealLight.color = new Color(0.25f, 1.0f, 0.45f); _sealLight.intensity = 2.0f; }
            Debug.Log($"<color=#55FF88>[Cueva]</color> ¡Zona {zoneIndex} ({currentZoneName}) despejada! Paso abierto hacia {nextZoneName}.");
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || _isUnlocked) return;
            Transform player = SpiderEnemyController.FindClosestLivingPlayer(transform.position);
            if (player == null || Mathf.Abs(player.position.z - transform.position.z) > 8.5f || player.position.z < 24f) return;

            Rect r = new Rect((Screen.width - 580f) * 0.5f, 88f, 580f, 54f);
            GUI.Box(r, "");
            GUIStyle st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 15 };
            GUI.Label(r, $"<color=#FF6655><b>BARRERA DE ZONA {zoneIndex} ({currentZoneName}) BLOQUEADA</b></color>\n" +
                $"<color=#FFF2CC>Derrota a los enemigos de esta zona (<b>{_remainingEnemies}/{_initialEnemyCount}</b> restantes) para avanzar a <b>{nextZoneName}</b></color>", st);
        }
    }
}
