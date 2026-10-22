using UnityEngine;

namespace ForjaGame.Interaction
{
    public interface IInteractable
    {
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
        string InteractionPrompt { get; }
    }
}