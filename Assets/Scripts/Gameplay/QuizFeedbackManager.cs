using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class QuizFeedbackManager : MonoBehaviour
{
    public static QuizFeedbackManager Instance { get; private set; }

    [Header("Audio SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip correctAnswerSound;
    [SerializeField] private AudioClip wrongAnswerSound;

    [Header("Visual Ikon Feedback")]
    [SerializeField] private Image feedbackIcon;
    [SerializeField] private Sprite correctTickSprite;
    [SerializeField] private Sprite wrongCrossSprite;
    [SerializeField] private float iconDisplayDuration = 2.0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (feedbackIcon != null)
            feedbackIcon.gameObject.SetActive(false);
    }

    /// <summary>
    /// Dipanggil oleh QuizManager saat pemain memilih jawaban.
    /// </summary>
    public void PlayFeedback(bool isCorrect)
    {
        // 1. Mainkan Suara
        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(isCorrect ? correctAnswerSound : wrongAnswerSound);
        }

        // 2. Mainkan Animasi UI (Centang / Silang)
        if (feedbackIcon != null)
        {
            feedbackIcon.sprite = isCorrect ? correctTickSprite : wrongCrossSprite;
            StartCoroutine(ShowIconRoutine());
        }
    }

    private IEnumerator ShowIconRoutine()
    {
        feedbackIcon.gameObject.SetActive(true);

        // Jika pakai LeanTween / DOTween, animasinya bisa ditaruh di sini
        // LeanTween.scale(feedbackIcon.gameObject, Vector3.one * 1.5f, 0.5f).setEasePunch();

        yield return new WaitForSeconds(iconDisplayDuration);
        feedbackIcon.gameObject.SetActive(false);
    }
}
