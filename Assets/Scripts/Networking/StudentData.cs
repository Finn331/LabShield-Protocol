using System;
using System.Collections.Generic;

/// <summary>
/// Data payload yang dikirim ke Server Dashboard.
/// Berisi klasifikasi APD dan Quiz beserta timer detailnya.
/// </summary>
[Serializable]
public class StudentScorePayload
{
    public string studentName;
    public int attemptNumber;

    // --- KLASIFIKASI 1: APD ---
    public int apdTotalCorrect;
    public int apdTotalWrong;
    public float apdTimeTakenSeconds;

    // --- KLASIFIKASI 2: QUIZ ---
    public int quizTotalCorrect;
    public int quizTotalWrong;
    public List<QuestionTimePayload> questionTimes;
}

[Serializable]
public class QuestionTimePayload
{
    public string questionID;
    public float timeTaken;
    public bool isCorrect;
}

// Legacy class retained for backward compatibility
[Serializable]
public class StudentData
{
    public string studentName;
    public int questionsAnswered;
    public float score;

    public StudentData(string name, int count, float finalScore)
    {
        studentName = name;
        questionsAnswered = count;
        score = finalScore;
    }
}
