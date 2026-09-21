using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Componente adjunto al visual con Animator (CharacterVisual) que captura
    /// el callback nativo OnAnimatorIK de Unity y lo reenvía a ProceduralCombatController.
    /// </summary>
    public class PlayerIKBridge : MonoBehaviour
    {
        [SerializeField] private ProceduralCombatController proceduralController;

        public void SetProceduralController(ProceduralCombatController controller)
        {
            proceduralController = controller;
        }

        private void Awake()
        {
            if (proceduralController == null)
            {
                proceduralController = GetComponentInParent<ProceduralCombatController>();
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (proceduralController != null && proceduralController.enabled)
            {
                proceduralController.OnAnimatorIKCallback(layerIndex);
            }
        }
    }
}
