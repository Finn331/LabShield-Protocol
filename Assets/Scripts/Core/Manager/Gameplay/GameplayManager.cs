using UnityEngine;
using TMPro;

public class GameplayManager : MonoBehaviour
{
    public static GameplayManager Instance;

    [Header("Intro Conversation & Objective")]
    [Tooltip("Klip audio intro/percakapan awal yang akan dimainkan saat game mulai.")]
    public AudioClip introDialogClip;
    
    [Tooltip("Pesan objektif pertama yang muncul di layar.")]
    public string firstObjectiveText = "Pergi ke Ruang Ganti untuk menggunakan APD (Alat Pelindung Diri).";
    
    [Tooltip("Jeda waktu sebelum memunculkan teks objektif (agar tidak tertumpuk dengan transisi layar).")]
    public float objectiveDelay = 2.0f;

    [Header("Waypoints Sequence")]
    [Tooltip("Titik penanda pintu masuk Ruang APD.")]
    public Transform apdRoomWaypoint;
    [Tooltip("Titik penanda pintu masuk Ruang Ganti.")]
    public Transform changingRoomWaypoint;
    [Tooltip("Titik penanda pintu masuk Lab Kimia.")]
    public Transform chemLabWaypoint;
    [Tooltip("Titik penanda kursi untuk memulai kuis.")]
    public Transform quizChairWaypoint;

    private AudioSource audioSource;

    [Header("UI - Score")]
    public GameObject scorePanel;
    [SerializeField] TextMeshProUGUI rightScoreText;
    [SerializeField] TextMeshProUGUI wrongScoreText;
    [SerializeField] float timerUIScore;

    [Header("Score Setting")]
    public int rightScoreValue;
    public int wrongScoreValue;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (introDialogClip != null && audioSource != null)
        {
            audioSource.clip = introDialogClip;
            audioSource.Play();
        }
        StartCoroutine(ShowInitialObjective());
    }

    private System.Collections.IEnumerator ShowInitialObjective()
    {
        yield return new WaitForSeconds(objectiveDelay);

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.UpdateObjective(firstObjectiveText);
            // Aktifkan Waypoint pertama menuju Ruang APD
            HUDManager.Instance.SetWaypointTarget(apdRoomWaypoint);
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateScore();
    }

    #region UI - Score
    // For updating Score Value
    void UpdateScore()
    {
        rightScoreText.text = rightScoreValue.ToString();
        wrongScoreText.text = wrongScoreValue.ToString();
    }

    // For adding Right Score Value from another script
    public void RightScoreAdd()
    {
        rightScoreValue += 1;
    }

    // For adding Wrong Score Value from another script
    public void WrongScoreAdd()
    {
        wrongScoreValue += 1;
    }

    // For hiding Score Panel from another script
    public void ScoreHide()
    {
        LeanTween.scale(scorePanel, new Vector3(0f, 0f, 0f), timerUIScore).setEase(LeanTweenType.easeOutSine).setOnComplete(() =>
        {
            scorePanel.SetActive(false);
        }); 
    }

    // For showing Score Panel from another script
    public void ScoreUnhide()
    {
        scorePanel.SetActive(true);
        LeanTween.scale(scorePanel, new Vector3(1f, 1f, 1f), timerUIScore).setEase(LeanTweenType.easeOutSine);
    }
    #endregion


}
