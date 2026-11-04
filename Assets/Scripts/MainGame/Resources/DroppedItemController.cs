using UnityEngine;
using ForjaGame.Inventory;

namespace ForjaGame.Resources
{
    /// <summary>
    /// Ítem coleccionable físico que flota en el mundo al romper menas o derrotar enemigos.
    /// Cuenta con flotación, rotación, luz de color y atracción magnética hacia el jugador.
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
        [SerializeField] private float pickupDelay = 0.30f;

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
            _baseY = Mathf.Max(0.35f, transform.position.y);
            _hasBaseY = true;
        }

        public void ApplyPopVelocity(Vector3 velocity)
        {
            _popVelocity = velocity;
        }

        private void Start()
        {
            _spawnTime = Time.time;
            if (!_hasBaseY)
            {
                _baseY = Mathf.Max(0.35f, transform.position.y);
                _hasBaseY = true;
            }
            ResourceVisualBuilder.ApplyDroppedItemStyle(gameObject, itemId);
        }

        private void Update()
        {
            if (_pickedUp) return;

            // 1. Pop arc inicial balístico con detección de suelo por Raycast (evita caer al vacío o hundirse en lomas)
            if (_popVelocity.sqrMagnitude > 0.05f)
            {
                _popVelocity += Physics.gravity * Time.deltaTime * 0.95f;
                transform.position += _popVelocity * Time.deltaTime;

                float groundY = 0.35f;
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    groundY = hit.point.y + 0.28f;
                }

                if (transform.position.y <= groundY && _popVelocity.y < 0f)
                {
                    transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                    _popVelocity = Vector3.zero;
                    _baseY = groundY;
                }
                else
                {
                    _baseY = Mathf.Max(0.20f, transform.position.y);
                }
            }
            else
            {
                // 2. Atracción magnética hacia el jugador tras el retardo visible
                if (Time.time >= _spawnTime + pickupDelay)
                {
                    CheckMagnetism();
                }

                if (_targetPlayer == null)
                {
                    // 3. Suave oscilación visual y rotación continua en reposo sobre el suelo
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
                var localInv = InventoryController.LocalInstance ?? FindFirstObjectByType<InventoryController>();
                if (localInv != null && Vector3.Distance(transform.position, localInv.transform.position) <= magnetRadius)
                {
                    _targetPlayer = localInv.transform;
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
                {
                    TryCollect(_targetPlayer.gameObject);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_pickedUp || Time.time < _spawnTime + pickupDelay) return;
            TryCollect(other.gameObject);
        }

        private void TryCollect(GameObject collector)
        {
            if (_pickedUp || collector == null) return;

            IInventoryReceiver inventory = collector.GetComponentInParent<IInventoryReceiver>();
            if (inventory == null) inventory = collector.GetComponentInChildren<IInventoryReceiver>();
            if (inventory == null && collector.CompareTag("Player"))
                inventory = collector.GetComponent<IInventoryReceiver>();

            if (inventory == null && InventoryController.LocalInstance != null)
                inventory = InventoryController.LocalInstance;

            if (inventory == null && InventoryController.Instance != null)
                inventory = InventoryController.Instance;

            if (inventory == null || !inventory.AddItem(itemId, amount))
                return;

            _pickedUp = true;
            Debug.Log($"<color=#77FFAA>[Item Recogido]</color> ¡{itemId} x{amount} añadido al inventario!");
            Destroy(gameObject);
        }
    }
}
