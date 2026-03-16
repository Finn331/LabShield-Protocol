using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class QuizAttemptData
{
    public int attemptNumber;
    public int totalCorrect;
    public int totalWrong;
    public List<QuestionTimeData> questionTimes = new List<QuestionTimeData>();
}

[System.Serializable]
public class QuestionTimeData
{
    public string questionID;
    public float timeTakenSeconds;
}

[System.Serializable]
public class PlayerSaveData
{
    public List<QuizAttemptData> attemptHistory = new List<QuizAttemptData>();
    public QuizAttemptData currentAttempt; // Data sesi yang sedang berjalan tapi belum disubmit
    public int highestAttemptCount = 0;
}

public class QuizSessionManager : MonoBehaviour
{
    public static QuizSessionManager Instance { get; private set; }

    [Header("Session Data")]
    public PlayerSaveData saveData;
    private string saveFilePath;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Application.persistentDataPath + "/StudentQuizData.json";
            LoadData(); // Load data history kalau gamenya sempat ketutup/crash
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Dipanggil saat siswa mulai ujian pertama kali, atau main lagi (Sesi Baru).
    public void StartNewAttempt()
    {
        saveData.highestAttemptCount++;
        saveData.currentAttempt = new QuizAttemptData
        {
            attemptNumber = saveData.highestAttemptCount,
            totalCorrect = 0,
            totalWrong = 0,
            questionTimes = new List<QuestionTimeData>()
        };
        SaveData(); // Langsung amankan state "Attempt Baru" ke file.
        Debug.Log("Memulai Quiz Sesi / Attempt ke-" + saveData.highestAttemptCount);
    }

    // Dipanggil oleh QuizManager setiap kali siswa selesai menjawab 1 soal
    public void RecordQuestionResult(string qID, float timeTaken, bool isCorrect)
    {
        if (saveData.currentAttempt == null)
        {
            Debug.LogWarning("QuizSessionManager: currentAttempt kosong! Membuat sesi baru otomatis (Mungkin sedang testing di Gameplay scene).");
            StartNewAttempt();
        }

        if (isCorrect) saveData.currentAttempt.totalCorrect++;
        else saveData.currentAttempt.totalWrong++;

        // Cek apakah soal ini udah pernah direkam sebelumnya (misal crash trus load ulang)
        QuestionTimeData existingData = saveData.currentAttempt.questionTimes.Find(q => q.questionID == qID);
        if (existingData != null)
        {
            existingData.timeTakenSeconds = timeTaken;
        }
        else
        {
            saveData.currentAttempt.questionTimes.Add(new QuestionTimeData { questionID = qID, timeTakenSeconds = timeTaken });
        }

        SaveData(); // Auto-save setelah tiap pertanyaan (Offline-Safe / Checkpoint)
        Debug.Log($"Recorded {qID}: {timeTaken} detik. Benar: {saveData.currentAttempt.totalCorrect}, Salah: {saveData.currentAttempt.totalWrong}");
    }

    // Jika ingin paksa membersihkan semua data history di memori lokal PC
    public void ClearAllLocalData()
    {
        saveData = new PlayerSaveData();
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
        }
        Debug.Log("Seluruh Data History Kuis di perangkat ini telah dihapus.");
    }

    // ==========================================
    // BAGIAN: SIMPAN & MUAT JSON LOKAL
    // ==========================================
    public void SaveData()
    {
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(saveFilePath, json);
    }

    public void LoadData()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            saveData = JsonUtility.FromJson<PlayerSaveData>(json);
            Debug.Log($"Loaded Local Data. Save path: {saveFilePath}");
        }
        else
        {
            saveData = new PlayerSaveData();
        }
    }

    // ==========================================
    // BAGIAN PENGIRIMAN DATA KE WEBSITE GURU
    // ==========================================
    // Panggil fungsi ini (SubmitAttemptToServer) ketika pemain selesai soal terakhir.
    // Jika sukses terkirim, datanya akan pindah ke "attemptHistory" dan mengijinkan Reset Game.
    public void SubmitAttemptToServer(System.Action<bool> onComplete)
    {
        // TODO: Nanti sambungkan ini ke IEnumerator UnityWebRequest di NetworkManager.cs
        // Untuk sekarang, kita anggap selalu "Sukses Mengirim".
        
        bool success = true; 

        if (success)
        {
            // Amankan data attempt ini ke daftar riwayat, hapus dari "progress jalan"
            saveData.attemptHistory.Add(saveData.currentAttempt);
            saveData.currentAttempt = null; 
            SaveData();
            
            Debug.Log("Data berhasil dikirim ke Website Guru!");
            onComplete?.Invoke(true);
        }
        else
        {
            Debug.LogWarning("Koneksi Timeout. Data tersimpan di memori aman, tapi belum masuk ke web guru.");
            onComplete?.Invoke(false);
        }
    }
}
