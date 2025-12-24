using System;

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
