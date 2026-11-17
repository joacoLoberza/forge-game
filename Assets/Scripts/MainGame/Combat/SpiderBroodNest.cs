using System;
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
    /// Nido de crías de araña (SpiderBroodNest) con modelo 3D orgánico procedural:
    /// montículo fibroso de seda, capullos elongados pulsantes, filamentos de telaraña
    /// tensados hacia la roca del nicho oscuro y alerta ecológica a la progenie.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class SpiderBroodNest : MonoBehaviour, IDamageable
    {
        public static readonly List<SpiderBroodNest> ActiveNests = new();
        public static event Action<Vector3> OnBroodThreatened;

        [SerializeField] private int maxHealth = 95;
        [SerializeField] private int currentHealth = 95;

        private Transform _visualRoot;
        private Light _nestGlow;
        private SphereCollider _col;
        private bool _isDestroyed;

        public bool IsAlive => !_isDestroyed && currentHealth > 0;

        private void OnEnable()
        {
            if (!ActiveNests.Contains(this)) ActiveNests.Add(this);
        }

        private void OnDisable()
        {
            ActiveNests.Remove(this);
        }

        public void InitializeNest(Vector3 worldPos)
        {
            _isDestroyed = false;
            currentHealth = maxHealth;
            float baseFloor = CaveDungeonBuilder.GetFloorY(worldPos.x, worldPos.z);
            float groundY = UnitySafeUtility.ProbeGroundY(new Vector3(worldPos.x, baseFloor, worldPos.z), transform, 1.8f, 10.0f);
            transform.position = new Vector3(worldPos.x, groundY + 0.02f, worldPos.z);

            _col = UnitySafeUtility.GetOrAdd<SphereCollider>(gameObject);
            _col.radius = 1.05f;
            _col.center = new Vector3(0f, 0.52f, 0f);
            _col.isTrigger = true;
            _col.enabled = true;

            if (!TryGetComponent<EnemyLootDropper>(out var dropper))
            {
                dropper = gameObject.AddComponent<EnemyLootDropper>();
                dropper.SetLootTable(new List<LootDropEntry>
                {
                    new LootDropEntry("Coal", 0.85f, 1, 3),
                    new LootDropEntry("Copper", 0.55f, 1, 2)
                });
            }

            BuildOrganic3DNestVisual();
        }

        public void ResetNest()
        {
            _isDestroyed = false;
            currentHealth = maxHealth;
            if (_col != null) _col.enabled = true;
            if (_visualRoot != null) _visualRoot.gameObject.SetActive(true);
            if (_nestGlow != null) _nestGlow.enabled = true;
        }

        private void Update()
        {
            if (!IsAlive || _visualRoot == null) return;
            float pulse = 1.0f + Mathf.Sin(Time.time * 2.8f + GetInstanceID()) * 0.045f;
            _visualRoot.localScale = new Vector3(pulse, 1.0f / pulse, pulse);
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Hurt);
            OnBroodThreatened?.Invoke(transform.position);
            StartCoroutine(PulseHitVisualRoutine());
            if (currentHealth <= 0) DestroyNest();
        }

        private void DestroyNest()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            if (_col != null) _col.enabled = false;
            if (_nestGlow != null) _nestGlow.enabled = false;
            if (_visualRoot != null) _visualRoot.gameObject.SetActive(false);
            GetComponent<EnemyLootDropper>()?.DropLoot(transform.position);
            OnBroodThreatened?.Invoke(transform.position);
        }

        private IEnumerator PulseHitVisualRoutine()
        {
            if (_nestGlow != null) _nestGlow.intensity = 2.1f;
            yield return new WaitForSeconds(0.14f);
            if (_nestGlow != null && IsAlive) _nestGlow.intensity = 0.55f;
        }

        private void BuildOrganic3DNestVisual()
        {
            if (_visualRoot != null) return;
            _visualRoot = new GameObject("OrganicBroodSacRoot").transform;
            _visualRoot.SetParent(transform, false);

            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material silkMat = new Material(s) { name = "Mat_SpiderSilkMound", color = new Color(0.76f, 0.74f, 0.68f, 1f) };
            if (silkMat.HasProperty("_BaseColor")) silkMat.SetColor("_BaseColor", silkMat.color);
            if (silkMat.HasProperty("_Smoothness")) silkMat.SetFloat("_Smoothness", 0.18f);

            Material cocoonMat = new Material(s) { name = "Mat_SpiderEggCocoon", color = new Color(0.84f, 0.88f, 0.76f, 1f) };
            if (cocoonMat.HasProperty("_BaseColor")) cocoonMat.SetColor("_BaseColor", cocoonMat.color);
            if (cocoonMat.HasProperty("_Smoothness")) cocoonMat.SetFloat("_Smoothness", 0.55f);
            if (cocoonMat.HasProperty("_EmissionColor"))
            {
                cocoonMat.EnableKeyword("_EMISSION");
                cocoonMat.SetColor("_EmissionColor", new Color(0.14f, 0.24f, 0.11f));
            }

            // 1) Base orgánica de seda fibrosa (malla facetada aplanada)
            CreateOrganicMeshPart("SilkMoundBase", new Vector3(0f, 0.18f, 0f), new Vector3(1.55f, 0.45f, 1.45f),
                Quaternion.identity, OreMeshGenerator.CreateFacetedChunkMesh(0.75f, 301), silkMat);
            CreateOrganicMeshPart("SilkCoreDomed", new Vector3(0f, 0.46f, 0f), new Vector3(1.05f, 0.78f, 0.98f),
                Quaternion.Euler(8f, 25f, -6f), OreMeshGenerator.CreateFacetedChunkMesh(0.58f, 307), silkMat);

            // 2) Racimo de capullos de huevos elongados orgánicos
            Vector3[] eggPos =
            {
                new(-0.42f, 0.36f, 0.28f), new(0.44f, 0.34f, -0.24f), new(0.18f, 0.58f, 0.36f),
                new(-0.26f, 0.54f, -0.34f), new(0.02f, 0.76f, -0.04f), new(0.52f, 0.24f, 0.32f),
                new(-0.54f, 0.25f, -0.18f)
            };
            for (int i = 0; i < eggPos.Length; i++)
            {
                Mesh eggMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.24f + (i % 3) * 0.04f, 410 + i * 19);
                CreateOrganicMeshPart($"EggCocoon_{i}", eggPos[i], new Vector3(0.85f, 1.42f, 0.85f),
                    Quaternion.Euler((i * 19f) % 28f - 14f, i * 47f, (i * 23f) % 24f - 12f), eggMesh, cocoonMat);
            }

            // 3) Filamentos radiales de telaraña tensados hacia el suelo y el techo bajo del nicho
            for (int w = 0; w < 8; w++)
            {
                float ang = w * Mathf.PI * 0.25f;
                Vector3 from = new Vector3(Mathf.Cos(ang) * 0.35f, 0.62f, Mathf.Sin(ang) * 0.35f);
                Vector3 to = (w % 2 == 0)
                    ? new Vector3(Mathf.Cos(ang) * 1.95f, 0.04f, Mathf.Sin(ang) * 1.95f)
                    : new Vector3(Mathf.Cos(ang) * 1.65f, 2.15f, Mathf.Sin(ang) * 1.65f);
                CreateWebStrand($"WebStrand_{w}", from, to, silkMat);
            }

            // Bioluminiscencia tenue para mantener el nicho oscuro e inquietante
            var lObj = new GameObject("BroodPulseLight", typeof(Light));
            lObj.transform.SetParent(transform, false);
            lObj.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            _nestGlow = lObj.GetComponent<Light>();
            _nestGlow.type = LightType.Point;
            _nestGlow.color = new Color(0.45f, 0.78f, 0.38f);
            _nestGlow.range = 4.2f;
            _nestGlow.intensity = 0.55f;
        }

        private void CreateOrganicMeshPart(string name, Vector3 pos, Vector3 scale, Quaternion rot, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_visualRoot, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private void CreateWebStrand(string name, Vector3 a, Vector3 b, Material mat)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = name;
            cyl.transform.SetParent(_visualRoot, false);
            Vector3 diff = b - a;
            cyl.transform.localPosition = (a + b) * 0.5f;
            cyl.transform.localRotation = Quaternion.FromToRotation(Vector3.up, diff.normalized);
            cyl.transform.localScale = new Vector3(0.028f, diff.magnitude * 0.5f, 0.028f);
            cyl.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (cyl.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
        }
    }
}
