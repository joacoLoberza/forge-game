using UnityEngine;
using UnityEngine.InputSystem; // Necesario para leer el nuevo input

namespace ForjaGame.Core
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float speed = 5f;
        
        private Rigidbody _rb;
        private Vector2 _moveInput;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        // Este método lo conectaremos a los Unity Events del PlayerInput
        public void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        private void FixedUpdate()
        {
            // Movemos el personaje en X y Z, respetando la gravedad en Y
            Vector3 movement = new Vector3(_moveInput.x, 0f, _moveInput.y) * speed;
            _rb.linearVelocity = new Vector3(movement.x, _rb.linearVelocity.y, movement.z);
        }
    }
}