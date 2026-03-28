using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro for Dropdowns
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SettingsMenuController : MonoBehaviour
{
    private const string GraphicsQualityPrefKey = "GraphicsQuality";
    private const string MaxFrameratePrefKey = "MaxFramerateIndex";

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

    // Committed (sudah di-apply) values
    private int appliedGraphicsQualityIndex = 2;
    private int appliedFramerateIndex = 1;
    private float appliedMasterVolume = 1f;
    private float appliedMusicVolume = 1f;
    private float appliedSfxVolume = 1f;

    // Draft (belum di-apply) values
    private int pendingGraphicsQualityIndex = 2;
    private int pendingFramerateIndex = 1;
    private float pendingMasterVolume = 1f;
    private float pendingMusicVolume = 1f;
    private float pendingSfxVolume = 1f;

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

        // Load committed values once, lalu siapkan draft.
        LoadCommittedSettingsFromPrefs();
        CopyCommittedToPending();

        // Initialize Options & UI
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

        // Setup Dropdown/Slider listeners dalam mode draft (tidak apply otomatis)
        if (qualityDropdown)
        {
            qualityDropdown.onValueChanged.RemoveAllListeners();
            qualityDropdown.onValueChanged.AddListener(OnDraftQualityChanged);
        }

        if (framerateDropdown)
        {
            framerateDropdown.onValueChanged.RemoveAllListeners();
            framerateDropdown.onValueChanged.AddListener(OnDraftFramerateChanged);
        }

        BindAudioDraftListeners();

        // Pastikan runtime mengikuti committed value yang tersimpan.
        ApplyCommittedSettingsToRuntime(false);

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
        qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(pendingGraphicsQualityIndex, 0, 5));
    }

    private void InitializeAudioSliders()
    {
        ResolveAndNormalizeAudioSliders();

        // Tampilkan draft value ke slider tanpa langsung apply runtime.
        if (masterVolSlider != null)
        {
            masterVolSlider.onValueChanged.RemoveAllListeners();
            masterVolSlider.SetValueWithoutNotify(Mathf.Clamp(pendingMasterVolume, masterVolSlider.minValue, masterVolSlider.maxValue));
        }

        if (musicVolSlider != null)
        {
            musicVolSlider.onValueChanged.RemoveAllListeners();
            musicVolSlider.SetValueWithoutNotify(Mathf.Clamp(pendingMusicVolume, musicVolSlider.minValue, musicVolSlider.maxValue));
        }

        if (sfxVolSlider != null)
        {
            sfxVolSlider.onValueChanged.RemoveAllListeners();
            sfxVolSlider.SetValueWithoutNotify(Mathf.Clamp(pendingSfxVolume, sfxVolSlider.minValue, sfxVolSlider.maxValue));
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
        index = Mathf.Clamp(index, 0, 5);
        Debug.Log($"[Settings] Graphics Quality Set to: {index}");

        if (save)
        {
            PlayerPrefs.SetInt(GraphicsQualityPrefKey, index);
            PlayerPrefs.Save();
        }

        ApplyGraphicsQualityProfile(index);

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

        // Saat panel dibuka, draft direset ke committed supaya perubahan lama yang belum di-apply tidak nyangkut.
        RevertDraftToCommitted();
        RefreshUiFromPending();

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
        CommitPendingAsApplied();
        ApplyCommittedSettingsToRuntime(true);
        Debug.Log("[Settings] Settings Applied.");
    }

    public void ResetSettings()
    {
        // Reset hanya ke draft default. Tetap perlu Apply agar benar-benar diterapkan/disimpan.
        pendingGraphicsQualityIndex = 2;
        pendingFramerateIndex = 1;
        pendingMasterVolume = 1f;
        pendingMusicVolume = 1f;
        pendingSfxVolume = 1f;
        RefreshUiFromPending();
        Debug.Log("[Settings] Draft reset to default. Press Apply to save.");
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
        framerateDropdown.SetValueWithoutNotify(Mathf.Clamp(pendingFramerateIndex, 0, 3));
    }

    public void SetMaxFramerate(int index) => SetMaxFramerate(index, true);

    public void SetMaxFramerate(int index, bool save)
    {
        index = Mathf.Clamp(index, 0, 3);
        int targetFPS = -1;
        switch (index)
        {
            case 0: targetFPS = 30; break;
            case 1: targetFPS = 60; break;
            case 2: targetFPS = 120; break;
            case 3: targetFPS = -1; break; // Unlimited
        }

        Application.targetFrameRate = targetFPS;
        if (save)
        {
            PlayerPrefs.SetInt(MaxFrameratePrefKey, index);
            PlayerPrefs.Save();
        }

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
        // Batalkan draft yang belum di-apply.
        RevertDraftToCommitted();
        RefreshUiFromPending();
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

    private void BindAudioDraftListeners()
    {
        if (masterVolSlider != null)
        {
            masterVolSlider.onValueChanged.RemoveAllListeners();
            masterVolSlider.onValueChanged.AddListener((val) => pendingMasterVolume = Mathf.Clamp01(val));
        }

        if (musicVolSlider != null)
        {
            musicVolSlider.onValueChanged.RemoveAllListeners();
            musicVolSlider.onValueChanged.AddListener((val) => pendingMusicVolume = Mathf.Clamp01(val));
        }

        if (sfxVolSlider != null)
        {
            sfxVolSlider.onValueChanged.RemoveAllListeners();
            sfxVolSlider.onValueChanged.AddListener((val) => pendingSfxVolume = Mathf.Clamp01(val));
        }
    }

    private void OnDraftQualityChanged(int index)
    {
        pendingGraphicsQualityIndex = Mathf.Clamp(index, 0, 5);
    }

    private void OnDraftFramerateChanged(int index)
    {
        pendingFramerateIndex = Mathf.Clamp(index, 0, 3);
    }

    private void LoadCommittedSettingsFromPrefs()
    {
        appliedGraphicsQualityIndex = Mathf.Clamp(PlayerPrefs.GetInt(GraphicsQualityPrefKey, 2), 0, 5);
        appliedFramerateIndex = Mathf.Clamp(PlayerPrefs.GetInt(MaxFrameratePrefKey, 1), 0, 3);
        appliedMasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SavedMasterVol", 1f));
        appliedMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SavedMusicVol", 1f));
        appliedSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("SavedSFXVol", 1f));
    }

    private void CopyCommittedToPending()
    {
        pendingGraphicsQualityIndex = appliedGraphicsQualityIndex;
        pendingFramerateIndex = appliedFramerateIndex;
        pendingMasterVolume = appliedMasterVolume;
        pendingMusicVolume = appliedMusicVolume;
        pendingSfxVolume = appliedSfxVolume;
    }

    private void RevertDraftToCommitted()
    {
        CopyCommittedToPending();
    }

    private void CommitPendingAsApplied()
    {
        appliedGraphicsQualityIndex = pendingGraphicsQualityIndex;
        appliedFramerateIndex = pendingFramerateIndex;
        appliedMasterVolume = pendingMasterVolume;
        appliedMusicVolume = pendingMusicVolume;
        appliedSfxVolume = pendingSfxVolume;
    }

    private void RefreshUiFromPending()
    {
        if (qualityDropdown != null)
        {
            qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(pendingGraphicsQualityIndex, 0, 5));
        }

        if (framerateDropdown != null)
        {
            framerateDropdown.SetValueWithoutNotify(Mathf.Clamp(pendingFramerateIndex, 0, 3));
        }

        if (masterVolSlider != null)
        {
            masterVolSlider.SetValueWithoutNotify(Mathf.Clamp(pendingMasterVolume, masterVolSlider.minValue, masterVolSlider.maxValue));
        }

        if (musicVolSlider != null)
        {
            musicVolSlider.SetValueWithoutNotify(Mathf.Clamp(pendingMusicVolume, musicVolSlider.minValue, musicVolSlider.maxValue));
        }

        if (sfxVolSlider != null)
        {
            sfxVolSlider.SetValueWithoutNotify(Mathf.Clamp(pendingSfxVolume, sfxVolSlider.minValue, sfxVolSlider.maxValue));
        }
    }

    private void ApplyCommittedSettingsToRuntime(bool save)
    {
        SetGraphicsQuality(appliedGraphicsQualityIndex, save);
        SetMaxFramerate(appliedFramerateIndex, save);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(appliedMasterVolume);
            AudioManager.Instance.SetMusicVolume(appliedMusicVolume);
            AudioManager.Instance.SetSFXVolume(appliedSfxVolume);
        }
        else if (save)
        {
            PlayerPrefs.SetFloat("SavedMasterVol", appliedMasterVolume);
            PlayerPrefs.SetFloat("SavedMusicVol", appliedMusicVolume);
            PlayerPrefs.SetFloat("SavedSFXVol", appliedSfxVolume);
            PlayerPrefs.Save();
        }
    }

    public static void ApplyGraphicsQualityProfile(int index)
    {
        index = Mathf.Clamp(index, 0, 5);

        // Project ini punya 2 quality level (Mobile, PC). Map 6 preset ke level tersebut.
        int qualityLevelCount = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        if (qualityLevelCount > 0)
        {
            int targetQualityLevel = index <= 2 ? Mathf.Min(1, qualityLevelCount - 1) : 0;
            targetQualityLevel = Mathf.Clamp(targetQualityLevel, 0, qualityLevelCount - 1);
            QualitySettings.SetQualityLevel(targetQualityLevel, true);
        }

        // Pastikan efek kualitas terlihat di camera gameplay lewat URP render scale.
        float targetRenderScale = GetRenderScaleForPreset(index);
        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            urpAsset.renderScale = targetRenderScale;
        }

        Debug.Log($"[Settings] Applied graphics preset {index} -> RenderScale {targetRenderScale:0.00}");
    }

    private static float GetRenderScaleForPreset(int index)
    {
        switch (Mathf.Clamp(index, 0, 5))
        {
            case 0: return 1.00f; // Native
            case 1: return 0.85f; // Ultra Quality
            case 2: return 0.75f; // Quality
            case 3: return 0.67f; // Balanced
            case 4: return 0.50f; // Performance
            case 5: return 0.33f; // Ultra Performance
            default: return 0.75f;
        }
    }
}


