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
    [HideInInspector] public Interactable forcedInteractable; // Dipakai agar tombol tidak hilang saat kamera pindah saat duduk

    void Start()
    {
        cam = GetComponent<Camera>();
        if (promptText) promptText.gameObject.SetActive(false);

        // --- Auto-wire UI Button Interact agar user tidak perlu setting manual di Inspector ---
        if (HUDManager.Instance != null && HUDManager.Instance.mobileInteractButton != null)
        {
            HUDManager.Instance.mobileInteractButton.onClick.RemoveListener(InteractByUI);
            HUDManager.Instance.mobileInteractButton.onClick.AddListener(InteractByUI);
            Debug.Log("[PlayerInteraction] Successfully auto-wired Mobile Interact Button!");
        }
        else
        {
            Debug.LogWarning("[PlayerInteraction] HUDManager or Mobile Interact Button is missing! Interaction UI will not work.");
        }
    }

    void Update()
    {
        // Jika sedang dipaksa fokus pada rupa interaksi tertentu (misal: Sedang duduk di kursi)
        if (forcedInteractable != null)
        {
            if (currentInteractable != forcedInteractable)
            {
                if (currentInteractable != null) currentInteractable.OnLoseFocus();
                currentInteractable = forcedInteractable;
                currentInteractable.OnFocus();
                HUDManager.Instance.ToggleInteractionButton(true, currentInteractable.promptMessage);
            }

            // Tetap deteksi input E keyboard
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                InteractByUI();
            }
            return; // Lewati pengecekan Raycast
        }

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
        // Gunakan RaycastAll untuk menebus objek (karena kamera Third Person biasanya menabrak punggung pemeran utama dulu)
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        
        // Urutkan dari yang terdekat dengan menara kamera hingga yang terjauh
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool foundInteractable = false;

        foreach (RaycastHit h in hits)
        {
            // 1. Abaikan tubuh pemain itu sendiri (Kamera pasti ada di belakang/dekat kepala pemain)
            if (h.collider.CompareTag("Player") || h.collider.transform.root.CompareTag("Player"))
            {
                continue;
            }

            // 2. Cek apakah objek ini ada di dalam layer Interaksi yang Anda izinkan
            if (((1 << h.collider.gameObject.layer) & interactionLayer) != 0)
            {
                Interactable interactable = h.collider.GetComponent<Interactable>();

                if (interactable != null && interactable.isInteractable)
                {
                    if (currentInteractable != interactable)
                    {
                        if (currentInteractable != null) currentInteractable.OnLoseFocus();
                        currentInteractable = interactable;
                        currentInteractable.OnFocus();
                        HUDManager.Instance.ToggleInteractionButton(true, currentInteractable.promptMessage);
                    }
                    foundInteractable = true;
                    return; // Selesai, kita menemukan target!
                }
            }
            
            // 3. Jika bukan Player dan bukan Interactable, berarti ini Tembok / Meja penghalang! (Sistem Raycast berhenti di sini)
            break; 
        }

        if (!foundInteractable)
        {
            // Nothing found or lost focus
            if (currentInteractable != null)
            {
                currentInteractable.OnLoseFocus();
                currentInteractable = null;
                // Notify HUD Manager to disable Mobile Button
                HUDManager.Instance.ToggleInteractionButton(false);
            }
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
