using UnityEngine;
using System.Collections.Generic;

public class PauseManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Panel menu yang muncul saat game di-pause (set scale awal ke 0)")]
    [SerializeField] private GameObject pausePanel;
    
    [Tooltip("Daftar elemen UI yang harus disembunyikan saat pause (misal: Virtual Joystick, Tombol Jump, dsb.)")]
    [SerializeField] private List<GameObject> uiControlsToHide;

    [Header("Settings")]
    [Tooltip("Waktu tunggu setelah user menekan pause/unpause agar tidak bisa spam")]
    [SerializeField] private float buttonCooldown = 0.5f;

    [Tooltip("Durasi animasi LeanTween saat panel pause muncul/hilang")]
    [SerializeField] private float animationDuration = 0.3f;

    private bool isPaused = false;
    private bool isOnCooldown = false;

    private void Start()
    {
        // Pastikan pause panel mati/tidak terlihat saat mulai dan ukurannya dinolkan
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
            pausePanel.transform.localScale = Vector3.zero;
        }
    }

    /// <summary>
    /// Fungsi untuk dihubungkan ke Event OnClick pada sebuah tombol (contoh: Button Pause / Button Resume)
    /// </summary>
    public void TogglePause()
    {
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
