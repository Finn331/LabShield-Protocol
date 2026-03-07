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
        // Pastikan collider adalah trigger (Jika masih dipakai untuk mencegah lewat, atau bisa diabaikan jika tidak pakai collider lagi)
        if (GetComponent<Collider>() != null)
        {
            GetComponent<Collider>().isTrigger = true;
        }
    }

    // Fungsi ini dipanggil manual oleh ChairInteraction saat player duduk, bukan lewat BoxCollider lagi
    public void TriggerQuizManual()
    {
        if (!hasBeenTriggered)
        {
            hasBeenTriggered = true; // Langsung kunci agar tidak trigger dobel
            Debug.Log($"Memulai Kuis Manual Dari Kursi: {roomQuizData?.questionID}");

            // 1. Jalankan Animasi / Skenario Ruangan (Lewat Event Unity)
            if (onPlayerEnterRoom != null)
                onPlayerEnterRoom.Invoke();

            // 2. Setelah animasi selesai, munculkan UI Soal.
            ShowQuizDelay(1.0f); // Delay transisi 1 detik 
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
