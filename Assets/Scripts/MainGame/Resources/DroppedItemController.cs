using UnityEngine;
using ForjaGame.Inventory;

namespace ForjaGame.Resources
{
    public class DroppedItemController : MonoBehaviour
    {
        [SerializeField] private string itemId = "Iron";
        [SerializeField] private int amount = 1;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 2.5f;
        [SerializeField] private float rotationSpeed = 60f;

        private bool _pickedUp;
        private float _baseY;
        private bool _hasBaseY;

        private void Start()
        {
            _baseY = transform.position.y;
            _hasBaseY = true;
        }

        public void Initialize(string newItemId, int newAmount)
        {
            if (!string.IsNullOrWhiteSpace(newItemId))
                itemId = newItemId;
            if (newAmount > 0)
                amount = newAmount;
        }

        private void Update()
        {
            if (_pickedUp) return;

            if (!_hasBaseY)
            {
                _baseY = transform.position.y;
                _hasBaseY = true;
            }

            // Suave oscilación visual y rotación continua
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            float currentY = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, currentY, transform.position.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_pickedUp)
                return;

            // Soporta multijugador local: verifica cualquier jugador con receptor de inventario
            IInventoryReceiver inventory = other.GetComponentInParent<IInventoryReceiver>() ??
                                           other.GetComponentInChildren<IInventoryReceiver>();

            if (inventory == null && other.CompareTag("Player"))
                inventory = other.GetComponent<IInventoryReceiver>();

            if (inventory == null || !inventory.AddItem(itemId, amount))
                return;

            _pickedUp = true;
            Debug.Log($"Item picked up: {itemId} x{amount}");
            Destroy(gameObject);
        }
    }
}
