using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Video;

public class QuizManager : MonoBehaviour
{
    public static QuizManager Instance { get; private set; }

    [Header("Referensi UI Utama")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private GameObject quizAnswerPanel;
    [SerializeField] private GameObject quizPicturePanel;
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private Image questionImageDisplay; // NEW
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI correctScoreText;
    [SerializeField] private TextMeshProUGUI wrongScoreText;

    [Header("Referensi Video (Opsional)")]
    [SerializeField] private GameObject videoPanel;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoRawImage;
    [SerializeField] private TextMeshProUGUI videoProblemText;
    [SerializeField] private GameObject videoProblemTextPanel;

    [Header("Referensi UI Jawaban")]
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TextMeshProUGUI[] answerTexts;
    private Color defaultButtonColor = Color.white;

    [Header("Referensi UI Evaluasi")]
    [SerializeField] private GameObject evaluationPanel;
    [SerializeField] private TextMeshProUGUI evaluationText;

    [Header("Referensi Ending Screen")]
    [SerializeField] private GameObject endingScreenPanel;
    [SerializeField] private TextMeshProUGUI endingPerformanceText;

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
    private Coroutine question3PresentationCoroutine;
    private bool isQuestion3ImagePresentationRunning = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        ResolveQuizUiReferences();

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

        // Rapikan UI ending agar tombol selalu bisa diklik dan teks ringkas pakai Total Nilai Panel
        SetupEndingScreenUI();
    }

    private void ResolveQuizUiReferences()
    {
        if (questionImageDisplay != null && quizPicturePanel == null && questionImageDisplay.transform.parent != null)
        {
            quizPicturePanel = questionImageDisplay.transform.parent.gameObject;
        }

        if (answerButtons != null && answerButtons.Length > 0 && answerButtons[0] != null && quizAnswerPanel == null)
        {
            Transform answerParent = answerButtons[0].transform.parent;
            if (answerParent != null)
            {
                quizAnswerPanel = answerParent.gameObject;
            }
        }

        if (quizPanel == null && questionText != null)
        {
            Transform cursor = questionText.transform;
            while (cursor != null)
            {
                if (cursor.name == "Quiz Panel")
                {
                    quizPanel = cursor.gameObject;
                    break;
                }

                cursor = cursor.parent;
            }
        }

        if (videoProblemText == null)
        {
            if (videoPanel != null)
            {
                Transform videoTextTransform = videoPanel.transform.Find("Video Text");
                if (videoTextTransform == null)
                {
                    videoTextTransform = videoPanel.transform.Find("Video Text Panel/Video Text");
                }

                if (videoTextTransform == null)
                {
                    TextMeshProUGUI[] candidateTexts = videoPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (TextMeshProUGUI candidate in candidateTexts)
                    {
                        if (candidate != null && candidate.name == "Video Text")
                        {
                            videoProblemText = candidate;
                            break;
                        }
                    }
                }
                else
                {
                    videoProblemText = videoTextTransform.GetComponent<TextMeshProUGUI>();
                }
            }

            if (videoProblemText == null)
            {
                GameObject fallbackVideoText = GameObject.Find("Video Text");
                if (fallbackVideoText != null)
                {
                    videoProblemText = fallbackVideoText.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        if (videoProblemTextPanel == null)
        {
            if (videoPanel != null)
            {
                Transform panelTransform = videoPanel.transform.Find("Video Text Panel");
                if (panelTransform != null)
                {
                    videoProblemTextPanel = panelTransform.gameObject;
                }
            }

            if (videoProblemTextPanel == null && videoProblemText != null && videoProblemText.transform.parent != null)
            {
                videoProblemTextPanel = videoProblemText.transform.parent.gameObject;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (videoPlayer != null) videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void SetupEndingScreenUI()
    {
        if (endingScreenPanel == null) return;

        Canvas endingCanvas = endingScreenPanel.GetComponentInParent<Canvas>();
        if (endingCanvas != null)
        {
            GraphicRaycaster raycaster = endingCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = true;
            }
        }

        // Paksa gunakan teks dari Total Nilai Panel agar muat dan konsisten
        TextMeshProUGUI totalNilaiText = endingScreenPanel.transform
            .Find("Total Nilai Panel/Text (TMP)")
            ?.GetComponent<TextMeshProUGUI>();

        if (totalNilaiText != null)
        {
            endingPerformanceText = totalNilaiText;
        }

        // Non-interaktif panel dekoratif agar tidak menutup raycast tombol
        SetGraphicRaycast(endingScreenPanel.GetComponent<Image>(), false);
        SetGraphicRaycast(endingScreenPanel.transform.Find("Finished Text Panel")?.GetComponent<Image>(), false);
        SetGraphicRaycast(endingScreenPanel.transform.Find("Total Nilai Panel")?.GetComponent<Image>(), false);
        SetGraphicRaycast(endingScreenPanel.transform.Find("Total Nilai Panel/BL Border")?.GetComponent<Image>(), false);
        SetGraphicRaycast(endingScreenPanel.transform.Find("Total Nilai Panel/TR Border")?.GetComponent<Image>(), false);
        SetGraphicRaycast(endingScreenPanel.transform.Find("Total Nilai Panel/Text (TMP)")?.GetComponent<TextMeshProUGUI>(), false);
        SetGraphicRaycast(endingScreenPanel.transform.Find("Button Pause Panel")?.GetComponent<Image>(), false);

        GameplayManager gameplayManager = FindFirstObjectByType<GameplayManager>();
        if (gameplayManager == null) return;

        Button backToMainMenuButton = endingScreenPanel.transform
            .Find("Button Pause Panel/Back To Main Menu Button")
            ?.GetComponent<Button>();

        if (backToMainMenuButton != null)
        {
            backToMainMenuButton.onClick.RemoveListener(gameplayManager.LoadMainMenu);
            backToMainMenuButton.onClick.AddListener(gameplayManager.LoadMainMenu);
            backToMainMenuButton.interactable = true;
        }

        Button exitButton = endingScreenPanel.transform
            .Find("Button Pause Panel/Exit Button")
            ?.GetComponent<Button>();

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(gameplayManager.QuitGame);
            exitButton.onClick.AddListener(gameplayManager.QuitGame);
            exitButton.interactable = true;
        }
    }

    private static void SetGraphicRaycast(Graphic graphic, bool isEnabled)
    {
        if (graphic == null) return;
        graphic.raycastTarget = isEnabled;
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

        // Jangan reset data APD saat kuis dimulai.
        // Fallback: buat sesi baru hanya jika gameplay dibuka langsung ke quiz (tanpa fase APD).
        if (QuizSessionManager.Instance != null && QuizSessionManager.Instance.saveData.currentAttempt == null)
        {
            QuizSessionManager.Instance.StartNewAttempt();
        }

        // Munculkan Score Panel dengan animasi LeanTween
        if (ScoreSystemManager.Instance != null && QuizSessionManager.Instance.saveData.currentAttempt != null)
        {
            var attempt = QuizSessionManager.Instance.saveData.currentAttempt;
            ScoreSystemManager.Instance.ShowQuizScore(attempt.quizTotalCorrect, attempt.quizTotalWrong);
        }

        LoadNextQuestion();
    }

    private void LoadNextQuestion()
    {
        StopQuestion3ImagePresentation();

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
        if (videoProblemText != null)
        {
            videoProblemText.text = string.Empty;
            SetVideoProblemTextVisible(false);
        }

        yield return new WaitForSeconds(0.5f); // Tunggu animasi masuk selesai

        int totalPlayableVideos = 0;
        for (int i = 0; i < currentQuiz.questionVideos.Length; i++)
        {
            if (currentQuiz.questionVideos[i] != null) totalPlayableVideos++;
        }

        int currentPlayableIndex = 0;

        // 3. Putar setiap video satu per satu
        for (int i = 0; i < currentQuiz.questionVideos.Length; i++)
        {
            if (currentQuiz.questionVideos[i] == null) continue; // Skip jika slot kosong

            currentPlayableIndex++;
            introVideoFinished = false;
            UpdateVideoProblemText(currentPlayableIndex, totalPlayableVideos, currentQuiz.questionVideos[i]);

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
        SetVideoProblemTextVisible(false);
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
                SetVideoProblemTextVisible(false);
                videoPanel.SetActive(false);
            });
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        // Set flag agar coroutine tahu video sudah selesai
        introVideoFinished = true;
    }

    private void SetVideoProblemTextVisible(bool isVisible)
    {
        if (videoProblemTextPanel != null)
        {
            videoProblemTextPanel.SetActive(isVisible);
        }

        if (videoProblemText != null)
        {
            videoProblemText.gameObject.SetActive(isVisible);
        }
    }

    private void UpdateVideoProblemText(int currentVideoIndex, int totalVideoCount, VideoClip activeClip)
    {
        if (videoProblemText == null) return;

        string videoIssueLabel = GetVideoIssueLabel(currentVideoIndex, activeClip);
        videoProblemText.text = videoIssueLabel;
        SetVideoProblemTextVisible(true);
    }

    private string GetVideoIssueLabel(int currentVideoIndex, VideoClip activeClip)
    {
        int zeroBasedIndex = currentVideoIndex - 1;

        if (currentQuiz != null && currentQuiz.questionVideoIssueTexts != null &&
            zeroBasedIndex >= 0 && zeroBasedIndex < currentQuiz.questionVideoIssueTexts.Length)
        {
            string customIssue = currentQuiz.questionVideoIssueTexts[zeroBasedIndex];
            if (!string.IsNullOrWhiteSpace(customIssue))
            {
                return customIssue.Replace("\r", " ").Replace("\n", " ").Trim();
            }
        }

        if (activeClip != null && !string.IsNullOrWhiteSpace(activeClip.name))
        {
            return activeClip.name.Trim();
        }

        if (currentQuiz != null && !string.IsNullOrWhiteSpace(currentQuiz.questionID))
        {
            return currentQuiz.questionID.Trim();
        }

        return "Permasalahan belum tersedia.";
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
                SetQuestionImageVisible(true);
                questionImageDisplay.sprite = currentQuiz.questionImage;
            }
            else
            {
                SetQuestionImageVisible(false);
            }
        }

        ConfigureAnswerButtonsForCurrentQuestion();
        SetAnswerButtonsVisible(true);

        // Khusus soal yang butuh presentasi gambar: tampilkan image dulu, baru tampilkan tombol jawaban.
        if (ShouldPresentImagesBeforeAnswers(currentQuiz))
        {
            Sprite[] presentationImages = GetPresentationImagesForCurrentQuestion();
            if (presentationImages.Length > 0)
            {
                SetAnswerButtonsVisible(false);
                SetAnswerButtonsInteractable(false);
                currentTimer = 0f;
                isTimerRunning = false;
                UpdateTimerUI();
                question3PresentationCoroutine = StartCoroutine(PresentQuestion3ImagesThenEnableAnswers(presentationImages));
            }
            else
            {
                Debug.LogWarning("[QuizManager] Soal_3 tidak punya gambar presentasi. Tombol jawaban langsung aktif.");
                BeginQuestionTimer();
            }
        }
        else
        {
            BeginQuestionTimer();
        }

        // Refresh Skor UI Top Bar
        UpdateScoreUI();
    }

    private void OnAnswerSelected(int selectedIndex)
    {
        if (isQuestion3ImagePresentationRunning) return;

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

    private void ConfigureAnswerButtonsForCurrentQuestion()
    {
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < currentQuiz.answers.Length)
            {
                answerButtons[i].gameObject.SetActive(true);
                answerButtons[i].interactable = true;

                Image buttonImage = answerButtons[i].GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = defaultButtonColor;
                }

                if (i < answerTexts.Length && answerTexts[i] != null)
                {
                    answerTexts[i].text = currentQuiz.answers[i];
                }

                int answerIndex = i;
                answerButtons[i].onClick.RemoveAllListeners();
                answerButtons[i].onClick.AddListener(() => OnAnswerSelected(answerIndex));
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void SetAnswerButtonsInteractable(bool isInteractable)
    {
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] != null && answerButtons[i].gameObject.activeSelf)
            {
                answerButtons[i].interactable = isInteractable;
            }
        }
    }

    private void SetAnswerButtonsVisible(bool isVisible)
    {
        if (quizAnswerPanel != null)
        {
            quizAnswerPanel.SetActive(isVisible);
            return;
        }

        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] != null)
            {
                answerButtons[i].gameObject.SetActive(isVisible);
            }
        }
    }

    private void SetQuestionImageVisible(bool isVisible)
    {
        if (quizPicturePanel != null)
        {
            quizPicturePanel.SetActive(isVisible);
        }

        if (questionImageDisplay != null)
        {
            questionImageDisplay.gameObject.SetActive(isVisible);
        }
    }

    private void BeginQuestionTimer()
    {
        currentTimer = 0f;
        isTimerRunning = true;
        UpdateTimerUI();
    }

    private bool IsQuestion3(QuizData quiz)
    {
        return quiz != null &&
               !string.IsNullOrWhiteSpace(quiz.questionID) &&
               quiz.questionID.Trim().Equals("Soal_3", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldPresentImagesBeforeAnswers(QuizData quiz)
    {
        if (quiz == null) return false;
        if (IsQuestion3(quiz)) return true;
        return quiz.questionPresentationImages != null && quiz.questionPresentationImages.Length > 0;
    }

    private Sprite[] GetPresentationImagesForCurrentQuestion()
    {
        if (currentQuiz == null) return new Sprite[0];

        if (currentQuiz.questionPresentationImages != null && currentQuiz.questionPresentationImages.Length > 0)
        {
            List<Sprite> filtered = new List<Sprite>();
            for (int i = 0; i < currentQuiz.questionPresentationImages.Length; i++)
            {
                Sprite sprite = currentQuiz.questionPresentationImages[i];
                if (sprite != null) filtered.Add(sprite);
            }

            if (filtered.Count > 0)
            {
                return filtered.ToArray();
            }
        }

        // Fallback: jika baru ada 1 gambar, tetap dipresentasikan sekali.
        if (currentQuiz.questionImage != null)
        {
            return new[] { currentQuiz.questionImage };
        }

        return new Sprite[0];
    }

    private IEnumerator PresentQuestion3ImagesThenEnableAnswers(Sprite[] presentationImages)
    {
        isQuestion3ImagePresentationRunning = true;
        float perImageDuration = Mathf.Max(0.5f, currentQuiz != null ? currentQuiz.presentationImageDuration : 1.5f);
        Sprite lastShown = null;

        Debug.Log($"[QuizManager] Start image presentation for '{currentQuiz?.questionID}' with {presentationImages.Length} image(s), duration={perImageDuration:F2}s, timeScale={Time.timeScale:F2}");

        for (int i = 0; i < presentationImages.Length; i++)
        {
            Sprite sprite = presentationImages[i];
            if (sprite == null) continue;

            lastShown = sprite;
            if (questionImageDisplay != null)
            {
                SetQuestionImageVisible(true);
                questionImageDisplay.sprite = sprite;
            }

            // Gunakan waktu real agar flow tidak macet ketika Time.timeScale = 0.
            yield return new WaitForSecondsRealtime(perImageDuration);
        }

        // Untuk soal 3: tutup panel gambar setelah presentasi selesai, lalu tampilkan jawaban.
        if (IsQuestion3(currentQuiz))
        {
            SetQuestionImageVisible(false);
        }
        else if (questionImageDisplay != null)
        {
            // Untuk soal lain yang memakai presentationImages, tetap tampilkan image terakhir.
            if (currentQuiz != null && currentQuiz.questionImage != null)
            {
                SetQuestionImageVisible(true);
                questionImageDisplay.sprite = currentQuiz.questionImage;
            }
            else if (lastShown != null)
            {
                SetQuestionImageVisible(true);
                questionImageDisplay.sprite = lastShown;
            }
        }

        isQuestion3ImagePresentationRunning = false;
        question3PresentationCoroutine = null;
        SetAnswerButtonsVisible(true);
        SetAnswerButtonsInteractable(true);
        Debug.Log($"[QuizManager] Image presentation finished for '{currentQuiz?.questionID}'. Answer buttons enabled.");
        BeginQuestionTimer();
    }

    private void StopQuestion3ImagePresentation()
    {
        if (question3PresentationCoroutine != null)
        {
            StopCoroutine(question3PresentationCoroutine);
            question3PresentationCoroutine = null;
        }

        isQuestion3ImagePresentationRunning = false;
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

        // Sembunyikan Score Panel
        if (ScoreSystemManager.Instance != null)
        {
            ScoreSystemManager.Instance.HideScore();
        }

        yield return new WaitForSeconds(0.5f);
        HideQuiz();

        // Lepas forced interaction kursi supaya UI ending tidak menunggu tombol E lagi
        PlayerInteraction playerInteraction = FindFirstObjectByType<PlayerInteraction>();
        if (playerInteraction != null)
        {
            playerInteraction.ReleaseForcedInteraction();
        }

        // Pastikan wiring tombol/raycast tetap benar sebelum panel ditampilkan
        SetupEndingScreenUI();

        // Munculkan Ending Screen
        if (endingScreenPanel != null)
        {
            endingScreenPanel.SetActive(true);
            
            // Animasi LeanTween (opsional)
            CanvasGroup cg = endingScreenPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = endingScreenPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            LeanTween.alphaCanvas(cg, 1f, 0.5f);
        }

        // Tampilkan teks ringkas di Total Nilai Panel
        if (QuizSessionManager.Instance != null && QuizSessionManager.Instance.saveData.currentAttempt != null)
        {
            var attempt = QuizSessionManager.Instance.saveData.currentAttempt;
            if (endingPerformanceText != null)
            {
                int totalCorrect = attempt.apdTotalCorrect + attempt.quizTotalCorrect;
                int totalWrong = attempt.apdTotalWrong + attempt.quizTotalWrong;
                int totalJawaban = totalCorrect + totalWrong;
                int nilaiStandar = totalJawaban > 0
                    ? Mathf.RoundToInt((totalCorrect / (float)totalJawaban) * 100f)
                    : 0;

                int apdAnswered = attempt.apdTotalCorrect + attempt.apdTotalWrong;
                int quizAnswered = attempt.quizTotalCorrect + attempt.quizTotalWrong;
                float apdAccuracy = apdAnswered > 0
                    ? (attempt.apdTotalCorrect / (float)apdAnswered) * 100f
                    : 0f;
                float quizAccuracy = quizAnswered > 0
                    ? (attempt.quizTotalCorrect / (float)quizAnswered) * 100f
                    : 0f;
                float weightedK3 = (apdAccuracy * 0.6f) + (quizAccuracy * 0.4f);
                int apdPenalty = Mathf.Min(20, attempt.apdTotalWrong * 5);
                int nilaiK3 = Mathf.Clamp(Mathf.RoundToInt(weightedK3 - apdPenalty), 0, 100);

                endingPerformanceText.text = $"Std:{nilaiStandar} | K3:{nilaiK3}";
            }

            // Simpan Ke Server
            NetworkManager.SubmitFullScoreSafe();
        }

        onQuizFinishedCallback?.Invoke(); // Memanggil trigger ruangan untuk membuka pintu/lanjut jalan
    }

    public void HideQuiz()
    {
        StopQuestion3ImagePresentation();
        HideQuizContentOnly();
        SetVideoProblemTextVisible(false);
        if (videoPanel != null) videoPanel.SetActive(false);
    }

    private void HideQuizContentOnly()
    {
        isTimerRunning = false;
        if (teacherController != null) teacherController.StopVoice();

        // Sembunyikan elemen teks & gambar
        if (questionText != null) questionText.gameObject.SetActive(false);
        SetQuestionImageVisible(false);
        if (timerText != null) timerText.gameObject.SetActive(false);

        // Kembalikan tombol agar bisa dipencet lagi dan sembunyikan dari layar
        SetAnswerButtonsVisible(false);
        foreach (var btn in answerButtons) 
        {
            if (btn != null)
            {
                btn.interactable = true;
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
        var attempt = QuizSessionManager.Instance.saveData.currentAttempt;
        if (attempt == null) return;

        if (correctScoreText != null)
            correctScoreText.text = "Benar: " + attempt.quizTotalCorrect.ToString();
            
        if (wrongScoreText != null)
            wrongScoreText.text = "Salah: " + attempt.quizTotalWrong.ToString();

        // Update Global UI Score System
        if (ScoreSystemManager.Instance != null)
        {
            ScoreSystemManager.Instance.UpdateQuizScore(attempt.quizTotalCorrect, attempt.quizTotalWrong);
        }
    }
}

