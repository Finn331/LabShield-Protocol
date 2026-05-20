using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class NetworkManager : MonoBehaviour
{
    [SerializeField] private string serverUrl = "http://2.27.165.46:5000/api/submit-score";

    public static NetworkManager Instance { get; private set; }
    private const string LastUsernameKey = "last_username";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Mengirim data lengkap (APD + Quiz) dari QuizSessionManager ke server Dashboard.
    /// Dipanggil saat siswa menyelesaikan seluruh sesi permainan.
    /// </summary>
    public void SubmitFullScore()
    {
        if (QuizSessionManager.Instance == null || QuizSessionManager.Instance.saveData.currentAttempt == null)
        {
            Debug.LogWarning("NetworkManager: Tidak ada data attempt untuk dikirim.");
            return;
        }

        var attempt = QuizSessionManager.Instance.saveData.currentAttempt;
        string studentName = ResolveStudentName();

        // Bangun payload baru yang terklasifikasi
        StudentScorePayload payload = new StudentScorePayload
        {
            studentName = studentName,
            attemptNumber = attempt.attemptNumber,
            apdTotalCorrect = attempt.apdTotalCorrect,
            apdTotalWrong = attempt.apdTotalWrong,
            apdTimeTakenSeconds = attempt.apdTimeTakenSeconds,
            quizTotalCorrect = attempt.quizTotalCorrect,
            quizTotalWrong = attempt.quizTotalWrong,
            questionTimes = new List<QuestionTimePayload>()
        };

        // Konversi tiap data waktu soal
        foreach (var qt in attempt.questionTimes)
        {
            payload.questionTimes.Add(new QuestionTimePayload
            {
                questionID = qt.questionID,
                timeTaken = qt.timeTakenSeconds,
                isCorrect = false // QuestionTimeData tidak menyimpan isCorrect, default false
            });
        }

        StartCoroutine(PostScore(payload));
    }

    public static void SubmitFullScoreSafe()
    {
        NetworkManager manager = EnsureInstance();
        if (manager == null)
        {
            Debug.LogError("NetworkManager: Instance tidak ditemukan dan gagal dibuat otomatis.");
            return;
        }

        manager.SubmitFullScore();
    }

    /// <summary>
    /// Legacy: Kirim skor simpel (backward compatible).
    /// </summary>
    public void SubmitScore(int questionsAnswered, float score)
    {
        string studentName = AuthManager.IsLoggedIn ? AuthManager.CurrentUsername : "Guest";
        StartCoroutine(PostScoreLegacy(new StudentData(studentName, questionsAnswered, score)));
    }

    private IEnumerator PostScore(StudentScorePayload data)
    {
        string jsonData = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"Sending full score to {serverUrl}: {jsonData}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error submitting score: {request.error} | Code: {request.responseCode} | Body: {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log($"Full score submitted successfully! Response: {request.downloadHandler.text}");

                if (QuizSessionManager.Instance != null && QuizSessionManager.Instance.FinalizeCurrentAttemptLocally())
                {
                    Debug.Log("NetworkManager: Attempt finalized locally after successful upload.");
                }
            }
        }
    }

    private IEnumerator PostScoreLegacy(StudentData data)
    {
        string jsonData = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"Sending legacy score to {serverUrl}: {jsonData}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error submitting legacy score: {request.error} | Code: {request.responseCode} | Body: {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log($"Legacy score submitted successfully! Response: {request.downloadHandler.text}");
            }
        }
    }

    private static NetworkManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<NetworkManager>();
        if (Instance != null) return Instance;

        GameObject go = new GameObject("NetworkManager_Auto");
        return go.AddComponent<NetworkManager>();
    }

    private static string ResolveStudentName()
    {
        if (AuthManager.IsLoggedIn && !string.IsNullOrWhiteSpace(AuthManager.CurrentUsername))
        {
            return AuthManager.CurrentUsername;
        }

        string cachedUsername = PlayerPrefs.GetString(LastUsernameKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(cachedUsername))
        {
            return cachedUsername;
        }

        return "Guest";
    }
}
