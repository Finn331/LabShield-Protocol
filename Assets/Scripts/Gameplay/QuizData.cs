using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "NewQuiz", menuName = "LabShield/Quiz Data", order = 1)]
public class QuizData : ScriptableObject
{
    [Header("Identitas Soal")]
    [Tooltip("ID Unik soal. Contoh: K3_UU_01, Bahaya_01, Ledakan_01")]
    public string questionID;

    [Header("Konten Soal")]
    [TextArea(3, 5)]
    public string questionText;
    
    [Tooltip("Suara guru saat membacakan soal (Boleh kosong)")]
    public AudioClip questionAudio;

    [Tooltip("Gambar referensi untuk soal (Boleh kosong, misalnya untuk tabel/simbol)")]
    public Sprite questionImage;

    [Header("Presentasi Gambar (Opsional)")]
    [Tooltip("Jika diisi, gambar akan ditampilkan berurutan sebelum tombol jawaban diaktifkan.")]
    public Sprite[] questionPresentationImages;

    [Tooltip("Durasi tampil per gambar saat presentasi berurutan.")]
    public float presentationImageDuration = 1.5f;

    [Tooltip("Video pembuka yang akan diputar sebelum soal ini muncul (Opsional, bisa lebih dari 1)")]
    public VideoClip[] questionVideos;


    [Tooltip("Label permasalahan untuk setiap video pembuka. Urutan harus sama dengan questionVideos. Jika kosong, sistem pakai nama video clip.")]
    public string[] questionVideoIssueTexts;
    [Tooltip("Pilihan jawaban A, B, C, D")]
    public string[] answers = new string[4];

    [Tooltip("Indeks jawaban yang benar (0 = A, 1 = B, 2 = C, 3 = D)")]
    public int correctAnswerIndex;

    [Header("Evaluasi & Umpan Balik")]
    [Tooltip("Teks penjelasan Evaluasi yang akan muncul HANYA jika pemain menjawab Salah.")]
    [TextArea(3, 5)]
    public string evaluationText;
    
    [Tooltip("Suara guru saat menjelaskan jawaban (Boleh kosong)")]
    public AudioClip explainAudio;
}
