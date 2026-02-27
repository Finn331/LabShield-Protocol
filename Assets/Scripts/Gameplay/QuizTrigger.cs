using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class QuizTrigger : MonoBehaviour
{
    [Header("Data Soal di Ruangan Ini")]
    public QuizData roomQuizData;

    [Header("Event Ruangan (Opsional)")]
    [Tooltip("Dipanggil saat pemain masuk trigger (contoh: Memulai animasi ledakan/kecipratan)")]
    public UnityEvent onPlayerEnterRoom;
    
    [Tooltip("Dipanggil saat kuis di ruangan ini selesai dijawab")]
    public UnityEvent onQuizCompleted;

    private bool hasBeenTriggered = false;

    private void Start()
    {
        // Pastikan collider adalah trigger
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Cek apakah yang masuk adalah Player (bisa dicek via tag "Player" atau komponen spesifik)
        if (other.CompareTag("Player") && !hasBeenTriggered)
        {
            hasBeenTriggered = true; // Langsung kunci agar tidak trigger dobel
            Debug.Log($"Pemain memasuki area kuis: {roomQuizData?.questionID}");

            // 1. Bekukan pemain (Opsional, tergantung sistem movementmu)
            // other.GetComponent<PlayerMovement>().enabled = false; 

            // 2. Jalankan Animasi / Skenario Ruangan (Lewat Event Unity)
            // Misalnya: Memutar animasi guru, animasi ledakan, dll.
            if (onPlayerEnterRoom != null)
                onPlayerEnterRoom.Invoke();

            // 3. Setelah animasi selesai, munculkan UI Soal.
            // Untuk sekarang, kita asumsikan animasinya instan, jadi langsung memanggil QuizManager:
            ShowQuizDelay(1.0f); // Contoh delay transisi 1 detik 
        }
    }

    private void ShowQuizDelay(float delay)
    {
        Invoke("TriggerQuizPanel", delay);
    }

    private void TriggerQuizPanel()
    {
        // Panggil QuizManager untuk menampilkan soal ini
        if (QuizManager.Instance != null && roomQuizData != null)
        {
            // Lempar callback (fungsi) yang akan dijalankan SETELAH pemain selesai menjawab Soal ini
            QuizManager.Instance.StartQuiz(roomQuizData, OnPlayerFinishedQuiz);
        }
        else
        {
            Debug.LogError("QuizManager belum terpasang di Scene, atau QuizData di Trigger belum di-assign!");
        }
    }

    private void OnPlayerFinishedQuiz()
    {
        Debug.Log($"Kuis di ruangan {roomQuizData?.questionID} selesai!");

        // Berikan kebebasan akses / buka pintu ke ruangan selanjutnya (via event)
        if (onQuizCompleted != null)
            onQuizCompleted.Invoke();
            
        // PlayerMovement = true; (Buka gembok kembali)
    }
}
