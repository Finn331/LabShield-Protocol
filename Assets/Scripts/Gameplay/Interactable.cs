using UnityEngine;
using UnityEngine.Events;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public string promptMessage = "Interact";
    public bool isInteractable = true;

    // Called when the player looks at this object
    public virtual void OnFocus() { }

    // Called when the player looks away
    public virtual void OnLoseFocus() { }

    // Called when the user presses the Interact button (E)
    public virtual void OnInteract()
    {
        if (!isInteractable) return;
        ExecuteInteraction();
    }

    protected abstract void ExecuteInteraction();
}
