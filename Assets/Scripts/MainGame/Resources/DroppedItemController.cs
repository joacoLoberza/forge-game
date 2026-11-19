using UnityEngine;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Inventory;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Ítem coleccionable físico que flota en el mundo al romper menas o derrotar enemigos.
    /// Cuenta con soporte para desniveles bajo cero (fosas de la mina) y recolección exclusiva por jugadores.
    /// </summary>
    public class DroppedItemController : MonoBehaviour
    {
        [SerializeField] private string itemId = "Iron";
        [SerializeField] private int amount = 1;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 2.5f;
        [SerializeField] private float rotationSpeed = 65f;
        [SerializeField] private float magnetRadius = 2.6f;
        [SerializeField] private float magnetSpeed = 9.5f;
        [SerializeField] private float pickupDelay = 0.35f;

        private bool _pickedUp;
        private float _baseY = 0.35f;
        private bool _hasBaseY;
        private float _spawnTime;
        private Vector3 _popVelocity;
        private Transform _targetPlayer;

        public string ItemId => itemId;
        public int Amount => amount;

        public static DroppedItemController Spawn(Vector3 position, string id, int qty, Vector3? popVel = null)
        {
            GameObject itemGo;
            if (id != null && id.ToLowerInvariant().Contains("wood"))
            {
                itemGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            }
            else
            {
                itemGo = new GameObject($"Dropped_{id}", typeof(MeshFilter), typeof(MeshRenderer));
                var sc = itemGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.45f;
            }
            itemGo.name = $"Dropped_{id}";
            itemGo.transform.position = position;
            itemGo.transform.localScale = Vector3.one;

            var rb = itemGo.GetComponent<Rigidbody>();
            if (rb == null) rb = itemGo.AddComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var col = itemGo.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            var ctrl = itemGo.AddComponent<DroppedItemController>();
            ctrl.Initialize(id, qty);
            if (popVel.HasValue) ctrl.ApplyPopVelocity(popVel.Value);

            return ctrl;
        }

        public void Initialize(string newItemId, int newAmount)
        {
            _spawnTime = Time.time;
            if (!string.IsNullOrWhiteSpace(newItemId)) itemId = newItemId;
            if (newAmount > 0) amount = newAmount;

            ResourceVisualBuilder.ApplyDroppedItemStyle(gameObject, itemId);
            _baseY = ProbeFloorHeight(transform.position) + 0.32f;
            _hasBaseY = true;
        }

        public void ApplyPopVelocity(Vector3 velocity) => _popVelocity = velocity;

        private void Start()
        {
            _spawnTime = Time.time;
            if (!_hasBaseY)
            {
                _baseY = ProbeFloorHeight(transform.position) + 0.32f;
                _hasBaseY = true;
            }
            ResourceVisualBuilder.ApplyDroppedItemStyle(gameObject, itemId);
        }

        private static float ProbeFloorHeight(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 8.0f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            if (pos.z >= CaveDungeonBuilder.CaveEntranceZ)
                return CaveDungeonBuilder.GetFloorY(pos.x, pos.z);
            return 0.05f;
        }

        private void Update()
        {
            if (_pickedUp) return;

            if (_popVelocity.sqrMagnitude > 0.05f)
            {
                _popVelocity += Physics.gravity * Time.deltaTime * 0.95f;
                transform.position += _popVelocity * Time.deltaTime;

                float groundY = ProbeFloorHeight(transform.position) + 0.30f;
                if (transform.position.y <= groundY && _popVelocity.y < 0f)
                {
                    transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                    _popVelocity = Vector3.zero;
                    _baseY = groundY;
                }
                else
                {
                    _baseY = transform.position.y;
                }
            }
            else
            {
                if (Time.time >= _spawnTime + pickupDelay)
                    CheckMagnetism();

                if (_targetPlayer == null)
                {
                    transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
                    float currentY = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                    transform.position = new Vector3(transform.position.x, currentY, transform.position.z);
                }
            }
        }

        private void CheckMagnetism()
        {
            if (_targetPlayer == null)
            {
                var localPlayer = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.transform : null;
                if (localPlayer == null)
                {
                    var localInv = InventoryController.LocalInstance ?? FindFirstObjectByType<InventoryController>();
                    if (localInv != null) localPlayer = localInv.transform;
                }
                if (localPlayer != null && Vector3.Distance(transform.position, localPlayer.position) <= magnetRadius)
                {
                    var hp = localPlayer.GetComponentInParent<ForjaGame.Combat.PlayerHealthController>();
                    if (hp == null || hp.IsAlive) _targetPlayer = localPlayer;
                }
            }

            if (_targetPlayer != null)
            {
                Vector3 targetCenter = _targetPlayer.position + Vector3.up * 0.8f;
                float dist = Vector3.Distance(transform.position, targetCenter);
                if (dist > magnetRadius * 1.35f)
                {
                    _targetPlayer = null;
                    return;
                }

                transform.position = Vector3.MoveTowards(transform.position, targetCenter, magnetSpeed * Time.deltaTime);
                transform.Rotate(Vector3.up, rotationSpeed * 2.5f * Time.deltaTime, Space.World);

                if (dist <= 0.95f)
                    TryCollect(_targetPlayer.gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_pickedUp || other == null || Time.time < _spawnTime + pickupDelay) return;
            // Solo recolectar si el collider pertenece realmente a un jugador (nunca al suelo ni a un enemigo)
            if (other.GetComponentInParent<PlayerMovement>() == null && !other.CompareTag("Player")) return;
            TryCollect(other.gameObject);
        }

        private void TryCollect(GameObject collector)
        {
            if (_pickedUp || collector == null) return;
            var hp = collector.GetComponentInParent<ForjaGame.Combat.PlayerHealthController>();
            if (hp != null && !hp.IsAlive) return;

            IInventoryReceiver inventory = collector.GetComponentInParent<IInventoryReceiver>();
            if (inventory == null) inventory = collector.GetComponentInChildren<IInventoryReceiver>();
            if (inventory == null && (collector.GetComponentInParent<PlayerMovement>() != null || collector.CompareTag("Player")))
                inventory = InventoryController.LocalInstance ?? InventoryController.Instance;

            if (inventory == null || !inventory.AddItem(itemId, amount))
                return;

            _pickedUp = true;
            ForjaGame.Audio.ForgeAudioSystem.PlayItemPickup(transform.position);
            Debug.Log($"<color=#77FFAA>[Item Recogido]</color> ¡{itemId} x{amount} añadido al inventario!");
            Destroy(gameObject);
        }
    }
}
