using UnityEngine;
using UnityEngine.Audio;

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
        // Simpan nilai untuk UI dan inisialisasi berikutnya
        PlayerPrefs.SetFloat("SavedMasterVol", sliderValue);
        
        // Konversi logaritmik: slider (0.0001 ke 1) menjadi dB (-80 ke 0)
        // Mencegah error log(0) dengan Math.Max
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        mainMixer.SetFloat(masterVolParam, db);
    }

    public void SetMusicVolume(float sliderValue)
    {
        PlayerPrefs.SetFloat("SavedMusicVol", sliderValue);
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        mainMixer.SetFloat(musicVolParam, db);
    }

    public void SetSFXVolume(float sliderValue)
    {
        PlayerPrefs.SetFloat("SavedSFXVol", sliderValue);
        float db = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        mainMixer.SetFloat(sfxVolParam, db);
    }
}
