using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    public float interactionDistance = 3f;
    public LayerMask interactionLayer;
    public KeyCode interactKey = KeyCode.E;

    [Header("Tap Pickup (Mobile/Mouse)")]
    [Tooltip("Izinkan pemain tap/klik langsung pada objek APD (PickupItem) untuk mengambilnya.")]
    public bool allowTapPickup = true;
    [Range(1f, 4f)]
    [Tooltip("Perbesar jarak raycast untuk tap agar lebih mudah mengenai APD dari layar.")]
    public float tapDistanceMultiplier = 1.5f;
    [Tooltip("Jika aktif, tap APD akan diblokir saat jari/kursor berada di atas elemen UI.")]
    public bool blockTapWhenPointerOverUI = false;

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
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                InteractByUI();
            }
            return; // Lewati pengecekan Raycast
        }

        CheckForInteractable();
        TryTapPickupInteraction();

        // Debug Raycast to visualize where the camera is looking
        Debug.DrawRay(cam.transform.position, cam.transform.forward * interactionDistance, Color.red);

        // Hybrid Input: Support both Mobile UI and Keyboard 'E' (New Input System)
        if (currentInteractable != null &&
            Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
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
                Interactable interactable = h.collider.GetComponentInParent<Interactable>();

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

    private void TryTapPickupInteraction()
    {
        if (!allowTapPickup || cam == null)
        {
            return;
        }

        if (!TryGetTapScreenPosition(out Vector2 screenPosition, out int pointerId))
        {
            return;
        }

        bool pointerOverUI = IsPointerOverUI(pointerId);
        bool pickedByRay = false;

        float tapDistance = interactionDistance * Mathf.Max(1f, tapDistanceMultiplier);
        Ray tapRay = cam.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(tapRay, tapDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player") || hit.collider.transform.root.CompareTag("Player"))
            {
                continue;
            }

            if (((1 << hit.collider.gameObject.layer) & interactionLayer) == 0)
            {
                continue;
            }

            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            if (interactable == null || !interactable.isInteractable)
            {
                continue;
            }

            // Tap langsung hanya untuk APD (PickupItem), agar perilaku objek lain tetap konsisten.
            if (interactable is PickupItem)
            {
                if (blockTapWhenPointerOverUI && pointerOverUI)
                {
                    return;
                }

                currentInteractable = interactable;
                currentInteractable.OnInteract();
                pickedByRay = true;
                return;
            }

            continue;

        }
        if (!pickedByRay && currentInteractable is PickupItem focusedPickup && focusedPickup.isInteractable)
        {
            if (blockTapWhenPointerOverUI && pointerOverUI)
            {
                return;
            }

            focusedPickup.OnInteract();
        }
    }


    private bool TryGetTapScreenPosition(out Vector2 screenPosition, out int pointerId)
    {
        screenPosition = default;
        pointerId = -1;

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                screenPosition = touch.position.ReadValue();
                pointerId = touch.touchId.ReadValue();
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPosition = touch.position;
                pointerId = touch.fingerId;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        return false;
    }

    private bool IsPointerOverUI(int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (pointerId >= 0 && EventSystem.current.IsPointerOverGameObject(pointerId))
        {
            return true;
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    // Public method called by UI Button Event
    public void InteractByUI()
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnInteract();
        }
    }

    public void ReleaseForcedInteraction(bool hideInteractionButton = true)
    {
        if (currentInteractable != null)
        {
            currentInteractable.OnLoseFocus();
        }

        currentInteractable = null;
        forcedInteractable = null;

        if (hideInteractionButton && HUDManager.Instance != null)
        {
            HUDManager.Instance.ToggleInteractionButton(false);
        }
    }

    void UpdatePrompt(bool active, string msg = "")
    {
        // Deprecated in favor of HUDManager
    }
}

