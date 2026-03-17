using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Video;

public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    [Header("Referensi UI Utama")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private Image questionImageDisplay; // NEW
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI correctScoreText;
    [SerializeField] private TextMeshProUGUI wrongScoreText;

    [Header("Referensi Video (Opsional)")]
    [SerializeField] private GameObject videoPanel;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoRawImage;

    [Header("Referensi UI Jawaban")]
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TextMeshProUGUI[] answerTexts;
    private Color defaultButtonColor = Color.white;

    [Header("Referensi UI Evaluasi")]
    [SerializeField] private GameObject evaluationPanel;
    [SerializeField] private TextMeshProUGUI evaluationText;

    [Header("Referensi Guru")]
    [SerializeField] private TeacherController teacherController;

    // State Internal Soal Saat Ini
    private QuizData[] currentQuizSequence;
    private int currentQuestionIndex;
    private QuizData currentQuiz;
    private float currentTimer;
    private bool isTimerRunning = false;
    private System.Action onQuizFinishedCallback;
    private Coroutine explainCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // BARU: Pastikan Canvas World Space memiliki Event Camera agar bisa diklik!
        if (quizPanel != null)
        {
            Canvas parentCanvas = quizPanel.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace)
            {
                if (parentCanvas.worldCamera == null)
                {
                    parentCanvas.worldCamera = Camera.main;
                }
            }
        }

        HideQuiz();
        
        // Simpan warna default tombol
        if (answerButtons.Length > 0 && answerButtons[0] != null)
        {
            defaultButtonColor = answerButtons[0].GetComponent<Image>().color;
        }
        
        // Otomatis cari Teacher jika belum di-assign
        if (teacherController == null)
            teacherController = FindFirstObjectByType<TeacherController>();
        // Pastikan Video UI mati di awal
        if (videoPanel != null) videoPanel.SetActive(false);
        if (videoPlayer != null) videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            currentTimer += Time.deltaTime; // Menghitung WAKTU BERLALU (Count Up)
            UpdateTimerUI();
        }
    }

    private bool introVideoFinished = false;
    private bool isPlayingQuestionVideos = false;

    /// <summary>
    /// Memulai serangkaian pertanyaan kuis
    /// </summary>
    public void StartQuizSequence(QuizData[] quizSequence, System.Action onFinish)
    {
        if (quizSequence == null || quizSequence.Length == 0) return;

        // Bikin salinan array untuk diacak agar urutan asli di Inspector tidak berubah permanen
        currentQuizSequence = (QuizData[])quizSequence.Clone();
        
        // Acak array menggunakan algoritma Fisher-Yates
        for (int i = 0; i < currentQuizSequence.Length; i++)
        {
            int randomIndex = Random.Range(i, currentQuizSequence.Length);
            QuizData temp = currentQuizSequence[i];
            currentQuizSequence[i] = currentQuizSequence[randomIndex];
            currentQuizSequence[randomIndex] = temp;
        }

        onQuizFinishedCallback = onFinish;
        currentQuestionIndex = 0;

        // Tampilkan Panel Utama
        if (quizPanel != null) quizPanel.SetActive(true);
        if (evaluationPanel != null) evaluationPanel.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(true);

        LoadNextQuestion();
    }

    private void LoadNextQuestion()
    {
        // Cek apakah sudah selesai
        if (currentQuestionIndex >= currentQuizSequence.Length)
        {
            StartCoroutine(FinishSequenceRoutine());
            return;
        }

        currentQuiz = currentQuizSequence[currentQuestionIndex];

        // Cek apakah soal ini punya Video Pembuka (1 atau lebih)?
        if (currentQuiz.questionVideos != null && currentQuiz.questionVideos.Length > 0 && videoPlayer != null && videoPanel != null)
        {
            StartCoroutine(PlayVideoBeforeQuestionRoutine());
            return; // Hentikan fungsi ini sementara, biarkan coroutine yang melanjutkannya nanti
        }

        // --- Alur Normal (Tanpa Video) ---
        ShowQuestionContent();
    }

    private IEnumerator PlayVideoBeforeQuestionRoutine()
    {
        isPlayingQuestionVideos = true;

        // 1. Matikan UI Kuis (supaya bersih saat nonton video)
        HideQuizContentOnly();
        
        // 2. Tampilkan panel video dengan animasi LeanTween
        ShowVideoPanel();
        yield return new WaitForSeconds(0.5f); // Tunggu animasi masuk selesai

        // 3. Putar setiap video satu per satu
        for (int i = 0; i < currentQuiz.questionVideos.Length; i++)
        {
            if (currentQuiz.questionVideos[i] == null) continue; // Skip jika slot kosong

            introVideoFinished = false;

            // Siapkan video
            videoPlayer.clip = currentQuiz.questionVideos[i];
            videoPlayer.Prepare();

            // Tunggu sampai siap
            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }

            // Tampilkan texture video ke RawImage (texture sudah tersedia setelah Prepare)
            if (videoRawImage != null)
            {
                videoRawImage.texture = videoPlayer.texture;
                videoRawImage.enabled = true;
            }

            // Putar
            videoPlayer.Play();

            Debug.Log($"[QuizManager] Memutar video soal {i + 1}/{currentQuiz.questionVideos.Length}: {currentQuiz.questionVideos[i].name}");

            // Tunggu sampai video selesai (flag diset oleh OnVideoFinished)
            while (!introVideoFinished)
            {
                yield return null;
            }
        }

        // 4. Semua video selesai, matikan panel video dengan animasi LeanTween
        isPlayingQuestionVideos = false;
        HideVideoPanel();
        yield return new WaitForSeconds(0.4f); // Tunggu animasi keluar selesai

        // 5. Lanjutkan menampilkan soal
        ShowQuestionContent();
    }

    // =============================================
    // VIDEO PANEL ANIMATION (LeanTween)
    // =============================================

    /// <summary>
    /// Menampilkan panel video dengan animasi fade-in (LeanTween)
    /// </summary>
    private void ShowVideoPanel()
    {
        if (videoPanel == null) return;

        // Cancel animasi LeanTween yang sedang berjalan di panel ini
        LeanTween.cancel(videoPanel);

        videoPanel.SetActive(true);

        // Pastikan CanvasGroup ada untuk efek fade
        CanvasGroup cg = videoPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = videoPanel.AddComponent<CanvasGroup>();

        // Set state awal
        cg.alpha = 0f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        // Animasi Fade In (tanpa ubah scale)
        LeanTween.alphaCanvas(cg, 1f, 0.4f).setEaseOutCubic();
    }

    /// <summary>
    /// Menyembunyikan panel video dengan animasi fade-out (LeanTween)
    /// </summary>
    private void HideVideoPanel()
    {
        if (videoPanel == null) return;

        // Cancel animasi LeanTween yang sedang berjalan di panel ini
        LeanTween.cancel(videoPanel);

        CanvasGroup cg = videoPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = videoPanel.AddComponent<CanvasGroup>();

        cg.interactable = false;
        cg.blocksRaycasts = false;

        // Animasi Fade Out (tanpa ubah scale)
        LeanTween.alphaCanvas(cg, 0f, 0.3f).setEaseInCubic()
            .setOnComplete(() =>
            {
                videoPanel.SetActive(false);
            });
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        // Set flag agar coroutine tahu video sudah selesai
        introVideoFinished = true;
    }

    private void ShowQuestionContent()
    {
        // Tampilkan Panel Utama (berjaga-jaga habis nonton video)
        if (quizPanel != null) quizPanel.SetActive(true);
        if (timerText != null) timerText.gameObject.SetActive(true);

        // Membacakan Soal (Idle & Audio)
        if (teacherController != null)
        {
            teacherController.PlayIdleAnimation();
            teacherController.PlayVoice(currentQuiz.questionAudio);
        }

        // Setup Teks Pertanyaan & Gambar (Bila ada)
        if (questionText != null)
        {
            questionText.gameObject.SetActive(true);
            questionText.text = currentQuiz.questionText;
        }

        if (questionImageDisplay != null)
        {
            if (currentQuiz.questionImage != null)
            {
                questionImageDisplay.gameObject.SetActive(true);
                questionImageDisplay.sprite = currentQuiz.questionImage;
            }
            else
            {
                questionImageDisplay.gameObject.SetActive(false);
            }
        }

        // Setup Teks Tombol (A, B, C, D) dan Kembalikan Warna Default
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < currentQuiz.answers.Length)
            {
                answerButtons[i].gameObject.SetActive(true);
                answerButtons[i].interactable = true;
                answerButtons[i].GetComponent<Image>().color = defaultButtonColor; // Reset Warna
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
    }

    private void OnAnswerSelected(int selectedIndex)
    {
        // 1. Matikan Timer & Suara Soal
        isTimerRunning = false;
        if (teacherController != null) teacherController.StopVoice();
        
        // Disable semua tombol agar tidak di-spam
        foreach (var btn in answerButtons) btn.interactable = false;

        // 2. Evaluasi Benar atau Salah
        bool isCorrect = (selectedIndex == currentQuiz.correctAnswerIndex);

        // 3. Simpan ke sistem (Persistence Local JSON)
        QuizSessionManager.Instance.RecordQuestionResult(currentQuiz.questionID, currentTimer, isCorrect);

        // 4. Ubah Warna Tombol yang Dipilih
        if (isCorrect)
            answerButtons[selectedIndex].GetComponent<Image>().color = Color.green;
        else
            answerButtons[selectedIndex].GetComponent<Image>().color = Color.red;

        // 5. Feedback Audio / Visual (Centang / Silang) dari Manager Lama
        if (QuizFeedbackManager.Instance != null)
            QuizFeedbackManager.Instance.PlayFeedback(isCorrect); // Ini hanya mainkan SFX dan Ikon centang/silang

        // 6. Tangani Animasi & Alur Guru
        if (isCorrect)
        {
            Debug.Log("Jawaban BENAR!");
            if (teacherController != null) teacherController.PlayClappingAnimation();

            // Lanjut ke soal berikutnya secara otomatis setelah claping selesai (sekitar 2 detik)
            StartCoroutine(WaitAndLoadNextQuestion(2.0f));
        }
        else
        {
            Debug.Log("Jawaban SALAH!");
            if (teacherController != null) teacherController.PlayWrongAnimation();

            // Tunggu 2.5 detik (wrong animation play), lalu tunjukkan bagian Explain
            explainCoroutine = StartCoroutine(WaitAndShowExplain(2.5f));
        }

        UpdateScoreUI();
    }

    private IEnumerator WaitAndLoadNextQuestion(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentQuestionIndex++;
        LoadNextQuestion();
    }

    private IEnumerator WaitAndShowExplain(float delay)
    {
        // 1. Tunggu durasi animasi "Wrong" (misal 2.5 detik atau sesuai kebutuhan)
        yield return new WaitForSeconds(delay);
        
        // 2. Mainkan animasi Explain SAAT jendela Evaluasi muncul
        float explainDuration = 3f; // Default durasi animasi jika tidak ada audio
        if (teacherController != null)
        {
            teacherController.PlayExplainAnimation();
            
            if (currentQuiz.explainAudio != null)
            {
                teacherController.PlayVoice(currentQuiz.explainAudio);
                explainDuration = currentQuiz.explainAudio.length; // Otomatis durasi berdasarkan suara
            }
        }

        // 3. Tampilkan UI Evaluasi (agar tombol lanjut & teks penjelasan muncul)
        ShowEvaluation(currentQuiz.evaluationText);

        // 4. Setelah selesai ngebacot/menjelaskan, balik ke posisi diam (Idle)
        yield return new WaitForSeconds(explainDuration);
        if (teacherController != null)
        {
            teacherController.PlayIdleAnimation();
        }
        
        // 5. Otomatis lanjut ke pertanyaan berikutnya tanpa harus tekan tombol
        CloseEvaluationAndContinue();
    }

    private void ShowEvaluation(string reasonText)
    {
        if (evaluationPanel != null)
        {
            evaluationPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("QuizManager: evaluationPanel belum di-assign di Inspector!");
        }

        if (evaluationText != null)
        {
            evaluationText.text = "<b>JAWABAN SALAH</b>\n\nPenjelasan: " + reasonText;
        }
        else
        {
            Debug.LogWarning("QuizManager: evaluationText belum di-assign di Inspector!");
        }
    }

    // Dipanggil oleh tombol "Lanjut" di Evaluasi Panel (atau otomatis setelah penjelasan selesai)
    public void CloseEvaluationAndContinue()
    {
        if (explainCoroutine != null)
        {
            StopCoroutine(explainCoroutine);
            explainCoroutine = null;
        }

        if (teacherController != null) 
        {
            teacherController.StopVoice();
            teacherController.PlayIdleAnimation();
        }

        if (evaluationPanel != null) evaluationPanel.SetActive(false);
        currentQuestionIndex++;
        LoadNextQuestion();
    }

    private IEnumerator FinishSequenceRoutine()
    {
        if (teacherController != null) teacherController.PlayIdleAnimation();

        yield return new WaitForSeconds(0.5f);
        HideQuiz();
        onQuizFinishedCallback?.Invoke(); // Memanggil trigger ruangan untuk membuka pintu/lanjut jalan
    }

    public void HideQuiz()
    {
        HideQuizContentOnly();
        if (videoPanel != null) videoPanel.SetActive(false);
    }

    private void HideQuizContentOnly()
    {
        isTimerRunning = false;
        if (teacherController != null) teacherController.StopVoice();

        // Sembunyikan elemen teks & gambar
        if (questionText != null) questionText.gameObject.SetActive(false);
        if (questionImageDisplay != null) questionImageDisplay.gameObject.SetActive(false);
        if (timerText != null) timerText.gameObject.SetActive(false);

        // Kembalikan tombol agar bisa dipencet lagi dan sembunyikan dari layar
        foreach (var btn in answerButtons) 
        {
            if (btn != null)
            {
                btn.interactable = true;
                btn.gameObject.SetActive(false);
            }
        }
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
