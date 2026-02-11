using UnityEngine;
using TMPro;

public class GameplayManager : MonoBehaviour
{
    [Header("UI - Score")]
    public GameObject scorePanel;
    [SerializeField] TextMeshProUGUI rightScoreText;
    [SerializeField] TextMeshProUGUI wrongScoreText;
    [SerializeField] float timerUIScore;

    [Header("Score Setting")]
    public int rightScoreValue;
    public int wrongScoreValue;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
