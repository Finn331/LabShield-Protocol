using UnityEngine;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    public float interactionDistance = 3f;
    public LayerMask interactionLayer;
    public KeyCode interactKey = KeyCode.E;

    [Header("UI References")]
    public TextMeshProUGUI promptText;
    public GameObject reticle;

    private Camera cam;
    private Interactable currentInteractable;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (promptText) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        CheckForInteractable();

        // Debug Raycast to visualize where the camera is looking
        Debug.DrawRay(cam.transform.position, cam.transform.forward * interactionDistance, Color.red);

        // Hybrid Input: Support both Mobile UI and Keyboard 'E' (New Input System)
        if (currentInteractable != null &&
            UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            InteractByUI(); // Reuse the same method
        }
    }

    void CheckForInteractable()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayer))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();

            if (interactable != null && interactable.isInteractable)
            {
                if (currentInteractable != interactable)
                {
                    if (currentInteractable != null) currentInteractable.OnLoseFocus();
                    currentInteractable = interactable;
                    currentInteractable.OnFocus();
                    // Notify HUD Manager to enable Mobile Button
                    HUDManager.Instance.ToggleInteractionButton(true, currentInteractable.promptMessage);
                }
                return;
            }
        }

        // Nothing found or lost focus
        if (currentInteractable != null)
        {
            currentInteractable.OnLoseFocus();
            currentInteractable = null;
            // Notify HUD Manager to disable Mobile Button
            HUDManager.Instance.ToggleInteractionButton(false);
        }
    }

    // Public method called by UI Button Event
    public void InteractByUI()
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnInteract();
        }
    }

    void UpdatePrompt(bool active, string msg = "")
    {
        // Deprecated in favor of HUDManager
    }
}
