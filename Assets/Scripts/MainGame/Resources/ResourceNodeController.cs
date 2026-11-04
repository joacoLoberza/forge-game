using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Forge;
using ForjaGame.Audio;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Veta o yacimiento mineral en el mundo. Notifica perturbaciones territoriales a la fauna
    /// subterránea al ser picada y permite alimentación ecológica sin soltar pepitas al suelo.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ResourceNodeController : MonoBehaviour, IDamageable
    {
        public static event Action<ResourceNodeController, GameObject> OnVeinDisturbed;

        [Header("Configuración de Yacimiento")]
        [SerializeField] private string itemId = "Iron";
        [SerializeField] private int minDropAmount = 2;
        [SerializeField] private int maxDropAmount = 4;
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private GameObject droppedItemPrefab;
        [SerializeField] private bool requiresPickaxe = true;
        [SerializeField] private bool autoRespawn = true;
        [SerializeField] private float respawnTime = 30f;

        private int _currentHealth;
        private bool _isDestroyed;
        private Vector3 _originalScale;
        private Coroutine _hitFeedbackRoutine;
        private readonly List<GameObject> _clusters = new();

        public bool IsAlive => !_isDestroyed && _currentHealth > 0;
        public string ItemId => itemId;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            _currentHealth = Mathf.Max(1, maxHealth);
            _originalScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
            EnsureSolidCollider();
            EnsureVisuals();
        }

        private void OnEnable()
        {
            EnsureSolidCollider();
            EnsureVisuals();
        }

        private void Reset() => EnsureSolidCollider();

        public void EnsureSolidCollider()
        {
            if (!TryGetComponent<BoxCollider>(out var box))
            {
                box = gameObject.AddComponent<BoxCollider>();
            }
            if (box != null)
            {
                box.center = new Vector3(0f, 0.60f, 0f);
                box.size = new Vector3(2.2f, 1.6f, 2.2f);
            }
        }

        public void EnsureVisuals()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null) ForjaGame.Core.UnitySafeUtility.DestroySafe(mf);
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) ForjaGame.Core.UnitySafeUtility.DestroySafe(mr);

            if (transform.childCount == 0) BuildVisuals();
            else if (_clusters.Count == 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var ch = transform.GetChild(i).gameObject;
                    if (ch.name.Contains("Spire") || ch.name.Contains("Nodule") || ch.name.Contains("Strata") || ch.name.Contains("Nugget") || ch.name.Contains("Chunk"))
                        _clusters.Add(ch);
                }
            }
        }

        public void BuildVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject ch = transform.GetChild(i).gameObject;
                ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(ch);
            }
            _clusters.Clear();
            ResourceVisualBuilder.CreateVeinVisual(gameObject, itemId, out var list);
            _clusters.AddRange(list);
            UpdateDamageVisualHint();
        }

        public void InitializeNode(string id, int health, int minDrop, int maxDrop, bool needPickaxe = true)
        {
            itemId = id;
            maxHealth = health;
            _currentHealth = health;
            minDropAmount = minDrop;
            maxDropAmount = maxDrop;
            requiresPickaxe = needPickaxe;
            _isDestroyed = false;
            BuildVisuals();
        }

        public void TakeDamage(int damage) => TakeDamageFrom(damage, null);

        public void TakeDamageFrom(int damage, GameObject attacker)
        {
            if (_currentHealth <= 0 && !_isDestroyed) _currentHealth = Mathf.Max(1, maxHealth);
            if (!IsAlive || damage <= 0) return;

            if (requiresPickaxe && attacker != null)
            {
                var attack = attacker.GetComponentInParent<AttackController>() ?? attacker.GetComponentInChildren<AttackController>();
                var equip = attacker.GetComponentInParent<PlayerEquipmentController>() ?? attacker.GetComponentInChildren<PlayerEquipmentController>();
                bool hasPickaxe = (attack != null && attack.HasPickaxeEquipped) ||
                                  (equip != null && equip.CurrentWeapon != null && equip.CurrentWeapon.weaponType == WeaponType.Pickaxe);

                if (!hasPickaxe)
                {
                    ForgeAudioSystem.PlayHitImpact(transform.position, isStoneOrArmor: true, isHeavy: false);
                    TriggerHitShake(false);
                    return;
                }
            }

            _currentHealth = Mathf.Max(0, _currentHealth - 1);
            bool depleted = _currentHealth <= 0;
            ForgeAudioSystem.PlayPickaxeStrike(transform.position, depleted);
            OnVeinDisturbed?.Invoke(this, attacker);

            TriggerHitShake(true);
            UpdateDamageVisualHint();

            if (!depleted) return;
            _isDestroyed = true;
            try { SpawnDrops(); }
            catch (Exception ex) { Debug.LogError($"[Veta Mineral] Error en SpawnDrops: {ex}"); }

            if (autoRespawn && Application.isPlaying) StartCoroutine(RespawnRoutine());
            else if (!autoRespawn) ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(gameObject);
        }

        /// <summary>
        /// Permite al Golem custodio absorber resonancia lítica de la mena sin agotarla ni destruir minerales preciosos.
        /// </summary>
        public bool ConsumeEcologicalBite()
        {
            if (!IsAlive) return false;
            TriggerHitShake(false);
            return true;
        }

        private void UpdateDamageVisualHint()
        {
            if (_clusters.Count == 0) return;
            float hpRatio = (float)_currentHealth / Mathf.Max(1, maxHealth);
            int keepActiveCount = Mathf.CeilToInt(_clusters.Count * hpRatio);
            for (int i = 0; i < _clusters.Count; i++)
                if (_clusters[i] != null) _clusters[i].SetActive(i < keepActiveCount);
        }

        private void TriggerHitShake(bool validHit = true)
        {
            if (!Application.isPlaying) return;
            if (_hitFeedbackRoutine != null) StopCoroutine(_hitFeedbackRoutine);
            _hitFeedbackRoutine = StartCoroutine(HitShakeRoutine(validHit));
        }

        private IEnumerator HitShakeRoutine(bool validHit)
        {
            Vector3 baseScale = _originalScale;
            transform.localScale = baseScale * (validHit ? 1.22f : 1.06f);
            yield return new WaitForSeconds(0.09f);
            transform.localScale = baseScale * 0.94f;
            yield return new WaitForSeconds(0.07f);
            transform.localScale = baseScale;
        }

        private void SpawnDrops()
        {
            if (!Application.isPlaying) return;
            int totalDrops = UnityEngine.Random.Range(minDropAmount, maxDropAmount + 1);
            Vector3 basePos = transform.position + Vector3.up * 0.5f;

            for (int i = 0; i < totalDrops; i++)
            {
                Vector2 circle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.20f, 0.50f);
                Vector3 spawnPos = basePos + new Vector3(circle.x, 0.15f, circle.y);
                Vector3 burstVelocity = new Vector3(circle.x * 1.15f, UnityEngine.Random.Range(2.2f, 3.2f), circle.y * 1.15f);

                if (droppedItemPrefab != null)
                {
                    GameObject dropObj = Instantiate(droppedItemPrefab, spawnPos, Quaternion.identity);
                    if (!dropObj.TryGetComponent<DroppedItemController>(out var dropCtrl))
                        dropCtrl = dropObj.AddComponent<DroppedItemController>();
                    dropCtrl.Initialize(itemId, 1);
                    dropCtrl.ApplyPopVelocity(burstVelocity);
                }
                else DroppedItemController.Spawn(spawnPos, itemId, 1, burstVelocity);
            }
        }

        private IEnumerator RespawnRoutine()
        {
            SetVisualsActive(false);
            yield return new WaitForSeconds(respawnTime);
            _currentHealth = maxHealth;
            _isDestroyed = false;
            SetVisualsActive(true);
            UpdateDamageVisualHint();
            TriggerHitShake();
        }

        private void SetVisualsActive(bool active)
        {
            if (TryGetComponent<Collider>(out var col)) col.enabled = active;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = active;
            foreach (var l in GetComponentsInChildren<Light>(true)) l.enabled = active;
        }
    }
}
