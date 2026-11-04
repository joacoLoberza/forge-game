using UnityEngine;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Componente de compatibilidad para versiones legadas de puertas cavernícolas.
    /// Las cavernas actuales utilizan navegación orgánica continua sin barreras artificiales.
    /// </summary>
    public class CaveZoneGateController : MonoBehaviour
    {
        private void Awake() => ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(gameObject);
    }
}
