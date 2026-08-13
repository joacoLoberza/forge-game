using UnityEngine;

public class CanvasController : MonoBehaviour, IToggleable
{
    [SerializeField] public GameObject canvasObject;

    public void Toggle()
    {
        canvasObject.SetActive(!canvasObject.activeSelf);
    }
}
