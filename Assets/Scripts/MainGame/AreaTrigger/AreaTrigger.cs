using UnityEngine;
using UnityEngine.Events;

namespace ForjaGame.Core
{
    /// <summary>
    /// SRP: Solo avisa cuando un objeto con cierto Tag entra en su zona.
    /// DIP: No sabe qué pasa después, solo dispara un evento abstracto.
    /// </summary>
    public class AreaTrigger : MonoBehaviour
    {
        [SerializeField] private string targetTag = "Player";
        public UnityEvent onTriggerEntered;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(targetTag))
            {
                onTriggerEntered?.Invoke();
            }
        }
    }
}