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
}
