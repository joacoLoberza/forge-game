using UnityEngine;
using UnityEngine.Events;

namespace ForjaGame.Core
{
    /// <summary>
    /// Dispara un evento cuando un jugador activo entra en la zona (p.ej. Altar de Victoria al final de la cueva).
    /// </summary>
    public class AreaTrigger : MonoBehaviour
    {
        [SerializeField] private string targetTag = "Player";
        public UnityEvent onTriggerEntered;

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.gameObject.activeInHierarchy) return;

            bool isTagMatch = !string.IsNullOrEmpty(targetTag) && SafeCompareTag(other, targetTag);
            bool isPlayer = isTagMatch
                            || other.GetComponentInParent<PlayerMovement>() != null
                            || other.GetComponentInParent<Combat.PlayerHealthController>() != null;

            if (isPlayer && !GameLoopController.IsEndScreenOpen)
            {
                onTriggerEntered?.Invoke();
            }
        }

        private static bool SafeCompareTag(Collider col, string tag)
        {
            try { return col.CompareTag(tag); }
            catch { return false; }
        }
    }
}
