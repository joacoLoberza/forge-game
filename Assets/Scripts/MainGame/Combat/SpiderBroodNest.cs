using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Nido de crías arácnidas ("sus hijos") protegido por las arañas cavernícolas.
    /// Genera sacos de huevos bioluminiscentes sobre seda densa y emite alertas ecológicas
    /// cuando un ser vivo pisa el nido o lo golpea.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class SpiderBroodNest : MonoBehaviour, IDamageable
    {
        public static event Action<Vector3> OnBroodThreatened;

        [SerializeField] private int maxHealth = 70;
        [SerializeField] private float respawnSeconds = 42f;

        private int _currentHealth;
        private bool _isDestroyed;
        private float _nextProximityAlertTime;
        private SphereCollider _col;
        private readonly List<Transform> _eggSacs = new();
        private static Material _webMat;
        private static Material _eggMat;

        public bool IsAlive => !_isDestroyed && _currentHealth > 0;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _col = UnitySafeUtility.GetOrAdd<SphereCollider>(gameObject);
            _col.radius = 1.05f;
            _col.center = new Vector3(0f, 0.45f, 0f);
            BuildNestVisuals();
        }

        private void Update()
        {
            if (!IsAlive || !Application.isPlaying) return;

            // Latido orgánico sutil de las crías dentro de los huevos
            float pulse = 1.0f + Mathf.Sin(Time.time * 2.8f + GetInstanceID() * 0.13f) * 0.055f;
            for (int i = 0; i < _eggSacs.Count; i++)
            {
                if (_eggSacs[i] != null)
                    _eggSacs[i].localScale = new Vector3(0.32f * pulse, 0.44f * (2f - pulse), 0.32f * pulse);
            }

            // Si un ser vivo pisa demasiado cerca de las crías, alerta a las madres cercanas
            if (Time.time >= _nextProximityAlertTime)
            {
                Transform player = CaveCreatureBase.FindClosestLivingPlayer(transform.position);
                if (player != null && (player.position - transform.position).sqrMagnitude <= 14.5f)
                {
                    _nextProximityAlertTime = Time.time + 3.6f;
                    OnBroodThreatened?.Invoke(transform.position);
                }
            }
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            ForgeAudioSystem.PlaySpiderSound(transform.position, CreatureSoundType.Hurt);
            OnBroodThreatened?.Invoke(transform.position);

            if (_currentHealth <= 0)
            {
                _isDestroyed = true;
                StartCoroutine(RespawnNestRoutine());
            }
            else StartCoroutine(ShakeEggsRoutine());
        }

        private IEnumerator ShakeEggsRoutine()
        {
            Vector3 orig = transform.localScale;
            transform.localScale = orig * 1.14f;
            yield return new WaitForSeconds(0.09f);
            transform.localScale = orig;
        }

        private IEnumerator RespawnNestRoutine()
        {
            SetNestVisible(false);
            yield return new WaitForSeconds(respawnSeconds);
            _currentHealth = maxHealth;
            _isDestroyed = false;
            SetNestVisible(true);
        }

        private void SetNestVisible(bool visible)
        {
            if (_col != null) _col.enabled = visible;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = visible;
        }

        private void BuildNestVisuals()
        {
            if (transform.Find("BroodWebBase") != null) return;
            EnsureMaterials();

            var webBase = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            webBase.name = "BroodWebBase";
            webBase.transform.SetParent(transform, false);
            webBase.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            webBase.transform.localScale = new Vector3(1.85f, 0.34f, 1.75f);
            UnitySafeUtility.DestroySafe(webBase.GetComponent<Collider>());
            webBase.GetComponent<MeshRenderer>().sharedMaterial = _webMat;

            Vector3[] offsets =
            {
                new Vector3( 0.00f, 0.32f,  0.00f),
                new Vector3(-0.36f, 0.26f,  0.24f),
                new Vector3( 0.38f, 0.25f,  0.18f),
                new Vector3(-0.22f, 0.24f, -0.34f),
                new Vector3( 0.26f, 0.25f, -0.30f)
            };

            _eggSacs.Clear();
            for (int i = 0; i < offsets.Length; i++)
            {
                var egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                egg.name = $"SpiderEggSac_{i}";
                egg.transform.SetParent(transform, false);
                egg.transform.localPosition = offsets[i];
                egg.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-14f, 14f), i * 67f, UnityEngine.Random.Range(-14f, 14f));
                egg.transform.localScale = new Vector3(0.32f, 0.44f, 0.32f);
                UnitySafeUtility.DestroySafe(egg.GetComponent<Collider>());
                egg.GetComponent<MeshRenderer>().sharedMaterial = _eggMat;
                _eggSacs.Add(egg.transform);
            }

            var glowObj = new GameObject("BroodWarmGlow", typeof(Light));
            glowObj.transform.SetParent(transform, false);
            glowObj.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            var lt = glowObj.GetComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(0.85f, 0.96f, 0.52f);
            lt.range = 4.6f;
            lt.intensity = 0.85f;
        }

        private static void EnsureMaterials()
        {
            if (_webMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _webMat = new Material(s) { name = "Mat_SpiderWebMound" };
                Color webCol = new Color(0.58f, 0.56f, 0.52f, 1f);
                if (_webMat.HasProperty("_BaseColor")) _webMat.SetColor("_BaseColor", webCol);
                if (_webMat.HasProperty("_Color")) _webMat.SetColor("_Color", webCol);
                if (_webMat.HasProperty("_Smoothness")) _webMat.SetFloat("_Smoothness", 0.12f);
            }
            if (_eggMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _eggMat = new Material(s) { name = "Mat_SpiderEggSac" };
                Color eggCol = new Color(0.84f, 0.90f, 0.62f, 1f);
                if (_eggMat.HasProperty("_BaseColor")) _eggMat.SetColor("_BaseColor", eggCol);
                if (_eggMat.HasProperty("_Color")) _eggMat.SetColor("_Color", eggCol);
                if (_eggMat.HasProperty("_EmissionColor"))
                {
                    _eggMat.EnableKeyword("_EMISSION");
                    _eggMat.SetColor("_EmissionColor", new Color(0.34f, 0.42f, 0.14f));
                }
            }
        }
    }
}

