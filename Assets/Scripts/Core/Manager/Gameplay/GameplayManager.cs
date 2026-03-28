using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameplayManager : MonoBehaviour
{
    public static GameplayManager Instance;

    [Header("Intro Conversation & Objective")]
    [Tooltip("Klip audio intro/percakapan awal yang akan dimainkan saat game mulai.")]
    public AudioClip introDialogClip;
    
    [Tooltip("Pesan objektif pertama yang muncul di layar.")]
    public string firstObjectiveText = "Pergi ke Ruang Ganti untuk menggunakan APD (Alat Pelindung Diri).";
    
    [Tooltip("Jeda waktu sebelum memunculkan teks objektif (agar tidak tertumpuk dengan transisi layar).")]
    public float objectiveDelay = 2.0f;

    [Header("Waypoints Sequence")]
    [Tooltip("Titik penanda pintu masuk Ruang APD.")]
    public Transform apdRoomWaypoint;
    [Tooltip("Titik penanda pintu masuk Ruang Ganti.")]
    public Transform changingRoomWaypoint;
    [Tooltip("Titik penanda pintu masuk Lab Kimia.")]
    public Transform chemLabWaypoint;
    [Tooltip("Titik penanda kursi untuk memulai kuis.")]
    public Transform quizChairWaypoint;

    private AudioSource audioSource;

    [Header("UI - Score")]
    public GameObject scorePanel;
    [SerializeField] TextMeshProUGUI rightScoreText;
    [SerializeField] TextMeshProUGUI wrongScoreText;
    [SerializeField] float timerUIScore;

    [Header("Score Setting")]
    public int rightScoreValue;
    public int wrongScoreValue;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ApplySavedRuntimeVideoSettings();

        if (introDialogClip != null && audioSource != null)
        {
            audioSource.clip = introDialogClip;
            audioSource.Play();
        }
        StartCoroutine(ShowInitialObjective());
    }

    private void ApplySavedRuntimeVideoSettings()
    {
        int savedGraphicsQuality = PlayerPrefs.GetInt("GraphicsQuality", 2);
        SettingsMenuController.ApplyGraphicsQualityProfile(savedGraphicsQuality);

        int maxFramerateIndex = PlayerPrefs.GetInt("MaxFramerateIndex", 1);
        Application.targetFrameRate = FramerateFromIndex(maxFramerateIndex);
    }

    private static int FramerateFromIndex(int index)
    {
        switch (index)
        {
            case 0: return 30;
            case 1: return 60;
            case 2: return 120;
            case 3: return -1;
            default: return 60;
        }
    }

    private System.Collections.IEnumerator ShowInitialObjective()
    {
        yield return new WaitForSeconds(objectiveDelay);

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateObjective(firstObjectiveText);
            // Aktifkan Waypoint pertama menuju Ruang APD
            HUDManager.Instance.SetWaypointTarget(apdRoomWaypoint);
        }
    }

    // Removed unused Score region and Update() loop to prevent conflicts with ScoreSystemManager

    public void LoadMainMenu()
    {
        PauseManager pauseManager = FindFirstObjectByType<PauseManager>();
        if (pauseManager != null)
        {
            pauseManager.ForceResetPauseState();
        }

        // Pastikan time scale kembali normal sebelum pindah scene
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Debug.Log("Keluar dari game...");
        Application.Quit();
    }
}
