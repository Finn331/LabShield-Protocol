using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro for Dropdowns
using System;

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

    [Header("Button SFX")]
    [Tooltip("SFX klik untuk tombol Settings (Video, Audio, Apply, Reset, Back).")]
    public AudioClip settingsButtonClickClip;

    [Header("Video Settings")]
    public TMP_Dropdown qualityDropdown; // For SGSR/Quality selection
    public TMP_Dropdown framerateDropdown; // Changed from Resolution to MaxFPS

    [Header("Audio Settings")]
    public Slider masterVolSlider;
    public Slider musicVolSlider;
    public Slider sfxVolSlider;

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

    private AudioSource uiSfxSource;
    private float lastSfxPlayTime = -10f;
    private const float MinSfxGap = 0.05f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (settingsButtonClickClip == null)
            settingsButtonClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Button/Click Menu.mp3");
    }
#endif

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
        ResolveAndNormalizeAudioSliders();
        InitializeAudioSliders();
        EnsureUiSfxSource();

        // Setup Tab Listeners
        if (videoTabButton)
        {
            videoTabButton.onClick.RemoveAllListeners();
            videoTabButton.onClick.AddListener(OnVideoTabPressed);
        }
        if (audioTabButton)
        {
            audioTabButton.onClick.RemoveAllListeners();
            audioTabButton.onClick.AddListener(OnAudioTabPressed);
        }

        // Setup Action Listeners
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackPressed);
        }
        if (applyButton)
        {
            applyButton.onClick.RemoveAllListeners();
            applyButton.onClick.AddListener(OnApplyPressed);
        }
        if (resetButton)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnResetPressed);
        }

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

    private void InitializeAudioSliders()
    {
        ResolveAndNormalizeAudioSliders();

        // Set slider values based on what is saved, defaulting to 1 (max volume)
        if (masterVolSlider != null)
        {
            masterVolSlider.onValueChanged.RemoveAllListeners();
            masterVolSlider.value = PlayerPrefs.GetFloat("SavedMasterVol", 1f);
            masterVolSlider.onValueChanged.AddListener((val) => 
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(val);
            });
        }

        if (musicVolSlider != null)
        {
            musicVolSlider.onValueChanged.RemoveAllListeners();
            musicVolSlider.value = PlayerPrefs.GetFloat("SavedMusicVol", 1f);
            musicVolSlider.onValueChanged.AddListener((val) => 
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(val);
            });
        }

        if (sfxVolSlider != null)
        {
            sfxVolSlider.onValueChanged.RemoveAllListeners();
            sfxVolSlider.value = PlayerPrefs.GetFloat("SavedSFXVol", 1f);
            sfxVolSlider.onValueChanged.AddListener((val) => 
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(val);
            });
        }
    }

    private void ResolveAndNormalizeAudioSliders()
    {
        // Fallback auto-wire jika ada reference kosong / salah mapping dari inspector.
        if (settingsPanel == null) return;

        Slider[] sliders = settingsPanel.GetComponentsInChildren<Slider>(true);

        if (masterVolSlider == null) masterVolSlider = FindSliderByKeyword(sliders, "main", "master");
        if (musicVolSlider == null) musicVolSlider = FindSliderByKeyword(sliders, "music", "bgm");
        if (sfxVolSlider == null) sfxVolSlider = FindSliderByKeyword(sliders, "sfx", "effect");

        // Auto-fix kasus reference tertukar (Music <-> SFX).
        if (musicVolSlider != null && sfxVolSlider != null)
        {
            bool musicLooksLikeSfx = ContainsAnyKeyword(musicVolSlider.gameObject.name, "sfx", "effect");
            bool sfxLooksLikeMusic = ContainsAnyKeyword(sfxVolSlider.gameObject.name, "music", "bgm");

            if (musicLooksLikeSfx || sfxLooksLikeMusic)
            {
                Slider temp = musicVolSlider;
                musicVolSlider = sfxVolSlider;
                sfxVolSlider = temp;
            }
        }

        NormalizeSlider(masterVolSlider);
        NormalizeSlider(musicVolSlider);
        NormalizeSlider(sfxVolSlider);
    }

    private static Slider FindSliderByKeyword(Slider[] sliders, params string[] keywords)
    {
        if (sliders == null) return null;

        for (int i = 0; i < sliders.Length; i++)
        {
            Slider slider = sliders[i];
            if (slider == null) continue;

            string candidateName = slider.gameObject.name;
            if (ContainsAnyKeyword(candidateName, keywords))
                return slider;
        }

        return null;
    }

    private static bool ContainsAnyKeyword(string text, params string[] keywords)
    {
        if (string.IsNullOrEmpty(text) || keywords == null) return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrEmpty(keyword)) continue;
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    private static void NormalizeSlider(Slider slider)
    {
        if (slider == null) return;

        slider.wholeNumbers = false;
        slider.minValue = 0.0001f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp(slider.value, slider.minValue, slider.maxValue);
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

        if (masterVolSlider) masterVolSlider.value = 1f;
        if (musicVolSlider) musicVolSlider.value = 1f;
        if (sfxVolSlider) sfxVolSlider.value = 1f;
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

    private void OnVideoTabPressed()
    {
        PlaySettingsButtonSfxIfEnabled();
        ShowVideoTab();
    }

    private void OnAudioTabPressed()
    {
        PlaySettingsButtonSfxIfEnabled();
        ShowAudioTab();
    }

    private void OnApplyPressed()
    {
        PlaySettingsButtonSfxIfEnabled();
        ApplySettings();
    }

    private void OnResetPressed()
    {
        PlaySettingsButtonSfxIfEnabled();
        ResetSettings();
    }

    private void OnBackPressed()
    {
        PlaySettingsButtonSfxIfEnabled();
        CloseSettings();
    }

    private void EnsureUiSfxSource()
    {
        if (uiSfxSource != null) return;

        uiSfxSource = GetComponent<AudioSource>();
        if (uiSfxSource == null)
        {
            uiSfxSource = gameObject.AddComponent<AudioSource>();
        }

        uiSfxSource.playOnAwake = false;
        uiSfxSource.loop = false;
        uiSfxSource.spatialBlend = 0f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.RouteSourceToSfxGroup(uiSfxSource);
        }
    }

    private void PlaySettingsButtonSfxIfEnabled()
    {
        if (settingsButtonClickClip == null) return;
        if (!ShouldPlaySettingsSfx()) return;

        EnsureUiSfxSource();
        if (uiSfxSource == null) return;

        if (Time.unscaledTime - lastSfxPlayTime < MinSfxGap) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.RouteSourceToSfxGroup(uiSfxSource);
        }

        if (uiSfxSource.isPlaying)
        {
            uiSfxSource.Stop();
        }

        uiSfxSource.clip = settingsButtonClickClip;
        uiSfxSource.Play();
        lastSfxPlayTime = Time.unscaledTime;
    }

    private static bool ShouldPlaySettingsSfx()
    {
        if (AudioManager.Instance != null)
        {
            return AudioManager.Instance.IsSfxAudible();
        }

        float savedMaster = PlayerPrefs.GetFloat("SavedMasterVol", 1f);
        float savedSfx = PlayerPrefs.GetFloat("SavedSFXVol", 1f);
        return savedMaster > 0.001f && savedSfx > 0.001f;
    }
}


