using UnityEngine;
using TMPro;

/// <summary>
/// Singleton Manager untuk menampilkan UI Skor (APD atau Quiz) dengan animasi LeanTween.
/// Di-attach ke objek yang menyimpan UI Canvas (misal HUDManager).
/// </summary>
public class ScoreSystemManager : MonoBehaviour
{
    public static ScoreSystemManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Panel utamanya, berisi CanvasGroup untuk transisi")]
    public CanvasGroup userScorePanel;
    public TextMeshProUGUI rightScoreText; // Dipakai untuk APD dan Quiz (Benar)
    public TextMeshProUGUI wrongScoreText; // Dipakai untuk Quiz (Salah)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Auto-wiring UI Referensi Bawaan User
        if (userScorePanel == null)
        {
            Transform panelTr = transform.Find("Score Panel");
            if (panelTr != null)
            {
                userScorePanel = panelTr.GetComponent<CanvasGroup>();
                Transform textPanelTr = panelTr.Find("Text Panel");
                if (textPanelTr != null)
                {
                    Transform rxTr = textPanelTr.Find("Right Score Text");
                    if (rxTr != null) rightScoreText = rxTr.GetComponent<TextMeshProUGUI>();

                    Transform wxTr = textPanelTr.Find("Wrong Score Text");
                    if (wxTr != null) wrongScoreText = wxTr.GetComponent<TextMeshProUGUI>();
                }
            }
        }
    }

    private void Start()
    {
        if (userScorePanel != null)
        {
            userScorePanel.alpha = 0f;
            userScorePanel.gameObject.SetActive(false);
        }
    }

    // ==========================================
    // BAGIAN: APD SCORE
    // ==========================================
    public void ShowAPDScore(int collected, int total, int wrong)
    {
        UpdateAPDScore(collected, total, wrong);
        ShowPanelAnimation(userScorePanel);
    }

    public void UpdateAPDScore(int collected, int total, int wrong)
    {
        if (rightScoreText != null)
        {
            rightScoreText.gameObject.SetActive(true);
            rightScoreText.text = $"APD Lengkap: {collected} / {total}";
        }
        
        if (wrongScoreText != null)
        {
            wrongScoreText.gameObject.SetActive(true);
            wrongScoreText.text = $"Salah: {wrong}";
        }
    }

    // ==========================================
    // BAGIAN: QUIZ SCORE
    // ==========================================
    public void ShowQuizScore(int correct, int wrong)
    {
        UpdateQuizScore(correct, wrong);
        ShowPanelAnimation(userScorePanel);
    }

    public void UpdateQuizScore(int correct, int wrong)
    {
        if (rightScoreText != null)
        {
            rightScoreText.gameObject.SetActive(true);
            rightScoreText.text = $"Benar: {correct}";
        }
        if (wrongScoreText != null)
        {
            wrongScoreText.gameObject.SetActive(true);
            wrongScoreText.text = $"Salah: {wrong}";
        }
    }

    // ==========================================
    // BAGIAN: ANIMASI LEANTWEEN
    // ==========================================
    private void ShowPanelAnimation(CanvasGroup panelGroup)
    {
        if (panelGroup == null) return;

        panelGroup.gameObject.SetActive(true);
        LeanTween.cancel(panelGroup.gameObject);

        panelGroup.alpha = 0f;
        panelGroup.transform.localScale = Vector3.one * 0.8f; // Ganti ukuran sesuai kesukaan

        LeanTween.alphaCanvas(panelGroup, 1f, 0.4f).setEaseOutCubic();
        LeanTween.scale(panelGroup.gameObject, Vector3.one, 0.4f).setEaseOutBack();
    }

    public void HideScore()
    {
        if (userScorePanel == null || !userScorePanel.gameObject.activeSelf) return;

        LeanTween.cancel(userScorePanel.gameObject);

        LeanTween.alphaCanvas(userScorePanel, 0f, 0.3f).setEaseInCubic();
        LeanTween.scale(userScorePanel.gameObject, Vector3.one * 0.8f, 0.3f).setEaseInBack()
            .setOnComplete(() =>
            {
                userScorePanel.gameObject.SetActive(false);
            });
    }
}
