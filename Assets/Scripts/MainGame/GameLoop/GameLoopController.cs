using UnityEngine;
using UnityEngine.Events;

namespace ForjaGame.Core
{
    public class GameLoopController : MonoBehaviour
    {
        [Header("Eventos de Interfaz")]
        public UnityEvent onVictory;
        public UnityEvent onDefeat;

        [Header("Reaparición")]
        [Tooltip("Arrastra aquí a tu jugador (la cápsula)")]
        public Transform playerTransform;
        
        [Tooltip("Crea un objeto vacío en tu escena donde quieras que reaparezca y arrástralo aquí")]
        public Transform respawnPoint;

        private void Start()
        {
            Time.timeScale = 1f; 
        }

        public void TriggerVictory()
        {
            Time.timeScale = 0f;
            onVictory?.Invoke(); 
        }

        public void TriggerDefeat()
        {
            Time.timeScale = 0f;
            onDefeat?.Invoke();  
        }

        public void AcceptVictory()
        {
            Time.timeScale = 1f;
            ResetPlayerPosition();
            // A futuro: Aquí se sumará un punto al ranking del jugador que ganó
        }

        public void AcceptDefeat()
        {
            Time.timeScale = 1f;
            ResetPlayerPosition();
            // A futuro: Aquí perderá los items recolectados en la cueva
        }

        private void ResetPlayerPosition()
        {
            if (playerTransform != null && respawnPoint != null)
            {
                playerTransform.position = respawnPoint.position;
                
                Rigidbody rb = playerTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero; // Frena al personaje
                }
            }
        }
    }
}