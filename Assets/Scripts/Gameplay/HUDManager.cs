using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance;

    [Header("UI References")]
    public TextMeshProUGUI interactionText;
    public GameObject crosshair;
    public GameObject objectivePanel;
    public TextMeshProUGUI objectiveText;

    [Header("Waypoints")]
    [Tooltip("Sistem waypoint untuk memandu arah player (Opsional).")]
    public WaypointMarker autoWaypoint;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public UnityEngine.UI.Button mobileInteractButton;
    public TextMeshProUGUI mobileButtonText;

    public void ToggleInteractionButton(bool active, string message = "")
    {
        if (mobileInteractButton)
        {
            mobileInteractButton.gameObject.SetActive(active);
            if (active && mobileButtonText)
            {
                mobileButtonText.text = message;
            }
        }
        // Fallback or PC support
        if (interactionText)
        {
            interactionText.gameObject.SetActive(active);
            interactionText.text = message;
        }
    }

    // Restored for compatibility with InventoryManager feedback
    public void ShowInteraction(string message)
    {
        // For feedback, we force the button/text to show with the message
        ToggleInteractionButton(true, message);
    }

    public void HideInteraction()
    {
        ToggleInteractionButton(false);
    }

    public void UpdateObjective(string objective)
    {
        if (objectiveText) objectiveText.text = objective;
        if (objectivePanel) objectivePanel.SetActive(true);
    }

    public void HideObjectivePanel()
    {
        if (objectivePanel)
        {
            // LeanTween Scale Down Animation
            LeanTween.scale(objectivePanel, Vector3.zero, 0.5f)
                .setEase(LeanTweenType.easeInBack)
                .setOnComplete(() =>
                {
                    objectivePanel.SetActive(false);
                    // Reset scale for next time it's shown, if needed
                    objectivePanel.transform.localScale = Vector3.one;
                });
        }
    }

    public void HideAllHUD()
    {
        HideInteraction();
        HideObjectivePanel();
        if (crosshair != null) crosshair.SetActive(false);
        if (autoWaypoint != null) autoWaypoint.gameObject.SetActive(false);

        // Turn off StarterAssets Mobile Controls to prevent them from blocking Canvas Clicks
        Transform moveJoy = transform.Find("UI_Virtual_Joystick_Move");
        if (moveJoy != null) moveJoy.gameObject.SetActive(false);
        
        Transform lookJoy = transform.Find("UI_Virtual_Joystick_Look");
        if (lookJoy != null) lookJoy.gameObject.SetActive(false);

        Transform jumpBtn = transform.Find("UI_Virtual_Button_Jump");
        if (jumpBtn != null) jumpBtn.gameObject.SetActive(false);

        Transform sprintBtn = transform.Find("UI_Virtual_Button_Sprint");
        if (sprintBtn != null) sprintBtn.gameObject.SetActive(false);
        
        UnityEngine.UI.GraphicRaycaster raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;
    }
    
    public void ShowAllHUD()
    {
        if (crosshair != null) crosshair.SetActive(true);
        if (autoWaypoint != null) autoWaypoint.gameObject.SetActive(true);

        Transform moveJoy = transform.Find("UI_Virtual_Joystick_Move");
        if (moveJoy != null) moveJoy.gameObject.SetActive(true);
        
        Transform lookJoy = transform.Find("UI_Virtual_Joystick_Look");
        if (lookJoy != null) lookJoy.gameObject.SetActive(true);

        Transform jumpBtn = transform.Find("UI_Virtual_Button_Jump");
        if (jumpBtn != null) jumpBtn.gameObject.SetActive(true);

        Transform sprintBtn = transform.Find("UI_Virtual_Button_Sprint");
        if (sprintBtn != null) sprintBtn.gameObject.SetActive(true);

        UnityEngine.UI.GraphicRaycaster raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = true;
    }

    // ==========================================
    // BAGIAN: WAYPOINT (PETUNJUK ARAH)
    // ==========================================
    
    /// <summary>
    /// Mengubah arah target Waypoint. Jika null, marker akan disembunyikan.
    /// </summary>
    /// <param name="target">Posisi 3D tujuan.</param>
    public void SetWaypointTarget(Transform target)
    {
        if (autoWaypoint != null)
        {
            autoWaypoint.SetTarget(target);
        }
        else
        {
            // Fallback: Jika tidak dipasang di Inspector, cari otomatis
            autoWaypoint = FindFirstObjectByType<WaypointMarker>();
            if (autoWaypoint != null)
            {
                autoWaypoint.SetTarget(target);
            }
            else
            {
                Debug.LogWarning("HUDManager: WaypointMarker belum dipasang di Canvas!");
            }
        }
    }
}
