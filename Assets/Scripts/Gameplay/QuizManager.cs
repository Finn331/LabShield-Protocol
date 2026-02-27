using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    [Header("Referensi UI Utama")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI correctScoreText;
    [SerializeField] private TextMeshProUGUI wrongScoreText;

    [Header("Referensi UI Jawaban")]
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TextMeshProUGUI[] answerTexts;

    [Header("Referensi UI Evaluasi")]
    [SerializeField] private GameObject evaluationPanel;
    [SerializeField] private TextMeshProUGUI evaluationText;

    // State Internal Soal Saat Ini
    private QuizData currentQuiz;
    private float currentTimer;
    private bool isTimerRunning = false;
    private System.Action onQuizFinishedCallback;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        HideQuiz();
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            currentTimer += Time.deltaTime; // Menghitung WAKTU BERLALU (Count Up)
            UpdateTimerUI();
        }
    }

    /// <summary>
    /// Memulai pertanyaan kuis baru di layar
    /// </summary>
    public void StartQuiz(QuizData quizData, System.Action onFinish)
    {
        if (quizData == null) return;

        currentQuiz = quizData;
        onQuizFinishedCallback = onFinish;

        // Setup Teks Pertanyaan
        questionText.text = currentQuiz.questionText;

        // Setup Teks Tombol (A, B, C, D)
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < currentQuiz.answers.Length)
            {
                answerButtons[i].gameObject.SetActive(true);
                answerTexts[i].text = currentQuiz.answers[i];
                
                // Hapus listener lama lalu tambahkan yang baru
                int answerIndex = i; // Cache variable untuk lambda
                answerButtons[i].onClick.RemoveAllListeners();
                answerButtons[i].onClick.AddListener(() => OnAnswerSelected(answerIndex));
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false); // Sembunyikan jika tidak ada isinya
            }
        }

        // Reset & Mulai Timer
        currentTimer = 0f;
        isTimerRunning = true;
        UpdateTimerUI();

        // Refresh Skor UI Top Bar
        UpdateScoreUI();

        // Tampilkan Panel
        quizPanel.SetActive(true);
        evaluationPanel.SetActive(false);
    }

    private void OnAnswerSelected(int selectedIndex)
    {
        // 1. Matikan Timer
        isTimerRunning = false;
        
        // Disable semua tombol agar tidak di-spam
        foreach (var btn in answerButtons) btn.interactable = false;

        // 2. Evaluasi Benar atau Salah
        bool isCorrect = (selectedIndex == currentQuiz.correctAnswerIndex);

        // 3. Simpan ke sistem (Persistence Local JSON)
        QuizSessionManager.Instance.RecordQuestionResult(currentQuiz.questionID, currentTimer, isCorrect);

        // 4. Feedback Audio / Visual (Centang / Silang & Ekpresi Guru)
        if (QuizFeedbackManager.Instance != null)
        {
            QuizFeedbackManager.Instance.PlayFeedback(isCorrect);
        }

        if (isCorrect)
        {
            Debug.Log("Jawaban BENAR!");
            StartCoroutine(FinishQuizRoutine(1.5f)); // Beri delay sejenak untuk animasi senyum
        }
        else
        {
            Debug.Log("Jawaban SALAH!");
            ShowEvaluation(currentQuiz.evaluationText);
        }

        UpdateScoreUI();
    }

    private void ShowEvaluation(string reasonText)
    {
        evaluationPanel.SetActive(true);
        evaluationText.text = "<b>JAWABAN SALAH</b>\n\nPenjelasan: " + reasonText;
    }

    // Dipanggil oleh tombol "Lanjut" di Evaluasi Panel
    public void CloseEvaluationAndContinue()
    {
        evaluationPanel.SetActive(false);
        StartCoroutine(FinishQuizRoutine(0.5f));
    }

    private IEnumerator FinishQuizRoutine(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        HideQuiz();
        onQuizFinishedCallback?.Invoke(); // Memanggil trigger ruangan untuk membuka pintu/lanjut jalan
    }

    public void HideQuiz()
    {
        quizPanel.SetActive(false);
        isTimerRunning = false;

        // Kembalikan tombol agar bisa dipencet lagi untuk kuis berikutnya
        foreach (var btn in answerButtons) btn.interactable = true;
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int menit = Mathf.FloorToInt(currentTimer / 60);
            int detik = Mathf.FloorToInt(currentTimer % 60);
            timerText.text = string.Format("{0:00}:{1:00}", menit, detik);
        }
    }

    private void UpdateScoreUI()
    {
        if (correctScoreText != null && QuizSessionManager.Instance.saveData.currentAttempt != null)
            correctScoreText.text = "Benar: " + QuizSessionManager.Instance.saveData.currentAttempt.totalCorrect.ToString();
            
        if (wrongScoreText != null && QuizSessionManager.Instance.saveData.currentAttempt != null)
            wrongScoreText.text = "Salah: " + QuizSessionManager.Instance.saveData.currentAttempt.totalWrong.ToString();
    }
}
