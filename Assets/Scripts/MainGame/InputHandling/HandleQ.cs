using UnityEngine;
using UnityEngine.InputSystem;

public class HandleQ : MonoBehaviour
{
    public MonoBehaviour toggleableObjectRef;
    private IToggleable _toggleableObject;

	void Awake()
	{
        if (toggleableObjectRef != null)
        {
            _toggleableObject = toggleableObjectRef.GetComponent<IToggleable>();
        }
	}
	void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame && _toggleableObject != null)
        {
            _toggleableObject.Toggle();
        }
    }
}
