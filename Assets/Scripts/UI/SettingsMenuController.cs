using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro for Dropdowns

public class SettingsMenuController : MonoBehaviour
{
    [Header("Animation")]
    public float animationDuration = 0.5f;
    public LeanTweenType openEase = LeanTweenType.easeOutBack;
    public LeanTweenType closeEase = LeanTweenType.easeInBack;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onBack;

    [Header("Panels")]
    public GameObject settingsPanel;
    public GameObject videoPanel;
    public GameObject audioPanel;

    [Header("Tabs (Buttons)")]
    public Button videoTabButton;
    public Button audioTabButton;

    [Header("Action Buttons")]
    public Button applyButton;
    public Button resetButton;
    public Button backButton;

    [Header("Video Settings")]
    public TMP_Dropdown qualityDropdown; // For SGSR/Quality selection
    public TMP_Dropdown framerateDropdown; // Changed from Resolution to MaxFPS

    /* 
     * TND / SGSR INTEGRATION NOTE:
     * If using TND Upscaler or SGSR, you typically need to reference their specific script or shader global properties.
     * Example:
     * using TND.Upscaler; // Uncomment if namespace exists
     */

    [Header("World Transform Targets")]
    public Vector3 targetPosition = new Vector3(5.999938f, 0.795325f, -4.931295f);
    public Quaternion targetRotation = new Quaternion(0.0f, -0.7071095f, 0.0f, -0.7071041f);
    public Vector3 targetScale = new Vector3(0.1332075f, 0.1332075f, 0.1332075f);

    void Start()
    {
        // 1. Ensure Panel is INACTIVE initially
        if (settingsPanel)
        {
            settingsPanel.SetActive(false);
            settingsPanel.transform.localScale = Vector3.zero; // Prepare for pop-in

            // Ensure CanvasGroup for interaction blocking
            if (settingsPanel.GetComponent<CanvasGroup>() == null)
                settingsPanel.AddComponent<CanvasGroup>();
        }

        // Initialize Options & Load Saves
        InitializeQualityDropdown();
        InitializeFramerateDropdown();

        // Setup Tab Listeners
        if (videoTabButton) videoTabButton.onClick.AddListener(ShowVideoTab);
        if (audioTabButton) audioTabButton.onClick.AddListener(ShowAudioTab);

        // Setup Action Listeners
        if (backButton) backButton.onClick.AddListener(CloseSettings);
        if (applyButton) applyButton.onClick.AddListener(ApplySettings);
        if (resetButton) resetButton.onClick.AddListener(ResetSettings);

        // Setup Dropdown Listeners
        if (qualityDropdown) qualityDropdown.onValueChanged.AddListener((val) => SetGraphicsQuality(val, true));
        if (framerateDropdown) framerateDropdown.onValueChanged.AddListener(SetMaxFramerate);

        // Default to Video Tab
        ShowVideoTab();
    }

    private void InitializeQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();

        // Standard SGSR / Upscaler Modes
        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>
        {
            "Native (No Upscaling)",
            "Ultra Quality (1.3x)",
            "Quality (1.5x)",
            "Balanced (1.7x)",
            "Performance (2.0x)",
            "Ultra Performance (3.0x)"
        };

        qualityDropdown.AddOptions(options);

        // Load Saved Quality
        int savedQuality = PlayerPrefs.GetInt("GraphicsQuality", 2); // Default to Quality
        qualityDropdown.value = savedQuality;
        SetGraphicsQuality(savedQuality, false); // Set without saving again
    }

    // ... (OpenSettings, CloseSettings, Tab methods remain unchanged)

    public void SetGraphicsQuality(int index) => SetGraphicsQuality(index, true);

    public void SetGraphicsQuality(int index, bool save)
    {
        Debug.Log($"[Settings] Graphics Quality Set to: {index}");

        if (save)
        {
            PlayerPrefs.SetInt("GraphicsQuality", index);
            PlayerPrefs.Save();
        }

        // Standard Unity Quality (Optional mapping)
        QualitySettings.SetQualityLevel(index, true);

        /* 
         * TND / SGSR IMPLEMENTATION
         * Map index to TND Properties:
         * 0: Native
         * 1: Ultra Quality
         * 2: Quality
         * 3: Balanced
         * 4: Performance
         * 5: Ultra Performance
         */
    }
    public void OpenSettings()
    {
        if (settingsPanel == null) return;

        settingsPanel.SetActive(true);
        SetInteraction(false); // Disable interaction during animation
        ShowVideoTab();

        // Animation: Move/Rotate/Scale to Target
        settingsPanel.transform.localScale = Vector3.zero; // Start small

        // Optional: Ensure Position/Rotation are set if they drift, or animate them too
        settingsPanel.transform.position = targetPosition;
        settingsPanel.transform.rotation = targetRotation;

        LeanTween.scale(settingsPanel, targetScale, animationDuration)
            .setEase(openEase)
            .setOnComplete(() => SetInteraction(true)); // Enable when done
    }

    public void CloseSettings()
    {
        SetInteraction(false); // Disable interaction immediately

        // Animation: Scale Down -> Then Disable -> Then Trigger Callback
        LeanTween.scale(settingsPanel, Vector3.zero, animationDuration)
            .setEase(closeEase)
            .setOnComplete(() =>
            {
                settingsPanel.SetActive(false);
                onBack?.Invoke();
            });
    }

    public void ShowVideoTab()
    {
        if (videoPanel) videoPanel.SetActive(true);
        if (audioPanel) audioPanel.SetActive(false);
        UpdateTabVisuals(videoTabButton, audioTabButton);
    }

    public void ShowAudioTab()
    {
        if (videoPanel) videoPanel.SetActive(false);
        if (audioPanel) audioPanel.SetActive(true);
        UpdateTabVisuals(audioTabButton, videoTabButton);
    }

    private void UpdateTabVisuals(Button active, Button inactive)
    {
        // Optional: Change button colors to show active state
        // if (active) active.image.color = Color.white;
        // if (inactive) inactive.image.color = Color.gray;
    }

    public void ApplySettings()
    {
        Debug.Log("[Settings] Settings Applied!");
        // Save preferences here (PlayerPrefs)
        PlayerPrefs.Save();
    }

    public void ResetSettings()
    {
        Debug.Log("[Settings] Settings Reset to Default.");

        SetGraphicsQuality(2); // Quality
        if (qualityDropdown) qualityDropdown.value = 2;

        SetMaxFramerate(1); // 60 FPS
        if (framerateDropdown) framerateDropdown.value = 1;
    }
    private void InitializeFramerateDropdown()
    {
        if (framerateDropdown == null) return;

        framerateDropdown.ClearOptions();

        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>
        {
            "30 FPS",
            "60 FPS",
            "120 FPS",
            "Unlimited"
        };

        framerateDropdown.AddOptions(options);

        // Load Saved Framerate
        int savedIndex = PlayerPrefs.GetInt("MaxFramerateIndex", 1); // Default to 60 FPS
        framerateDropdown.value = savedIndex;
        SetMaxFramerate(savedIndex);
    }

    public void SetMaxFramerate(int index)
    {
        int targetFPS = -1;
        switch (index)
        {
            case 0: targetFPS = 30; break;
            case 1: targetFPS = 60; break;
            case 2: targetFPS = 120; break;
            case 3: targetFPS = -1; break; // Unlimited
        }

        Application.targetFrameRate = targetFPS;
        PlayerPrefs.SetInt("MaxFramerateIndex", index);
        PlayerPrefs.Save();

        Debug.Log($"[Settings] Max Framerate Set to: {targetFPS} (Index: {index})");
    }

    // Toggle Interaction to prevent bugs during animation
    private void SetInteraction(bool active)
    {
        if (settingsPanel)
        {
            CanvasGroup cg = settingsPanel.GetComponent<CanvasGroup>();
            if (cg) cg.blocksRaycasts = active;
        }
    }
}


