using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;

namespace ForjaGame.Resources
{
    public enum GeneratorCreatureKind
    {
        VeinGolem,
        BroodSpider
    }

    /// <summary>
    /// Generador temporizado de criaturas cavernícolas.
    /// Nunca instancia enemigos manualmente al inicio: espera un tiempo X configurable antes de
    /// hacer emerger Golems desde su mena mineral o Arañas desde su nido de crías, y repone bajas
    /// tras un enfriamiento ecológico respetando límites globales de rendimiento.
    /// </summary>
    public class CaveCreatureGenerator : MonoBehaviour
    {
        public const int MaxGlobalActiveGolems = 7;
        public const int MaxGlobalActiveSpiders = 10;

        public static readonly List<CaveCreatureGenerator> ActiveGenerators = new();

        [Header("Configuración del Generador")]
        [SerializeField] private GeneratorCreatureKind kind = GeneratorCreatureKind.VeinGolem;
        [SerializeField] private float initialSpawnDelay = 1.5f;
        [SerializeField] private float respawnCooldown = 32f;
        [SerializeField] private int maxConcurrent = 1;
        [SerializeField] private int creatureHp = 280;
        [SerializeField] private int creatureDamage = 24;
        [SerializeField] private float creatureScale = 1.0f;
        [SerializeField] private float zoneMinZ = 29f;
        [SerializeField] private float zoneMaxZ = 224f;

        private ResourceNodeController _hostVein;
        private SpiderBroodNest _hostNest;
        private Transform _spawnParent;
        private readonly List<CaveCreatureBase> _aliveInstances = new();
        private float _nextSpawnTime;
        private bool _configured;

        private void Awake()
        {
            if (_hostVein == null) _hostVein = GetComponent<ResourceNodeController>();
            if (_hostNest == null) _hostNest = GetComponent<SpiderBroodNest>();
            if (_spawnParent == null) _spawnParent = transform.parent;
            _configured = true;
            if (_nextSpawnTime <= 0f) _nextSpawnTime = Time.time + initialSpawnDelay;
        }

        private void OnEnable()
        {
            if (!ActiveGenerators.Contains(this)) ActiveGenerators.Add(this);
        }

        private void OnDisable() => ActiveGenerators.Remove(this);

        public void ConfigureVeinGolemGenerator(
            ResourceNodeController vein, Transform container, float firstDelay, float respawnDelay,
            int hp, int dmg, float scaleMult, float minZ, float maxZ)
        {
            kind = GeneratorCreatureKind.VeinGolem;
            _hostVein = vein;
            _spawnParent = container;
            initialSpawnDelay = Mathf.Max(1.0f, firstDelay);
            respawnCooldown = Mathf.Max(10f, respawnDelay);
            maxConcurrent = 1;
            creatureHp = hp;
            creatureDamage = dmg;
            creatureScale = scaleMult;
            zoneMinZ = minZ;
            zoneMaxZ = maxZ;
            _configured = true;
            ResetGeneratorTimer();
        }

        public void ConfigureBroodSpiderGenerator(
            SpiderBroodNest nest, Transform container, float firstDelay, float respawnDelay,
            int maxAlive, int hp, int biteDmg, float legSpan, float minZ, float maxZ)
        {
            kind = GeneratorCreatureKind.BroodSpider;
            _hostNest = nest;
            _spawnParent = container;
            initialSpawnDelay = Mathf.Max(1.0f, firstDelay);
            respawnCooldown = Mathf.Max(10f, respawnDelay);
            maxConcurrent = Mathf.Clamp(maxAlive, 1, 2);
            creatureHp = hp;
            creatureDamage = biteDmg;
            creatureScale = legSpan;
            zoneMinZ = minZ;
            zoneMaxZ = maxZ;
            _configured = true;
            ResetGeneratorTimer();
        }

        public void ResetGeneratorTimer()
        {
            PurgeSpawnedCreatures();
            _nextSpawnTime = Time.time + initialSpawnDelay;
        }

        public void PurgeSpawnedCreatures()
        {
            for (int i = _aliveInstances.Count - 1; i >= 0; i--)
            {
                if (_aliveInstances[i] != null)
                    UnitySafeUtility.DestroyGameObjectSafe(_aliveInstances[i].gameObject);
            }
            _aliveInstances.Clear();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_configured || GameLoopController.IsEndScreenOpen) return;

            _aliveInstances.RemoveAll(c => c == null || !c.IsAlive);
            if (_aliveInstances.Count >= maxConcurrent) return;

            if (Time.time < _nextSpawnTime) return;

            // Verificar que la mena o el nido anfitrión estén activos antes de emerger
            if (kind == GeneratorCreatureKind.VeinGolem && _hostVein != null && !_hostVein.IsAlive)
            {
                _nextSpawnTime = Time.time + 5.0f;
                return;
            }
            if (kind == GeneratorCreatureKind.BroodSpider && _hostNest != null && !_hostNest.IsAlive)
            {
                _nextSpawnTime = Time.time + 6.0f;
                return;
            }

            if (!HasGlobalCapacity())
            {
                _nextSpawnTime = Time.time + 4.5f;
                return;
            }

            SpawnGeneratedCreature();
            _nextSpawnTime = Time.time + respawnCooldown;
        }

        private bool HasGlobalCapacity() => kind == GeneratorCreatureKind.VeinGolem
            ? GolemEnemyController.ActiveGolems.Count < MaxGlobalActiveGolems
            : SpiderEnemyController.ActiveSpiders.Count < MaxGlobalActiveSpiders;

        private void SpawnGeneratedCreature()
        {
            Vector3 spawnPos = ComputeSafeEmergencePoint();
            CaveCreatureBase spawned;

            if (kind == GeneratorCreatureKind.VeinGolem)
            {
                var go = new GameObject($"GeneratedGolem_{name}");
                if (_spawnParent != null) go.transform.SetParent(_spawnParent, true);
                go.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                var ctrl = go.AddComponent<GolemEnemyController>();
                ctrl.ConfigureGolem(spawnPos, creatureHp, creatureDamage, creatureScale, zoneMinZ, zoneMaxZ, _hostVein);
                ForgeAudioSystem.PlayGolemSound(spawnPos, CreatureSoundType.Feed);
                spawned = ctrl;
            }
            else
            {
                var go = new GameObject($"GeneratedSpider_{name}");
                if (_spawnParent != null) go.transform.SetParent(_spawnParent, true);
                go.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                var ctrl = go.AddComponent<SpiderEnemyController>();
                ctrl.ConfigureSpider(spawnPos, creatureHp, creatureDamage, creatureScale, zoneMinZ, zoneMaxZ, _hostNest);
                ForgeAudioSystem.PlaySpiderSound(spawnPos, CreatureSoundType.Feed);
                spawned = ctrl;
            }

            spawned.CreatureDied += HandleCreatureDied;
            _aliveInstances.Add(spawned);
            StartCoroutine(EmergenceScaleRoutine(spawned.transform));
        }

        private void HandleCreatureDied(CaveCreatureBase dead)
        {
            _aliveInstances.Remove(dead);
            _nextSpawnTime = Mathf.Max(_nextSpawnTime, Time.time + respawnCooldown);
        }

        private Vector3 ComputeSafeEmergencePoint()
        {
            Vector3 anchor = transform.position;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float offset = kind == GeneratorCreatureKind.VeinGolem ? 1.55f : 1.15f;
            float z = Mathf.Clamp(anchor.z + Mathf.Sin(ang) * offset, CaveDungeonBuilder.CaveEntranceZ + 2f, CaveDungeonBuilder.CaveEndZ - 2.5f);
            float sx = CaveDungeonBuilder.GetSpineX(z);
            CaveDungeonBuilder.GetWallWidthsAtZ(z, out float lw, out float rw);
            float x = Mathf.Clamp(anchor.x + Mathf.Cos(ang) * offset, sx - Mathf.Max(1.6f, lw - 1.8f), sx + Mathf.Max(1.6f, rw - 1.8f));
            return new Vector3(x, CaveDungeonBuilder.GetFloorY(x, z) + 0.05f, z);
        }

        private static IEnumerator EmergenceScaleRoutine(Transform tr)
        {
            if (tr == null) yield break;
            Vector3 targetScale = tr.localScale;
            for (float t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                if (tr == null) yield break;
                tr.localScale = targetScale * Mathf.SmoothStep(0.25f, 1.0f, t / 0.55f);
                yield return null;
            }
            if (tr != null) tr.localScale = targetScale;
        }
    }
}

