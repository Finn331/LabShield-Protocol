using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Panel menu yang muncul saat game di-pause (set scale awal ke 0)")]
    [SerializeField] private GameObject pausePanel;
    
    [Tooltip("Daftar elemen UI yang harus disembunyikan saat pause (misal: Virtual Joystick, Tombol Jump, dsb.)")]
    [SerializeField] private List<GameObject> uiControlsToHide;

    [Header("Optional Button References (Auto-Resolve if empty)")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private Button exitButton;

    [Header("Settings")]
    [Tooltip("Waktu tunggu setelah user menekan pause/unpause agar tidak bisa spam")]
    [SerializeField] private float buttonCooldown = 0.5f;

    [Tooltip("Durasi animasi LeanTween saat panel pause muncul/hilang")]
    [SerializeField] private float animationDuration = 0.3f;

    private bool isPaused = false;
    private bool isOnCooldown = false;
    private GameplayManager gameplayManager;

    private void Awake()
    {
        gameplayManager = FindFirstObjectByType<GameplayManager>();
        ResolveButtonReferences();
        WireButtonListeners();
        ForceResetPauseState();
    }

    private void OnEnable()
    {
        ResolveButtonReferences();
        WireButtonListeners();
        EnsurePauseButtonState();
    }

    private void OnDisable()
    {
        // Jika scene berpindah saat game masih pause, paksa pulihkan state UI.
        ForceResetPauseState();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    public void ForceResetPauseState()
    {
        isPaused = false;
        isOnCooldown = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
            pausePanel.transform.localScale = Vector3.zero;
        }

        SetUIControlsActive(true);
        EnsurePauseButtonState();
    }

    private void ResolveButtonReferences()
    {
        if (pauseButton == null)
        {
            GameObject pauseButtonObject = GameObject.Find("UI_Canvas_StarterAssetsInputs_Joysticks Variant/Pause Button");
            if (pauseButtonObject != null)
            {
                pauseButton = pauseButtonObject.GetComponent<Button>();
            }
        }

        if (pausePanel == null) return;

        if (resumeButton == null)
        {
            resumeButton = pausePanel.transform.Find("Button Pause Panel/Resume Button")?.GetComponent<Button>();
        }

        if (backToMainMenuButton == null)
        {
            backToMainMenuButton = pausePanel.transform.Find("Button Pause Panel/Back To Main Menu Button")?.GetComponent<Button>();
        }

        if (exitButton == null)
        {
            exitButton = pausePanel.transform.Find("Button Pause Panel/Exit Button")?.GetComponent<Button>();
        }
    }

    private void WireButtonListeners()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(TogglePause);
            pauseButton.onClick.AddListener(TogglePause);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(TogglePause);
            resumeButton.onClick.AddListener(TogglePause);
        }

        if (backToMainMenuButton != null)
        {
            backToMainMenuButton.onClick.RemoveAllListeners();
            backToMainMenuButton.onClick.AddListener(BackToMainMenuFromPause);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(QuitFromPause);
        }
    }

    private void EnsurePauseButtonState()
    {
        if (pauseButton == null) return;

        if (!pauseButton.gameObject.activeSelf)
        {
            pauseButton.gameObject.SetActive(true);
        }

        pauseButton.interactable = true;
    }

    private void BackToMainMenuFromPause()
    {
        ForceResetPauseState();
        if (gameplayManager == null) gameplayManager = FindFirstObjectByType<GameplayManager>();
        if (gameplayManager != null)
        {
            gameplayManager.LoadMainMenu();
        }
    }

    private void QuitFromPause()
    {
        ForceResetPauseState();
        if (gameplayManager == null) gameplayManager = FindFirstObjectByType<GameplayManager>();
        if (gameplayManager != null)
        {
            gameplayManager.QuitGame();
        }
    }

    /// <summary>
    /// Fungsi untuk dihubungkan ke Event OnClick pada sebuah tombol (contoh: Button Pause / Button Resume)
    /// </summary>
    public void TogglePause()
    {
        if (!isActiveAndEnabled) return;
        if (isOnCooldown) return;

        isPaused = !isPaused;

        // Mulai waktu cooldown anti-spam click
        StartCoroutine(ActionCooldownRoutine());

        if (isPaused)
            ExecutePause();
        else
            ExecuteResume();
    }

    private void ExecutePause()
    {
        Time.timeScale = 0f; // Hentikan pergerakan di dalam game

        // Sembunyikan UI Kontrol Pergerakan
        SetUIControlsActive(false);

        // Munculkan Panel dengan animasi membesar memakai LeanTween
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            
            // Batalkan animasi yang mungkin masih berjalan dan buat dari scale 0
            LeanTween.cancel(pausePanel);
            pausePanel.transform.localScale = Vector3.zero;
            
            // Menggunakan ignoreTimeScale(true) karena Time.timeScale sekarang 0
            LeanTween.scale(pausePanel, Vector3.one, animationDuration)
                     .setIgnoreTimeScale(true)
                     .setEaseOutBack(); // Efek memantul ringan
        }
    }

    private void ExecuteResume()
    {
        // Kembalikan Time.timeScale ke 1 agar game jalan lagi (Bisa diletakkan di akhir animasi jika diinginkan)
        Time.timeScale = 1f;

        // Munculkan kembali UI Kontrol Pergerakan
        SetUIControlsActive(true);
        EnsurePauseButtonState();

        if (pausePanel != null)
        {
            LeanTween.cancel(pausePanel);
            
            LeanTween.scale(pausePanel, Vector3.zero, animationDuration)
                     .setIgnoreTimeScale(true)
                     .setEaseInBack() // Efek menyusut ke dalam
                     .setOnComplete(() =>
                     {
                         pausePanel.SetActive(false);
                     });
        }
    }

    private void SetUIControlsActive(bool isActive)
    {
        foreach (var uiElement in uiControlsToHide)
        {
            if (uiElement != null)
            {
                uiElement.SetActive(isActive);
            }
        }
    }

    private System.Collections.IEnumerator ActionCooldownRoutine()
    {
        isOnCooldown = true;
        // Kita menggunakan WaitForSecondsRealtime karena Time.timeScale bisa bernilai 0
        yield return new WaitForSecondsRealtime(buttonCooldown);
        isOnCooldown = false;
    }
}
