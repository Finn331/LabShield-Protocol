using UnityEngine;

[CreateAssetMenu(fileName = "NewQuiz", menuName = "LabShield/Quiz Data", order = 1)]
public class QuizData : ScriptableObject
{
    [Header("Identitas Soal")]
    [Tooltip("ID Unik soal. Contoh: K3_UU_01, Bahaya_01, Ledakan_01")]
    public string questionID;

    [Header("Konten Soal")]
    [TextArea(3, 5)]
    public string questionText;

    [Tooltip("Pilihan jawaban A, B, C, D")]
    public string[] answers = new string[4];

    [Tooltip("Indeks jawaban yang benar (0 = A, 1 = B, 2 = C, 3 = D)")]
    public int correctAnswerIndex;

    [Header("Evaluasi & Umpan Balik")]
    [Tooltip("Teks penjelasan Evaluasi yang akan muncul HANYA jika pemain menjawab Salah.")]
    [TextArea(3, 5)]
    public string evaluationText;
}
