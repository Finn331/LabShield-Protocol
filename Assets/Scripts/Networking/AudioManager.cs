using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Mixer Reference")]
    [Tooltip("Masukkan AudioMixer utama game Anda (misal: MainMixer)")]
    public AudioMixer mainMixer;

    [Header("Volume Parameters (Match Exposed Names in Mixer)")]
    public string masterVolParam = "MasterVol";
    public string musicVolParam = "MusicVol";
    public string sfxVolParam = "SFXVol";

    [Header("Default Volumes (0.0001 - 1.0)")]
    public float defaultMasterVolume = 1f;
    public float defaultMusicVolume = 1f;
    public float defaultSFXVolume = 1f;

    [Header("Runtime Fallback")]
    [Tooltip("Jika aktif, source yang tidak diroute ke Mixer Group tetap ikut slider volume.")]
    public bool applyFallbackToUnroutedSources = true;
    [Tooltip("Nama Audio Mixer Group untuk kanal SFX.")]
    public string sfxGroupName = "SFX";

    private float currentMasterVolume = 1f;
    private float currentMusicVolume = 1f;
    private float currentSFXVolume = 1f;

    public float CurrentMasterVolume => currentMasterVolume;
    public float CurrentMusicVolume => currentMusicVolume;
    public float CurrentSfxVolume => currentSFXVolume;

    // Simpan nilai volume mentah per AudioSource (sebelum dikali slider)
    private readonly Dictionary<int, float> rawVolumeBySource = new Dictionary<int, float>();
    private readonly Dictionary<int, float> lastCombinedMultiplierBySource = new Dictionary<int, float>();

    void Awake()
    {
        // Singleton pattern pattern yang menjaga instance tetap hidup melintasi scene
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // Muat volume yang tersimpan atau gunakan default
        float savedMaster = PlayerPrefs.GetFloat("SavedMasterVol", defaultMasterVolume);
        float savedMusic = PlayerPrefs.GetFloat("SavedMusicVol", defaultMusicVolume);
        float savedSFX = PlayerPrefs.GetFloat("SavedSFXVol", defaultSFXVolume);

        SetMasterVolume(savedMaster);
        SetMusicVolume(savedMusic);
        SetSFXVolume(savedSFX);
    }

    public void SetMasterVolume(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);
        currentMasterVolume = sliderValue;

        // Simpan nilai untuk UI dan inisialisasi berikutnya
        PlayerPrefs.SetFloat("SavedMasterVol", sliderValue);
        PlayerPrefs.Save();
        
        // Konversi logaritmik: slider (0.0001 ke 1) menjadi dB (-80 ke 0)
        // Mencegah error log(0) dengan Math.Max
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        TrySetMixerFloat(masterVolParam, db);
        ApplyFallbackVolumesToUnroutedSources();
    }

    public void SetMusicVolume(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);
        currentMusicVolume = sliderValue;

        PlayerPrefs.SetFloat("SavedMusicVol", sliderValue);
        PlayerPrefs.Save();

        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        TrySetMixerFloat(musicVolParam, db);
        ApplyFallbackVolumesToUnroutedSources();
    }

    public void SetSFXVolume(float sliderValue)
    {
        sliderValue = Mathf.Clamp01(sliderValue);
        currentSFXVolume = sliderValue;

        PlayerPrefs.SetFloat("SavedSFXVol", sliderValue);
        PlayerPrefs.Save();

        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        TrySetMixerFloat(sfxVolParam, db);
        ApplyFallbackVolumesToUnroutedSources();
    }

    private void TrySetMixerFloat(string exposedParamName, float db)
    {
        if (mainMixer == null)
        {
            Debug.LogWarning($"[AudioManager] Main mixer belum di-assign. Param {exposedParamName} tidak bisa di-set.");
            return;
        }

        bool success = mainMixer.SetFloat(exposedParamName, db);
        if (!success)
        {
            Debug.LogWarning($"[AudioManager] Exposed parameter '{exposedParamName}' tidak ditemukan di mixer.");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Tunggu 1 frame agar semua AudioSource di scene baru sudah aktif.
        StartCoroutine(ApplyFallbackNextFrame());
    }

    private IEnumerator ApplyFallbackNextFrame()
    {
        yield return null;
        ApplyFallbackVolumesToUnroutedSources();
    }

    private void ApplyFallbackVolumesToUnroutedSources()
    {
        if (!applyFallbackToUnroutedSources) return;

        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null) continue;

            // Source yang sudah diroute ke mixer group biarkan dikontrol mixer sepenuhnya.
            if (source.outputAudioMixerGroup != null) continue;

            int id = source.GetInstanceID();

            float previousCombined = 1f;
            if (lastCombinedMultiplierBySource.TryGetValue(id, out float storedCombined))
                previousCombined = Mathf.Max(storedCombined, 0.0001f);

            // Ambil raw volume (sebelum scaling fallback), tetap adaptif kalau ada script lain yang ubah volume.
            float rawVolume = source.volume / previousCombined;
            rawVolume = Mathf.Clamp01(rawVolume);
            rawVolumeBySource[id] = rawVolume;

            bool isLikelyMusic = source.loop || source.gameObject.name.ToLower().Contains("music");
            float channelMultiplier = isLikelyMusic ? currentMusicVolume : currentSFXVolume;
            float combinedMultiplier = currentMasterVolume * channelMultiplier;

            source.volume = Mathf.Clamp01(rawVolumeBySource[id] * combinedMultiplier);
            lastCombinedMultiplierBySource[id] = Mathf.Max(combinedMultiplier, 0.0001f);
        }
    }

    public AudioMixerGroup GetSfxMixerGroup()
    {
        if (mainMixer == null || string.IsNullOrEmpty(sfxGroupName))
            return null;

        AudioMixerGroup[] groups = mainMixer.FindMatchingGroups(sfxGroupName);
        if (groups == null || groups.Length == 0)
            return null;

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && string.Equals(groups[i].name, sfxGroupName, System.StringComparison.OrdinalIgnoreCase))
                return groups[i];
        }

        return groups[0];
    }

    public void RouteSourceToSfxGroup(AudioSource source)
    {
        if (source == null) return;

        AudioMixerGroup sfxGroup = GetSfxMixerGroup();
        if (sfxGroup != null)
        {
            source.outputAudioMixerGroup = sfxGroup;
        }
    }

    public bool IsSfxAudible(float threshold = 0.001f)
    {
        return currentMasterVolume > threshold && currentSFXVolume > threshold;
    }
}
